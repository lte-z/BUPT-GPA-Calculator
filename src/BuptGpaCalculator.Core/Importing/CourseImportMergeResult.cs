using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Core.Importing;

/// <summary>Represents the atomically merged course set and a concise import summary.</summary>
public sealed record CourseImportMergeResult(IReadOnlyList<CourseRecord> Courses, int AddedCount, int UpdatedCount);
