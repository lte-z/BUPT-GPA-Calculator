using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.App.Models;

/// <summary>Provides a display item for selecting a student.</summary>
/// <param name="Student">The represented student.</param>
public sealed record StudentItem(StudentProfile Student)
{
    /// <summary>Gets the display text shown in the student selector.</summary>
    public string DisplayName => $"{Student.Name} · {Student.StudentId}";
}
