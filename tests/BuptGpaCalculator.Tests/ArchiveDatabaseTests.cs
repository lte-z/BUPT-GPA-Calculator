using BuptGpaCalculator.Core.Models;
using BuptGpaCalculator.Core.Persistence;
using Microsoft.Data.Sqlite;

namespace BuptGpaCalculator.Tests;

public sealed class ArchiveDatabaseTests
{
    [Fact]
    public async Task Archive_RoundTripsStudentsAndCourses()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var archive = await ArchiveDatabase.CreateAsync(databasePath);
            var student = new StudentProfile("test-student", "测试用户");
            await archive.SaveStudentAsync(student);

            var term = AcademicTerm.Parse("2025-2026-2");
            await archive.ReplaceCoursesAsync(
                student.StudentId,
                [
                    CreateCourse(student.StudentId, term, "COURSE-001", "课程甲", 98, 0.5m, true),
                    CreateCourse(student.StudentId, term, "COURSE-002", "课程乙", 92, 4m, false, CourseSource.Modified),
                ]);

            var students = await archive.GetStudentsAsync();
            var courses = await archive.GetCoursesAsync(student.StudentId);

            Assert.Equal(student, Assert.Single(students));
            Assert.Equal(2, courses.Count);
            Assert.Contains(courses, course => course.CourseCode == "COURSE-001" && course.Credit == 0.5m);
            Assert.Contains(courses, course => course.CourseCode == "COURSE-002" && !course.IsIncluded && course.Source == CourseSource.Modified);
        }
        finally
        {
            DeleteTemporaryDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReplaceCourses_WhenDuplicateCourseCodesExist_RollsBackTheReplacement()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var archive = await ArchiveDatabase.CreateAsync(databasePath);
            var student = new StudentProfile("test-student", "测试用户");
            await archive.SaveStudentAsync(student);
            var term = AcademicTerm.Parse("2025-2026-1");
            var originalCourse = CreateCourse(student.StudentId, term, "A", "原课程", 80, 2m, true);
            await archive.ReplaceCoursesAsync(student.StudentId, [originalCourse]);

            await Assert.ThrowsAsync<SqliteException>(async () =>
                await archive.ReplaceCoursesAsync(
                    student.StudentId,
                    [
                        CreateCourse(student.StudentId, term, "DUP", "课程一", 80, 2m, true),
                        CreateCourse(student.StudentId, term, "DUP", "课程二", 90, 3m, true),
                    ]));

            var courses = await archive.GetCoursesAsync(student.StudentId);
            Assert.Equal(new[] { originalCourse }, courses);
        }
        finally
        {
            DeleteTemporaryDatabase(databasePath);
        }
    }

    [Fact]
    public async Task OpenAsync_WhenArchiveDoesNotExist_ThrowsFileNotFoundException()
    {
        var databasePath = CreateTemporaryDatabasePath();

        await Assert.ThrowsAsync<FileNotFoundException>(() => ArchiveDatabase.OpenAsync(databasePath));
    }

    [Fact]
    public async Task DeleteStudentAsync_RemovesStudentAndCourses()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var archive = await ArchiveDatabase.CreateAsync(databasePath);
            var student = new StudentProfile("test-student", "测试用户");
            await archive.SaveStudentAsync(student);
            await archive.ReplaceCoursesAsync(student.StudentId, [CreateCourse(student.StudentId, AcademicTerm.Parse("2025-2026-1"), "A", "课程甲", 80, 2m, true)]);

            await archive.DeleteStudentAsync(student.StudentId);

            Assert.Empty(await archive.GetStudentsAsync());
            Assert.Empty(await archive.GetCoursesAsync(student.StudentId));
        }
        finally
        {
            DeleteTemporaryDatabase(databasePath);
        }
    }

    [Fact]
    public async Task OpenAsync_WithVersion3Archive_MigratesIntegerScores()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            await CreateVersion3ArchiveAsync(databasePath);

            var archive = await ArchiveDatabase.OpenAsync(databasePath);
            var courses = await archive.GetCoursesAsync("test-student");

            var course = Assert.Single(courses);
            Assert.Equal("89", course.ScoreText);
            Assert.Equal(ScoreKind.Percentage, course.ScoreKind);
            Assert.Equal(89m, course.Score);

            await archive.ReplaceCoursesAsync(
                "test-student",
                [CreateCourse("test-student", AcademicTerm.Parse("2025-2026-2"), "NEW", "新成绩课程", CourseScore.FromStored("优", ScoreKind.FiveLevel, 95m), 1m, true)]);

            var savedCourse = Assert.Single(await archive.GetCoursesAsync("test-student"));
            Assert.Equal("优", savedCourse.ScoreText);
            Assert.Equal(ScoreKind.FiveLevel, savedCourse.ScoreKind);
            Assert.Equal(95m, savedCourse.Score);
            Assert.DoesNotContain("Score", await GetCourseColumnNamesAsync(databasePath));
        }
        finally
        {
            DeleteTemporaryDatabase(databasePath);
        }
    }

    [Fact]
    public async Task OpenAsync_WithPartiallyMigratedVersion4Archive_RemovesLegacyScoreColumn()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            await CreatePartiallyMigratedVersion4ArchiveAsync(databasePath);

            var archive = await ArchiveDatabase.OpenAsync(databasePath);
            await archive.ReplaceCoursesAsync(
                "test-student",
                [CreateCourse("test-student", AcademicTerm.Parse("2025-2026-2"), "NEW", "新成绩课程", CourseScore.FromStored("优", ScoreKind.FiveLevel, 95m), 1m, true)]);

            var savedCourse = Assert.Single(await archive.GetCoursesAsync("test-student"));
            Assert.Equal("优", savedCourse.ScoreText);
            Assert.Equal(ScoreKind.FiveLevel, savedCourse.ScoreKind);
            Assert.Equal(95m, savedCourse.Score);
            Assert.DoesNotContain("Score", await GetCourseColumnNamesAsync(databasePath));
        }
        finally
        {
            DeleteTemporaryDatabase(databasePath);
        }
    }

    private static CourseRecord CreateCourse(
        string studentId,
        AcademicTerm term,
        string code,
        string name,
        int score,
        decimal credit,
        bool isIncluded,
        CourseSource source = CourseSource.Manual) => new(Guid.Empty, studentId, term, code, name, score, credit, isIncluded, source);

    private static CourseRecord CreateCourse(
        string studentId,
        AcademicTerm term,
        string code,
        string name,
        CourseScore score,
        decimal credit,
        bool isIncluded,
        CourseSource source = CourseSource.Manual) => new(Guid.Empty, studentId, term, code, name, score, credit, isIncluded, source);

    private static string CreateTemporaryDatabasePath() => Path.Combine(Path.GetTempPath(), $"BuptGpaCalculator-{Guid.NewGuid():N}.db");

    private static async Task CreateVersion3ArchiveAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE ArchiveInfo (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL
            );
            CREATE TABLE Students (
                StudentId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE Courses (
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
                UpdatedAtUtc TEXT NOT NULL
            );
            INSERT INTO ArchiveInfo (Key, Value) VALUES ('SchemaVersion', '3');
            INSERT INTO Students (StudentId, Name, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('test-student', '测试用户', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
            INSERT INTO Courses (Id, StudentId, TermStartYear, TermNumber, CourseCode, CourseName, Score, Credit, IsIncluded, Source, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('00000000-0000-0000-0000-000000000001', 'test-student', 2025, 1, 'OLD', '旧档案课程', 89, '2', 1, 0, 0, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreatePartiallyMigratedVersion4ArchiveAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE ArchiveInfo (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL
            );
            CREATE TABLE Students (
                StudentId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE Courses (
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
                ScoreText TEXT NULL,
                ScoreKind INTEGER NOT NULL DEFAULT 0,
                ScoreValue TEXT NULL
            );
            INSERT INTO ArchiveInfo (Key, Value) VALUES ('SchemaVersion', '4');
            INSERT INTO Students (StudentId, Name, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('test-student', '测试用户', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
            INSERT INTO Courses (Id, StudentId, TermStartYear, TermNumber, CourseCode, CourseName, Score, Credit, IsIncluded, Source, SortOrder, CreatedAtUtc, UpdatedAtUtc, ScoreText, ScoreKind, ScoreValue)
            VALUES ('00000000-0000-0000-0000-000000000001', 'test-student', 2025, 1, 'OLD', '旧档案课程', 89, '2', 1, 0, 0, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '89', 0, '89');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyCollection<string>> GetCourseColumnNamesAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Courses);";

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static void DeleteTemporaryDatabase(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-journal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
