using BuptGpaCalculator.Core.Importing;

namespace BuptGpaCalculator.Tests;

public sealed class AcademicScoreImportParserTests
{
    [Fact]
    public void Parse_WithAcademicSystemTable_IgnoresMetadataAndReadsRequiredColumns()
    {
        const string text = """
            查询条件：开课时间【2025-2026-1】
            序号	开课学期	课程编号	课程名称	成绩	学分	绩点
            1	2025-2026-1	COURSE-001	课程甲	90	3	3.81
            2	2025-2026-1	COURSE-002	课程乙	80	2	3.25
            """;

        var result = AcademicScoreImportParser.Parse(text);

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.Courses.Count);
        Assert.Equal("COURSE-001", result.Courses[0].CourseCode);
        Assert.Equal("课程乙", result.Courses[1].CourseName);
        Assert.Equal(2m, result.Courses[1].Credit);
    }

    [Fact]
    public void Parse_WithFullAcademicSystemColumnsAndCarriageReturnLines_ReadsTable()
    {
        const string text = "查询条件：开课时间【2042-2043-1】\r序号\t开课学期\t课程编号\t课程名称\t分组名\t成绩\t成绩标识\t学分\t总学时\t绩点\r1\t2042-2043-1\tSYN-2042\t虚构课程甲\t\t95\t\t2.5\t40\t3.95";

        var result = AcademicScoreImportParser.Parse(text);

        var course = Assert.Single(result.Courses);
        Assert.Empty(result.Issues);
        Assert.Equal("SYN-2042", course.CourseCode);
        Assert.Equal(95, course.Score);
        Assert.Equal(2.5m, course.Credit);
    }

    [Fact]
    public void Parse_WithInvalidRows_ReturnsOtherCoursesAndRowIssues()
    {
        const string text = """
            开课学期	课程名称	成绩	学分
            2025-2026-1	课程甲	90	3
            2025-2026-1	课程乙	缺考	2
            2025-2027-1	课程丙	80	2
            """;

        var result = AcademicScoreImportParser.Parse(text);

        Assert.Single(result.Courses);
        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.LineNumber == 3 && issue.Message.Contains("成绩", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.LineNumber == 4 && issue.Message.Contains("开课学期", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_WithoutResultHeader_ReturnsGuidance()
    {
        var result = AcademicScoreImportParser.Parse("这不是成绩表格");

        Assert.Empty(result.Courses);
        Assert.Single(result.Issues);
        Assert.Equal(0, result.Issues[0].LineNumber);
    }
}
