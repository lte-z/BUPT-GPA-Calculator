namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents one inclusive lower score boundary and its corresponding grade point.</summary>
/// <param name="MinimumScore">The inclusive lower score boundary.</param>
/// <param name="GradePoint">The grade point assigned at and above the boundary.</param>
public sealed record GpaRule(decimal MinimumScore, decimal GradePoint);
