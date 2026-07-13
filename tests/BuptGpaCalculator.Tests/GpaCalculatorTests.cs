using BuptGpaCalculator.Core.Models;
using BuptGpaCalculator.Core.Services;

namespace BuptGpaCalculator.Tests;

public sealed class GpaCalculatorTests
{
    [Theory]
    [InlineData(100, 4.00)]
    [InlineData(99, 4.00)]
    [InlineData(98, 3.99)]
    [InlineData(93, 3.91)]
    [InlineData(92, 3.88)]
    [InlineData(73, 2.63)]
    [InlineData(60, 1.00)]
    [InlineData(59, 0.00)]
    public void GetGradePoint_UsesLegacyScale(int score, decimal expectedGradePoint)
    {
        var gradePoint = GpaScale.GetGradePoint(score);

        Assert.Equal(expectedGradePoint, gradePoint);
    }

    [Fact]
    public void Calculate_WithWeightedCourses_ReturnsExpectedWeightedValues()
    {
        var term = AcademicTerm.Parse("2025-2026-1");
        var courses = new[]
        {
            CreateCourse(term, "COURSE-001", "课程甲", 90, 3m, true),
            CreateCourse(term, "COURSE-002", "课程乙", 80, 2m, true),
        };

        var result = GpaCalculator.Calculate(courses);

        Assert.Equal(5m, result.TotalCredit);
        Assert.Equal(5m, result.IncludedCredit);
        Assert.Equal(86m, result.GradeAverage);
        Assert.Equal(3.586m, result.Gpa);
    }

    [Fact]
    public void Calculate_ExcludesMarkedCoursesAndZeroCreditCoursesFromWeightedResults()
    {
        var term = AcademicTerm.Parse("2025-2026-1");
        var result = GpaCalculator.Calculate(
        [
            CreateCourse(term, "A", "计入课程", 90, 2m, true),
            CreateCourse(term, "B", "不计入课程", 100, 3m, false),
            CreateCourse(term, "C", "零学分课程", 100, 0m, true),
        ]);

        Assert.Equal(5m, result.TotalCredit);
        Assert.Equal(2m, result.IncludedCredit);
        Assert.Equal(90m, result.GradeAverage);
        Assert.Equal(3.81m, result.Gpa);
        Assert.Equal(0m, result.Contributions[1].GpaWeight);
        Assert.Equal(0m, result.Contributions[2].GpaWeight);
    }

    [Fact]
    public void Calculate_WithoutIncludedCredit_ReturnsEmptyAverages()
    {
        var result = GpaCalculator.Calculate(
        [
            CreateCourse(AcademicTerm.Parse("2025-2026-1"), "A", "不计入课程", 90, 2m, false),
        ]);

        Assert.Null(result.GradeAverage);
        Assert.Null(result.Gpa);
        Assert.Equal(0m, result.IncludedCredit);
    }

    private static CourseRecord CreateCourse(AcademicTerm term, string code, string name, int score, decimal credit, bool isIncluded) => new(
        Guid.Empty,
        "test-student",
        term,
        code,
        name,
        score,
        credit,
        isIncluded);
}
