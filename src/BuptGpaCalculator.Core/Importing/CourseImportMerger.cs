using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Core.Importing;

/// <summary>Merges one copied academic-system table into a student's existing courses.</summary>
public static class CourseImportMerger
{
    /// <summary>Updates matching courses and assigns the copied table's order as the default order.</summary>
    public static CourseImportMergeResult Merge(string studentId, IReadOnlyCollection<CourseRecord> existingCourses, IReadOnlyList<ImportedCourse> importedCourses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);
        ArgumentNullException.ThrowIfNull(existingCourses);
        ArgumentNullException.ThrowIfNull(importedCourses);

        var duplicateCode = importedCourses
            .Where(course => !string.IsNullOrWhiteSpace(course.CourseCode))
            .GroupBy(course => (course.Term, Code: Normalize(course.CourseCode!)))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCode is not null)
        {
            throw new ArgumentException($"导入内容中“{duplicateCode.Key.Term}”的课程编号“{duplicateCode.Key.Code}”重复，未执行导入。", nameof(importedCourses));
        }

        var records = existingCourses.ToList();
        var added = 0;
        var updated = 0;
        var nextOrder = records.Count == 0 ? 0 : records.Max(course => course.SortOrder) + 1;
        foreach (var termGroup in importedCourses.GroupBy(course => course.Term))
        {
            var order = 0;
            foreach (var imported in termGroup)
            {
                var match = FindMatch(records, imported);
                var record = new CourseRecord(
                    match?.Id ?? Guid.NewGuid(),
                    studentId,
                    imported.Term,
                    imported.CourseCode,
                    imported.CourseName,
                    imported.Score,
                    imported.Credit,
                    match?.IsIncluded ?? true,
                    match?.Source == CourseSource.Modified ? CourseSource.Modified : CourseSource.AcademicSystem,
                    order++);
                if (match is null)
                {
                    records.Add(record);
                    added++;
                }
                else
                {
                    records[records.IndexOf(match)] = record;
                    updated++;
                }
            }

            // Results not yet published remain visible after the current copied table; manually entered rows are always last.
            var trailingImported = records
                .Where(course => course.Term == termGroup.Key
                    && course.Source is CourseSource.AcademicSystem or CourseSource.Modified
                    && !termGroup.Any(item => IsSameCourse(course, item)))
                .OrderBy(course => course.SortOrder)
                .ToList();
            foreach (var course in trailingImported)
            {
                records[records.IndexOf(course)] = CopyWithOrder(course, order++);
            }

            var manual = records.Where(course => course.Term == termGroup.Key && course.Source == CourseSource.Manual).OrderBy(course => course.SortOrder).ToList();
            foreach (var course in manual)
            {
                records[records.IndexOf(course)] = CopyWithOrder(course, order++);
            }
            nextOrder = Math.Max(nextOrder, order);
        }

        return new CourseImportMergeResult(records.OrderBy(course => course.Term).ThenBy(course => course.SortOrder).ToList(), added, updated);
    }

    private static CourseRecord? FindMatch(IEnumerable<CourseRecord> existing, ImportedCourse imported)
    {
        if (!string.IsNullOrWhiteSpace(imported.CourseCode))
        {
            return existing.SingleOrDefault(course => course.Term == imported.Term && string.Equals(Normalize(course.CourseCode), Normalize(imported.CourseCode), StringComparison.Ordinal));
        }

        var byName = existing.Where(course => course.Term == imported.Term && string.Equals(course.CourseName, imported.CourseName, StringComparison.Ordinal)).ToList();
        return byName.Count == 1 ? byName[0] : null;
    }

    private static bool IsSameCourse(CourseRecord course, ImportedCourse imported) =>
        course.Term == imported.Term && (!string.IsNullOrWhiteSpace(imported.CourseCode)
            ? string.Equals(Normalize(course.CourseCode), Normalize(imported.CourseCode), StringComparison.Ordinal)
            : string.Equals(course.CourseName, imported.CourseName, StringComparison.Ordinal));

    private static CourseRecord CopyWithOrder(CourseRecord course, int sortOrder) => new(course.Id, course.StudentId, course.Term, course.CourseCode, course.CourseName, course.Score, course.Credit, course.IsIncluded, course.Source, sortOrder);

    private static string Normalize(string? code) => code?.Trim() ?? string.Empty;
}
