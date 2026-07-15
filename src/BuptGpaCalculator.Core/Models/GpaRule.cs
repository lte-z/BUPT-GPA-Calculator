namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents one official score boundary and its corresponding grade point.</summary>
/// <param name="MinimumScore">The inclusive lower score boundary.</param>
/// <param name="GradePoint">The grade point assigned at and above the boundary.</param>
/// <param name="PercentageText">The official percentage-score display text.</param>
/// <param name="FiveLevelText">The optional five-level score at this boundary.</param>
/// <param name="TwoLevelText">The optional two-level score at this boundary.</param>
public sealed record GpaRule(decimal MinimumScore, decimal GradePoint, string? PercentageText = null, string? FiveLevelText = null, string? TwoLevelText = null)
{
    /// <summary>Gets the percentage text shown in the calculation rule table.</summary>
    public string PercentageDisplayText => PercentageText ?? MinimumScore.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets the grade point text shown in the calculation rule table.</summary>
    public string GradePointDisplayText => GradePoint.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
