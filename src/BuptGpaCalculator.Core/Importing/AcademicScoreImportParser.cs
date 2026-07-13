using System.Globalization;
using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Core.Importing;

/// <summary>Parses tab-separated results copied from the BUPT academic-system result table.</summary>
public static class AcademicScoreImportParser
{
    private static readonly string[] RequiredHeaders = ["开课学期", "课程名称", "成绩", "学分"];

    /// <summary>Parses copied tab-separated result text without accessing the academic system.</summary>
    /// <param name="text">The text pasted by the user.</param>
    /// <returns>The valid courses and row-level issues.</returns>
    public static ImportParseResult Parse(string? text)
    {
        var courses = new List<ImportedCourse>();
        var issues = new List<ImportIssue>();
        if (string.IsNullOrWhiteSpace(text))
        {
            issues.Add(new ImportIssue(0, "请先粘贴教务系统中的成绩表格。"));
            return new ImportParseResult(courses, issues);
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var headerLineIndex = FindHeaderLine(lines);
        if (headerLineIndex < 0)
        {
            issues.Add(new ImportIssue(0, "未找到成绩表头，请复制包含“开课学期、课程名称、成绩、学分”的完整表格。"));
            return new ImportParseResult(courses, issues);
        }

        var headerMap = CreateHeaderMap(lines[headerLineIndex]);
        for (var index = headerLineIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (!TryCreateCourse(cells, headerMap, index + 1, out var course, out var issue))
            {
                issues.Add(issue!);
                continue;
            }

            courses.Add(course!);
        }

        return new ImportParseResult(courses, issues);
    }

    private static int FindHeaderLine(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var headers = CreateHeaderMap(lines[index]);
            if (RequiredHeaders.All(headers.ContainsKey))
            {
                return index;
            }
        }

        return -1;
    }

    private static Dictionary<string, int> CreateHeaderMap(string line)
    {
        var headers = line.TrimStart('\uFEFF').Split('\t');
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index].Trim();
            if (!string.IsNullOrEmpty(header) && !map.ContainsKey(header))
            {
                map.Add(header, index);
            }
        }

        return map;
    }

    private static bool TryCreateCourse(
        IReadOnlyList<string> cells,
        IReadOnlyDictionary<string, int> headerMap,
        int lineNumber,
        out ImportedCourse? course,
        out ImportIssue? issue)
    {
        course = null;
        issue = null;

        var termText = GetCell(cells, headerMap["开课学期"]);
        if (!AcademicTerm.TryParse(termText, out var term))
        {
            issue = new ImportIssue(lineNumber, "开课学期格式无效。");
            return false;
        }

        var courseName = GetCell(cells, headerMap["课程名称"]);
        if (string.IsNullOrWhiteSpace(courseName))
        {
            issue = new ImportIssue(lineNumber, "课程名称不能为空。");
            return false;
        }

        if (!int.TryParse(GetCell(cells, headerMap["成绩"]), NumberStyles.None, CultureInfo.InvariantCulture, out var score)
            || score is < 0 or > 100)
        {
            issue = new ImportIssue(lineNumber, "成绩必须为 0 至 100 的整数。");
            return false;
        }

        if (!decimal.TryParse(
                GetCell(cells, headerMap["学分"]),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var credit)
            || credit < 0)
        {
            issue = new ImportIssue(lineNumber, "学分必须为非负数。");
            return false;
        }

        var courseCode = headerMap.TryGetValue("课程编号", out var courseCodeIndex)
            ? GetCell(cells, courseCodeIndex)
            : null;
        course = new ImportedCourse(
            term,
            string.IsNullOrWhiteSpace(courseCode) ? null : courseCode.Trim(),
            courseName.Trim(),
            score,
            credit,
            lineNumber);
        return true;
    }

    private static string GetCell(IReadOnlyList<string> cells, int index) => index < cells.Count ? cells[index].Trim() : string.Empty;
}
