namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents one student stored in an archive.</summary>
public sealed record StudentProfile
{
    /// <summary>Initializes a student profile.</summary>
    /// <param name="studentId">The unique student number.</param>
    /// <param name="name">The student's name.</param>
    public StudentProfile(string studentId, string name)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new ArgumentException("学号不能为空。", nameof(studentId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("姓名不能为空。", nameof(name));
        }

        StudentId = studentId.Trim();
        Name = name.Trim();
    }

    /// <summary>Gets the unique student number.</summary>
    public string StudentId { get; }

    /// <summary>Gets the student's name.</summary>
    public string Name { get; }
}
