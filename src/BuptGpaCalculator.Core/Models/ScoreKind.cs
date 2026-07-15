namespace BuptGpaCalculator.Core.Models;

/// <summary>Identifies the original form of a course score.</summary>
public enum ScoreKind
{
    /// <summary>A numeric percentage score.</summary>
    Percentage = 0,

    /// <summary>A five-level score such as 优、良 or 及格.</summary>
    FiveLevel = 1,

    /// <summary>A two-level score such as 通过 or 不通过.</summary>
    TwoLevel = 2,
}
