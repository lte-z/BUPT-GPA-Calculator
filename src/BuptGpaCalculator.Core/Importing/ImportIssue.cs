namespace BuptGpaCalculator.Core.Importing;

/// <summary>Describes one row that could not be imported.</summary>
/// <param name="LineNumber">The one-based line number in the pasted text, or zero when no header was found.</param>
/// <param name="Message">The user-facing reason that the row was rejected.</param>
public sealed record ImportIssue(int LineNumber, string Message);
