namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents one course result belonging to a student.</summary>
public sealed record CourseRecord
{
    /// <summary>Initializes a course record.</summary>
    /// <param name="id">The internal course identifier. An empty value creates a new identifier.</param>
    /// <param name="studentId">The student number that owns the course.</param>
    /// <param name="term">The academic term in which the course was taken.</param>
    /// <param name="courseCode">The optional course code.</param>
    /// <param name="courseName">The course name.</param>
    /// <param name="score">The numeric percentage score from 0 through 100.</param>
    /// <param name="credit">The non-negative course credit.</param>
    /// <param name="isIncluded">Whether the course contributes to GPA and GA.</param>
    /// <param name="source">Where the course information came from.</param>
    /// <param name="sortOrder">The stable order used by the default course list.</param>
    public CourseRecord(Guid id, string studentId, AcademicTerm term, string? courseCode, string courseName, decimal score, decimal credit, bool isIncluded, CourseSource source = CourseSource.Manual, int sortOrder = 0)
        : this(id, studentId, term, courseCode, courseName, CourseScore.FromPercentage(score), credit, isIncluded, source, sortOrder)
    {
    }

    /// <summary>Initializes a course record.</summary>
    /// <param name="id">The internal course identifier. An empty value creates a new identifier.</param>
    /// <param name="studentId">The student number that owns the course.</param>
    /// <param name="term">The academic term in which the course was taken.</param>
    /// <param name="courseCode">The optional course code.</param>
    /// <param name="courseName">The course name.</param>
    /// <param name="score">The original score and its equivalent percentage value.</param>
    /// <param name="credit">The non-negative course credit.</param>
    /// <param name="isIncluded">Whether the course contributes to GPA and GA.</param>
    /// <param name="source">Where the course information came from.</param>
    /// <param name="sortOrder">The stable order used by the default course list.</param>
    public CourseRecord(Guid id, string studentId, AcademicTerm term, string? courseCode, string courseName, CourseScore score, decimal credit, bool isIncluded, CourseSource source = CourseSource.Manual, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new ArgumentException("学号不能为空。", nameof(studentId));
        }

        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(score);

        if (string.IsNullOrWhiteSpace(courseName))
        {
            throw new ArgumentException("课程名称不能为空。", nameof(courseName));
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
        CourseScore = score;
        Credit = credit;
        IsIncluded = isIncluded;
        Source = source;
        SortOrder = sortOrder;
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

    /// <summary>Gets the original score and its equivalent percentage value.</summary>
    public CourseScore CourseScore { get; }

    /// <summary>Gets the equivalent percentage score used in calculation.</summary>
    public decimal Score => CourseScore.Value;

    /// <summary>Gets the normalized raw score text.</summary>
    public string ScoreText => CourseScore.Text;

    /// <summary>Gets the original score kind.</summary>
    public ScoreKind ScoreKind => CourseScore.Kind;

    /// <summary>Gets the score text shown to users.</summary>
    public string ScoreDisplayText => CourseScore.DisplayText;

    /// <summary>Gets the course credit.</summary>
    public decimal Credit { get; }

    /// <summary>Gets a value indicating whether this course contributes to GPA and GA.</summary>
    public bool IsIncluded { get; }

    /// <summary>Gets the source shown alongside the course to keep manually entered results identifiable.</summary>
    public CourseSource Source { get; }

    /// <summary>Gets the stable default order within an academic term.</summary>
    public int SortOrder { get; }
}
