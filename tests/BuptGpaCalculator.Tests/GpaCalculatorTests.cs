using BuptGpaCalculator.Core.Models;
using BuptGpaCalculator.Core.Services;

namespace BuptGpaCalculator.Tests;

public sealed class GpaCalculatorTests
{
    [Theory]
    [MemberData(nameof(OfficialScaleCases))]
    public void GetGradePoint_UsesOfficialScale(decimal score, decimal expectedGradePoint)
    {
        var gradePoint = GpaScale.GetGradePoint(score);

        Assert.Equal(expectedGradePoint, gradePoint);
    }

    public static TheoryData<decimal, decimal> OfficialScaleCases => new()
    {
        { 100m, 4.00m },
        { 99m, 4.00m },
        { 98m, 3.99m },
        { 93m, 3.91m },
        { 92m, 3.88m },
        { 89.5m, 3.79m },
        { 73m, 2.63m },
        { 71.5m, 2.48m },
        { 60m, 1.00m },
        { 59.9m, 0.00m },
        { 59m, 0.00m },
    };

    [Fact]
    public void Rules_ExposeOfficialFourColumnLabels()
    {
        Assert.Equal(77, GpaScale.Rules.Count);
        Assert.Contains(GpaScale.Rules, rule => rule.PercentageDisplayText == "小于 60.0" && rule.FiveLevelText == "不及格" && rule.TwoLevelText == "不通过" && rule.GradePoint == 0m);
        Assert.Contains(GpaScale.Rules, rule => rule.MinimumScore == 95m && rule.FiveLevelText == "优" && rule.GradePointDisplayText == "3.95");
        Assert.Contains(GpaScale.Rules, rule => rule.MinimumScore == 80m && rule.TwoLevelText == "通过");
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
    public void Calculate_WithNamedScores_UsesEquivalentPercentageValues()
    {
        var term = AcademicTerm.Parse("2025-2026-1");
        var result = GpaCalculator.Calculate(
        [
            CreateCourse(term, "A", "五级制课程", "优", 2m, true),
            CreateCourse(term, "B", "两级制课程", "不通过", 1m, true),
        ]);

        Assert.Equal(3m, result.IncludedCredit);
        Assert.Equal(83m, result.GradeAverage);
        Assert.Equal(2.6333333333333333333333333333m, result.Gpa);
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

    private static CourseRecord CreateCourse(AcademicTerm term, string code, string name, string score, decimal credit, bool isIncluded)
    {
        Assert.True(CourseScore.TryParse(score, out var parsed, out _));
        return new CourseRecord(Guid.Empty, "test-student", term, code, name, parsed!, credit, isIncluded);
    }
}
