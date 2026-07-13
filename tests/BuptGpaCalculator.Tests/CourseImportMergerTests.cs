using BuptGpaCalculator.Core.Importing;
using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Tests;

public sealed class CourseImportMergerTests
{
    [Fact]
    public void Merge_WhenNewTableAddsPublishedCourses_ReordersToAcademicSystemOrder()
    {
        var term = new AcademicTerm(2042, 1);
        var existing = new[]
        {
            Course("A", "虚构课程甲", term, 0),
            Course("C", "虚构课程丙", term, 1),
            Course("D", "虚构课程丁", term, 2),
        };
        var imported = new[]
        {
            Imported("A", "虚构课程甲", term, 96),
            Imported("B", "虚构课程乙", term, 93),
            Imported("C", "虚构课程丙", term, 90),
            Imported("D", "虚构课程丁", term, 87),
            Imported("E", "虚构课程戊", term, 84),
        };

        var result = CourseImportMerger.Merge("20420001", existing, imported);

        Assert.Equal(2, result.AddedCount);
        Assert.Equal(3, result.UpdatedCount);
        Assert.Equal(["A", "B", "C", "D", "E"], result.Courses.Select(course => course.CourseCode));
        Assert.All(result.Courses, course => Assert.Equal(CourseSource.AcademicSystem, course.Source));
    }

    [Fact]
    public void Merge_WhenImportedCourseMatchesManualEntry_RemovesManualLabelAndPlacesItInImportedOrder()
    {
        var term = new AcademicTerm(2042, 2);
        var manual = new CourseRecord(Guid.NewGuid(), "20420001", term, "CODE-X", "虚构手动课程", 70, 2m, false, CourseSource.Manual, 4);

        var result = CourseImportMerger.Merge("20420001", [manual], [Imported("CODE-X", "虚构手动课程", term, 88)]);

        var course = Assert.Single(result.Courses);
        Assert.False(course.IsIncluded);
        Assert.Equal(CourseSource.AcademicSystem, course.Source);
        Assert.Equal(0, course.SortOrder);
    }

    [Fact]
    public void Merge_WhenImportedCourseWasModified_KeepsModifiedSource()
    {
        var term = new AcademicTerm(2042, 2);
        var modified = new CourseRecord(Guid.NewGuid(), "20420001", term, "CODE-M", "虚构修改课程", 70, 2m, false, CourseSource.Modified, 4);

        var result = CourseImportMerger.Merge("20420001", [modified], [Imported("CODE-M", "虚构修改课程", term, 88)]);

        var course = Assert.Single(result.Courses);
        Assert.False(course.IsIncluded);
        Assert.Equal(CourseSource.Modified, course.Source);
        Assert.Equal(0, course.SortOrder);
    }

    [Fact]
    public void Merge_WhenPublishedCourseIsMissingFromNewTable_KeepsModifiedBeforeManualRows()
    {
        var term = new AcademicTerm(2042, 2);
        var modified = new CourseRecord(Guid.NewGuid(), "20420001", term, "CODE-M", "虚构修改课程", 70, 2m, false, CourseSource.Modified, 4);
        var manual = new CourseRecord(Guid.NewGuid(), "20420001", term, "CODE-X", "虚构手动课程", 70, 2m, false, CourseSource.Manual, 5);

        var result = CourseImportMerger.Merge("20420001", [modified, manual], [Imported("CODE-A", "虚构导入课程", term, 88)]);

        Assert.Equal(["CODE-A", "CODE-M", "CODE-X"], result.Courses.Select(course => course.CourseCode));
    }


    [Fact]
    public void Merge_WhenImportContainsDuplicateCourseCode_RejectsEntireImport()
    {
        var term = new AcademicTerm(2042, 1);
        var duplicate = new[] { Imported("SAME", "虚构课程甲", term, 90), Imported("SAME", "虚构课程乙", term, 91) };

        Assert.Throws<ArgumentException>(() => CourseImportMerger.Merge("20420001", [], duplicate));
    }

    private static CourseRecord Course(string code, string name, AcademicTerm term, int order) => new(Guid.NewGuid(), "20420001", term, code, name, 80, 2m, true, CourseSource.AcademicSystem, order);
    private static ImportedCourse Imported(string code, string name, AcademicTerm term, int score) => new(term, code, name, score, 2m, 1);
}
