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

    private static CourseRecord CreateCourse(
        string studentId,
        AcademicTerm term,
        string code,
        string name,
        int score,
        decimal credit,
        bool isIncluded,
        CourseSource source = CourseSource.Manual) => new(Guid.Empty, studentId, term, code, name, score, credit, isIncluded, source);

    private static string CreateTemporaryDatabasePath() => Path.Combine(Path.GetTempPath(), $"BuptGpaCalculator-{Guid.NewGuid():N}.db");

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
