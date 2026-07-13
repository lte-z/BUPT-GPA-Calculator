namespace BuptGpaCalculator.Core.Models;

/// <summary>Contains the results and per-course details of a GPA calculation.</summary>
public sealed record CalculationResult
{
    /// <summary>Initializes calculation results.</summary>
    /// <param name="gradeAverage">The weighted grade average, or <see langword="null"/> when no credit is included.</param>
    /// <param name="gpa">The weighted GPA, or <see langword="null"/> when no credit is included.</param>
    /// <param name="totalCredit">The sum of all recorded course credits.</param>
    /// <param name="includedCredit">The sum of credits included in GPA and GA.</param>
    /// <param name="contributions">The per-course calculation details.</param>
    public CalculationResult(decimal? gradeAverage, decimal? gpa, decimal totalCredit, decimal includedCredit, IReadOnlyList<CourseContribution> contributions)
    {
        GradeAverage = gradeAverage;
        Gpa = gpa;
        TotalCredit = totalCredit;
        IncludedCredit = includedCredit;
        Contributions = contributions;
    }

    /// <summary>Gets the weighted grade average.</summary>
    public decimal? GradeAverage { get; }

    /// <summary>Gets the weighted GPA.</summary>
    public decimal? Gpa { get; }

    /// <summary>Gets the sum of all recorded course credits.</summary>
    public decimal TotalCredit { get; }

    /// <summary>Gets the sum of credits included in GPA and GA.</summary>
    public decimal IncludedCredit { get; }

    /// <summary>Gets the calculation details for every course.</summary>
    public IReadOnlyList<CourseContribution> Contributions { get; }
}
