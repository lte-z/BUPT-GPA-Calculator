namespace BuptGpaCalculator.Core.Models;

/// <summary>
/// Represents one academic term in the <c>YYYY-YYYY-N</c> format used by the application.
/// </summary>
public sealed record AcademicTerm : IComparable<AcademicTerm>
{
    /// <summary>Initializes a new academic term.</summary>
    /// <param name="startYear">The first year of the academic year.</param>
    /// <param name="termNumber">The term number, from 1 through 3.</param>
    public AcademicTerm(int startYear, int termNumber)
    {
        if (startYear is < 1900 or > 9998)
        {
            throw new ArgumentOutOfRangeException(nameof(startYear), "学年起始年份必须在 1900 至 9998 之间。");
        }

        if (termNumber is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(termNumber), "学期编号必须为 1、2 或 3。");
        }

        StartYear = startYear;
        TermNumber = termNumber;
    }

    /// <summary>Gets the first year of the academic year.</summary>
    public int StartYear { get; }

    /// <summary>Gets the term number within the academic year.</summary>
    public int TermNumber { get; }

    /// <summary>Parses an academic term in the <c>YYYY-YYYY-N</c> format.</summary>
    /// <param name="value">The text to parse.</param>
    /// <returns>The parsed academic term.</returns>
    public static AcademicTerm Parse(string value)
    {
        if (TryParse(value, out var term))
        {
            return term;
        }

        throw new FormatException("学期必须采用连续学年的格式，例如 2025-2026-1。");
    }

    /// <summary>Attempts to parse an academic term in the <c>YYYY-YYYY-N</c> format.</summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="term">The parsed term when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out AcademicTerm term)
    {
        term = null!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var startYear)
            || !int.TryParse(parts[1], out var endYear)
            || !int.TryParse(parts[2], out var termNumber)
            || endYear != startYear + 1
            || startYear is < 1900 or > 9998
            || termNumber is < 1 or > 3)
        {
            return false;
        }

        term = new AcademicTerm(startYear, termNumber);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(AcademicTerm? other)
    {
        if (other is null)
        {
            return 1;
        }

        var yearComparison = StartYear.CompareTo(other.StartYear);
        return yearComparison != 0 ? yearComparison : TermNumber.CompareTo(other.TermNumber);
    }

    /// <inheritdoc />
    public override string ToString() => $"{StartYear}-{StartYear + 1}-{TermNumber}";
}
