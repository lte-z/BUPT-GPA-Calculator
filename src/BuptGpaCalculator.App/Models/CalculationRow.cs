using System.Globalization;
using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.App.Models;

/// <summary>Provides a presentation row for one course's calculation contribution.</summary>
/// <param name="contribution">The domain contribution.</param>
public sealed class CalculationRow(CourseContribution contribution)
{
    /// <summary>Gets the course name.</summary>
    public string CourseName { get; } = contribution.Course.CourseName;

    /// <summary>Gets the course score.</summary>
    public int Score { get; } = contribution.Course.Score;

    /// <summary>Gets the course credit.</summary>
    public string Credit { get; } = contribution.Course.Credit.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Gets the course grade point.</summary>
    public string GradePoint { get; } = contribution.GradePoint.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Gets the weighted-score contribution.</summary>
    public string GradeAverageWeight { get; } = contribution.GradeAverageWeight.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Gets the weighted-GPA contribution.</summary>
    public string GpaWeight { get; } = contribution.GpaWeight.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Gets whether the course is included in the aggregate result.</summary>
    public bool IsIncluded { get; } = contribution.Course.IsIncluded;
}
