using System.Globalization;
using BuptGpaCalculator.Core.Models;
using Microsoft.Data.Sqlite;

namespace BuptGpaCalculator.Core.Persistence;

/// <summary>Provides access to one local SQLite archive file.</summary>
public sealed class ArchiveDatabase
{
    private const int CurrentSchemaVersion = 3;
    private readonly string connectionString;

    private ArchiveDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
    }

    /// <summary>Gets the absolute path of the archive file.</summary>
    public string DatabasePath { get; }

    /// <summary>Creates and initializes a new archive at the specified path.</summary>
    /// <param name="databasePath">The absolute target path for the <c>.db</c> file.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>The initialized archive.</returns>
    public static async Task<ArchiveDatabase> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var archive = new ArchiveDatabase(NormalizePath(databasePath));
        await archive.InitializeAsync(cancellationToken);
        return archive;
    }

    /// <summary>Opens and validates an existing archive.</summary>
    /// <param name="databasePath">The absolute path of the existing <c>.db</c> file.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>The opened archive.</returns>
    public static async Task<ArchiveDatabase> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(databasePath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("未找到指定的成绩档案。", normalizedPath);
        }

        var archive = new ArchiveDatabase(normalizedPath);
        await archive.InitializeAsync(cancellationToken);
        return archive;
    }

    /// <summary>Gets every student in the archive.</summary>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>The students ordered by student number.</returns>
    public async Task<IReadOnlyList<StudentProfile>> GetStudentsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT StudentId, Name FROM Students ORDER BY StudentId COLLATE NOCASE;";

        var students = new List<StudentProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            students.Add(new StudentProfile(reader.GetString(0), reader.GetString(1)));
        }

        return students;
    }

    /// <summary>Creates or updates a student profile.</summary>
    /// <param name="student">The student to save.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>A task that completes when the profile is saved.</returns>
    public async Task SaveStudentAsync(StudentProfile student, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(student);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Students (StudentId, Name, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($studentId, $name, $now, $now)
            ON CONFLICT(StudentId) DO UPDATE SET
                Name = excluded.Name,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$studentId", student.StudentId);
        command.Parameters.AddWithValue("$name", student.Name);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Deletes a student and every course belonging to the student.</summary>
    /// <param name="studentId">The student number to delete.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>A task that completes when the student is deleted.</returns>
    public async Task DeleteStudentAsync(string studentId, CancellationToken cancellationToken = default)
    {
        ValidateStudentId(studentId);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Students WHERE StudentId = $studentId;";
        command.Parameters.AddWithValue("$studentId", studentId.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Gets all courses belonging to a student in their stable default display order.</summary>
    /// <param name="studentId">The student number.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>The student's courses.</returns>
    public async Task<IReadOnlyList<CourseRecord>> GetCoursesAsync(string studentId, CancellationToken cancellationToken = default)
    {
        ValidateStudentId(studentId);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, StudentId, TermStartYear, TermNumber, CourseCode, CourseName, Score, Credit, IsIncluded, Source, SortOrder
            FROM Courses
            WHERE StudentId = $studentId
            ORDER BY TermStartYear, TermNumber, SortOrder, Id;
            """;
        command.Parameters.AddWithValue("$studentId", studentId.Trim());

        var courses = new List<CourseRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            courses.Add(new CourseRecord(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                new AcademicTerm(reader.GetInt32(2), reader.GetInt32(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                decimal.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                reader.GetInt64(8) == 1,
                (CourseSource)reader.GetInt32(9),
                reader.GetInt32(10)));
        }

        return courses;
    }

    /// <summary>Replaces the complete set of courses for a student in one transaction.</summary>
    /// <param name="studentId">The student number that owns every supplied course.</param>
    /// <param name="courses">The replacement courses.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>A task that completes when the replacement is committed.</returns>
    public async Task ReplaceCoursesAsync(string studentId, IReadOnlyCollection<CourseRecord> courses, CancellationToken cancellationToken = default)
    {
        ValidateStudentId(studentId);
        ArgumentNullException.ThrowIfNull(courses);

        var normalizedStudentId = studentId.Trim();
        foreach (var course in courses)
        {
            ArgumentNullException.ThrowIfNull(course);
            if (!string.Equals(course.StudentId, normalizedStudentId, StringComparison.Ordinal))
            {
                throw new ArgumentException("所有课程必须属于同一名学生。", nameof(courses));
            }
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM Courses WHERE StudentId = $studentId;";
            deleteCommand.Parameters.AddWithValue("$studentId", normalizedStudentId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var course in courses)
        {
            await InsertCourseAsync(connection, transaction, course, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static string NormalizePath(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("档案路径不能为空。", nameof(databasePath));
        }

        var fullPath = Path.GetFullPath(databasePath);
        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(fullPath)))
        {
            throw new ArgumentException("档案路径必须包含目录。", nameof(databasePath));
        }

        return fullPath;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = DELETE;
            CREATE TABLE IF NOT EXISTS ArchiveInfo (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Students (
                StudentId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Courses (
                Id TEXT NOT NULL PRIMARY KEY,
                StudentId TEXT NOT NULL,
                TermStartYear INTEGER NOT NULL,
                TermNumber INTEGER NOT NULL,
                CourseCode TEXT NULL,
                CourseName TEXT NOT NULL,
                Score INTEGER NOT NULL CHECK (Score BETWEEN 0 AND 100),
                Credit TEXT NOT NULL,
                IsIncluded INTEGER NOT NULL CHECK (IsIncluded IN (0, 1)),
                Source INTEGER NOT NULL CHECK (Source IN (0, 1, 2)),
                SortOrder INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (StudentId) REFERENCES Students (StudentId) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Courses_Student_Term_Code
                ON Courses (StudentId, TermStartYear, TermNumber, CourseCode)
                WHERE CourseCode IS NOT NULL;
            INSERT OR IGNORE INTO ArchiveInfo (Key, Value) VALUES ('SchemaVersion', '3');
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT Value FROM ArchiveInfo WHERE Key = 'SchemaVersion';";
        var versionText = (string?)await versionCommand.ExecuteScalarAsync(cancellationToken);
        if (!int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            throw new InvalidDataException("该成绩档案来自早期原型或更新版本，无法由当前版本打开。请新建一个档案后重新导入成绩。");
        }

        if (version != CurrentSchemaVersion)
        {
            throw new InvalidDataException("该成绩档案来自早期原型或更新版本，无法由当前版本打开。请新建一个档案后重新导入成绩。");
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteProviderBootstrap.EnsureInitialized();
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task InsertCourseAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CourseRecord course,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Courses (
                Id, StudentId, TermStartYear, TermNumber, CourseCode, CourseName, Score, Credit, IsIncluded, Source, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES (
                $id, $studentId, $termStartYear, $termNumber, $courseCode, $courseName, $score, $credit, $isIncluded, $source, $sortOrder, $now, $now);
            """;
        command.Parameters.AddWithValue("$id", course.Id.ToString("D"));
        command.Parameters.AddWithValue("$studentId", course.StudentId);
        command.Parameters.AddWithValue("$termStartYear", course.Term.StartYear);
        command.Parameters.AddWithValue("$termNumber", course.Term.TermNumber);
        command.Parameters.AddWithValue("$courseCode", (object?)course.CourseCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$courseName", course.CourseName);
        command.Parameters.AddWithValue("$score", course.Score);
        command.Parameters.AddWithValue("$credit", course.Credit.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$isIncluded", course.IsIncluded ? 1 : 0);
        command.Parameters.AddWithValue("$source", (int)course.Source);
        command.Parameters.AddWithValue("$sortOrder", course.SortOrder);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateStudentId(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new ArgumentException("学号不能为空。", nameof(studentId));
        }
    }
}
