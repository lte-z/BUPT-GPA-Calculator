using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Tests;

public sealed class AcademicTermTests
{
    [Theory]
    [InlineData("2025-2026-1", 2025, 1)]
    [InlineData("2025-2026-3", 2025, 3)]
    [InlineData(" 2025-2026-2 ", 2025, 2)]
    public void TryParse_WithValidContinuousAcademicYear_ReturnsTerm(string value, int expectedStartYear, int expectedTermNumber)
    {
        var parsed = AcademicTerm.TryParse(value, out var term);

        Assert.True(parsed);
        Assert.Equal(expectedStartYear, term.StartYear);
        Assert.Equal(expectedTermNumber, term.TermNumber);
    }

    [Theory]
    [InlineData("2020-2026-1")]
    [InlineData("2025-2026-4")]
    [InlineData("2025-2026")]
    [InlineData("2025-2027-1")]
    public void TryParse_WithInvalidTerm_ReturnsFalse(string value)
    {
        var parsed = AcademicTerm.TryParse(value, out _);

        Assert.False(parsed);
    }
}
