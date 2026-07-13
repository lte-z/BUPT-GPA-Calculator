namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents one percentage-based course result belonging to a student.</summary>
public sealed record CourseRecord
{
    /// <summary>Initializes a course record.</summary>
    /// <param name="id">The internal course identifier. An empty value creates a new identifier.</param>
    /// <param name="studentId">The student number that owns the course.</param>
    /// <param name="term">The academic term in which the course was taken.</param>
    /// <param name="courseCode">The optional course code.</param>
    /// <param name="courseName">The course name.</param>
    /// <param name="score">The integer score from 0 through 100.</param>
    /// <param name="credit">The non-negative course credit.</param>
    /// <param name="isIncluded">Whether the course contributes to GPA and GA.</param>
    public CourseRecord(Guid id, string studentId, AcademicTerm term, string? courseCode, string courseName, int score, decimal credit, bool isIncluded)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new ArgumentException("学号不能为空。", nameof(studentId));
        }

        ArgumentNullException.ThrowIfNull(term);

        if (string.IsNullOrWhiteSpace(courseName))
        {
            throw new ArgumentException("课程名称不能为空。", nameof(courseName));
        }

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "成绩必须为 0 至 100 的整数。");
        }

        if (credit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(credit), "学分不能为负数。");
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        StudentId = studentId.Trim();
        Term = term;
        CourseCode = string.IsNullOrWhiteSpace(courseCode) ? null : courseCode.Trim();
        CourseName = courseName.Trim();
        Score = score;
        Credit = credit;
        IsIncluded = isIncluded;
    }

    /// <summary>Gets the internal course identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the student number that owns this course.</summary>
    public string StudentId { get; }

    /// <summary>Gets the academic term.</summary>
    public AcademicTerm Term { get; }

    /// <summary>Gets the optional course code.</summary>
    public string? CourseCode { get; }

    /// <summary>Gets the course name.</summary>
    public string CourseName { get; }

    /// <summary>Gets the percentage score.</summary>
    public int Score { get; }

    /// <summary>Gets the course credit.</summary>
    public decimal Credit { get; }

    /// <summary>Gets a value indicating whether this course contributes to GPA and GA.</summary>
    public bool IsIncluded { get; }
}
