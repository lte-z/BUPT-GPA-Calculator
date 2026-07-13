using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Core.Services;

/// <summary>Converts percentage scores to grade points using the scale from the original calculator.</summary>
public static class GpaScale
{
    private static readonly GpaRule[] RulesInternal =
    [
        new(98.5m, 4.00m), new(97.5m, 3.99m), new(96.5m, 3.98m), new(96.0m, 3.97m),
        new(95.5m, 3.96m), new(95.0m, 3.95m), new(94.5m, 3.94m), new(94.0m, 3.93m),
        new(93.5m, 3.92m), new(93.0m, 3.91m), new(92.5m, 3.89m), new(92.0m, 3.88m),
        new(91.5m, 3.86m), new(91.0m, 3.85m), new(90.5m, 3.83m), new(90.0m, 3.81m),
        new(89.5m, 3.79m), new(89.0m, 3.77m), new(88.5m, 3.75m), new(88.0m, 3.73m),
        new(87.5m, 3.71m), new(87.0m, 3.68m), new(86.5m, 3.66m), new(86.0m, 3.63m),
        new(85.5m, 3.61m), new(85.0m, 3.58m), new(84.5m, 3.55m), new(84.0m, 3.52m),
        new(83.5m, 3.49m), new(83.0m, 3.46m), new(82.5m, 3.43m), new(82.0m, 3.39m),
        new(81.5m, 3.36m), new(81.0m, 3.32m), new(80.5m, 3.29m), new(80.0m, 3.25m),
        new(79.5m, 3.21m), new(79.0m, 3.17m), new(78.5m, 3.13m), new(78.0m, 3.09m),
        new(77.5m, 3.05m), new(77.0m, 3.01m), new(76.5m, 2.96m), new(76.0m, 2.92m),
        new(75.5m, 2.87m), new(75.0m, 2.83m), new(74.5m, 2.78m), new(74.0m, 2.73m),
        new(73.5m, 2.68m), new(73.0m, 2.63m), new(72.5m, 2.58m), new(72.0m, 2.53m),
        new(71.0m, 2.42m), new(70.5m, 2.37m), new(70.0m, 2.31m), new(69.5m, 2.26m),
        new(69.0m, 2.20m), new(68.5m, 2.14m), new(68.0m, 2.08m), new(67.5m, 2.02m),
        new(67.0m, 1.96m), new(66.5m, 1.90m), new(66.0m, 1.83m), new(65.5m, 1.77m),
        new(65.0m, 1.70m), new(64.5m, 1.64m), new(64.0m, 1.57m), new(63.5m, 1.50m),
        new(63.0m, 1.43m), new(62.5m, 1.36m), new(62.0m, 1.29m), new(61.5m, 1.22m),
        new(61.0m, 1.15m), new(60.5m, 1.07m), new(60.0m, 1.00m),
    ];

    /// <summary>Gets the score-to-grade-point table in descending score order.</summary>
    public static IReadOnlyList<GpaRule> Rules => RulesInternal;

    /// <summary>Gets the grade point for an integer percentage score.</summary>
    /// <param name="score">The score from 0 through 100.</param>
    /// <returns>The corresponding grade point.</returns>
    public static decimal GetGradePoint(int score)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "成绩必须为 0 至 100 的整数。");
        }

        foreach (var rule in RulesInternal)
        {
            if (score >= rule.MinimumScore)
            {
                return rule.GradePoint;
            }
        }

        return 0m;
    }
}
