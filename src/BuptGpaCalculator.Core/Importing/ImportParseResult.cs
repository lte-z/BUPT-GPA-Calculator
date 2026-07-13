namespace BuptGpaCalculator.Core.Importing;

/// <summary>Contains valid courses and rejected rows from one pasted academic-system table.</summary>
public sealed record ImportParseResult
{
    /// <summary>Initializes a parsing result.</summary>
    /// <param name="courses">The valid imported courses.</param>
    /// <param name="issues">The rejected rows and parsing errors.</param>
    public ImportParseResult(IReadOnlyList<ImportedCourse> courses, IReadOnlyList<ImportIssue> issues)
    {
        Courses = courses;
        Issues = issues;
    }

    /// <summary>Gets valid courses found in the pasted text.</summary>
    public IReadOnlyList<ImportedCourse> Courses { get; }

    /// <summary>Gets rows or input states that could not be imported.</summary>
    public IReadOnlyList<ImportIssue> Issues { get; }
}
