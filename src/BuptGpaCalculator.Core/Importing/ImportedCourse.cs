using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Core.Importing;

/// <summary>Represents one valid course parsed from copied academic-system text.</summary>
/// <param name="Term">The parsed academic term.</param>
/// <param name="CourseCode">The optional course code supplied by the academic system.</param>
/// <param name="CourseName">The course name.</param>
/// <param name="Score">The integer percentage score.</param>
/// <param name="Credit">The course credit.</param>
/// <param name="SourceLineNumber">The one-based line number in the pasted text.</param>
public sealed record ImportedCourse(
    AcademicTerm Term,
    string? CourseCode,
    string CourseName,
    int Score,
    decimal Credit,
    int SourceLineNumber);
