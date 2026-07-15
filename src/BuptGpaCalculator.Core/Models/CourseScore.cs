using System.Globalization;

namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents a raw course score and its equivalent percentage value for calculation.</summary>
public sealed record CourseScore
{
    private static readonly IReadOnlyDictionary<string, (ScoreKind Kind, decimal Value)> NamedScores =
        new Dictionary<string, (ScoreKind, decimal)>(StringComparer.Ordinal)
        {
            ["优"] = (ScoreKind.FiveLevel, 95m),
            ["良"] = (ScoreKind.FiveLevel, 85m),
            ["中"] = (ScoreKind.FiveLevel, 75m),
            ["及格"] = (ScoreKind.FiveLevel, 65m),
            ["不及格"] = (ScoreKind.FiveLevel, 59m),
            ["通过"] = (ScoreKind.TwoLevel, 80m),
            ["不通过"] = (ScoreKind.TwoLevel, 59m),
        };

    private CourseScore(string text, ScoreKind kind, decimal value)
    {
        Text = text;
        Kind = kind;
        Value = value;
    }

    /// <summary>Gets the normalized raw score text.</summary>
    public string Text { get; }

    /// <summary>Gets the original score kind.</summary>
    public ScoreKind Kind { get; }

    /// <summary>Gets the equivalent percentage value used by GA and GPA calculation.</summary>
    public decimal Value { get; }

    /// <summary>Gets the score text shown in course lists and calculation details.</summary>
    public string DisplayText => Kind == ScoreKind.Percentage ? FormatPercentage(Value) : $"{Text}（{FormatPercentage(Value)}）";

    /// <summary>Creates a percentage score.</summary>
    public static CourseScore FromPercentage(decimal value) => CreatePercentage(value);

    /// <summary>Creates a score from values loaded from storage.</summary>
    public static CourseScore FromStored(string text, ScoreKind kind, decimal value)
    {
        if (kind == ScoreKind.Percentage)
        {
            return CreatePercentage(value);
        }

        var normalized = text.Trim();
        if (NamedScores.TryGetValue(normalized, out var mapping) && mapping.Kind == kind && mapping.Value == value)
        {
            return new CourseScore(normalized, kind, value);
        }

        throw new ArgumentException("成绩存储值与成绩类型不匹配。", nameof(text));
    }

    /// <summary>Attempts to parse a user-facing score value.</summary>
    public static bool TryParse(string? text, out CourseScore? score, out string? errorMessage)
    {
        score = null;
        errorMessage = null;
        var normalized = text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
        {
            errorMessage = "成绩不能为空。";
            return false;
        }

        if (NamedScores.TryGetValue(normalized, out var named))
        {
            score = new CourseScore(normalized, named.Kind, named.Value);
            return true;
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var percentage))
        {
            errorMessage = "成绩必须为 0 至 100 的百分制成绩，或优、良、中、及格、不及格、通过、不通过。";
            return false;
        }

        if (percentage is < 0m or > 100m)
        {
            errorMessage = "百分制成绩必须为 0 至 100。";
            return false;
        }

        if (decimal.Round(percentage, 1, MidpointRounding.AwayFromZero) != percentage)
        {
            errorMessage = "百分制成绩最多保留一位小数。";
            return false;
        }

        score = CreatePercentage(percentage);
        return true;
    }

    /// <summary>Formats a percentage value for user-facing score displays.</summary>
    public static string FormatPercentage(decimal value)
        => decimal.Truncate(value) == value
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);

    private static CourseScore CreatePercentage(decimal value)
    {
        if (value is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "百分制成绩必须为 0 至 100。");
        }

        if (decimal.Round(value, 1, MidpointRounding.AwayFromZero) != value)
        {
            throw new ArgumentException("百分制成绩最多保留一位小数。", nameof(value));
        }

        return new CourseScore(FormatPercentage(value), ScoreKind.Percentage, value);
    }
}
