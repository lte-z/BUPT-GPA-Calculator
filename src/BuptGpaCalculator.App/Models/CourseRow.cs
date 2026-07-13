using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.App.Models;

/// <summary>Provides an editable WPF representation of a course record.</summary>
public sealed class CourseRow : INotifyPropertyChanged
{
    private string courseCode;
    private string courseName;
    private string creditText;
    private bool isIncluded;
    private string scoreText;
    private string term;

    /// <summary>Initializes an empty editable course row.</summary>
    /// <param name="term">The initial academic term.</param>
    public CourseRow(string term)
        : this(Guid.NewGuid(), term, string.Empty, string.Empty, string.Empty, string.Empty, true)
    {
    }

    private CourseRow(Guid id, string term, string courseCode, string courseName, string scoreText, string creditText, bool isIncluded)
    {
        Id = id;
        this.term = term;
        this.courseCode = courseCode;
        this.courseName = courseName;
        this.scoreText = scoreText;
        this.creditText = creditText;
        this.isIncluded = isIncluded;
    }

    /// <summary>Occurs when an editable value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the internal course identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets or sets the academic term text.</summary>
    public string Term { get => term; set => SetField(ref term, value); }

    /// <summary>Gets or sets the optional course code.</summary>
    public string CourseCode { get => courseCode; set => SetField(ref courseCode, value); }

    /// <summary>Gets or sets the course name.</summary>
    public string CourseName { get => courseName; set => SetField(ref courseName, value); }

    /// <summary>Gets or sets the score text.</summary>
    public string ScoreText { get => scoreText; set => SetField(ref scoreText, value); }

    /// <summary>Gets or sets the credit text.</summary>
    public string CreditText { get => creditText; set => SetField(ref creditText, value); }

    /// <summary>Gets or sets a value indicating whether the course is included in GPA and GA.</summary>
    public bool IsIncluded { get => isIncluded; set => SetField(ref isIncluded, value); }

    /// <summary>Creates an editable row from a stored course.</summary>
    /// <param name="course">The stored course.</param>
    /// <returns>The corresponding row.</returns>
    public static CourseRow FromCourse(CourseRecord course) => new(
        course.Id,
        course.Term.ToString(),
        course.CourseCode ?? string.Empty,
        course.CourseName,
        course.Score.ToString(CultureInfo.InvariantCulture),
        course.Credit.ToString(CultureInfo.InvariantCulture),
        course.IsIncluded);

    /// <summary>Attempts to create a valid domain course for one student.</summary>
    /// <param name="studentId">The owning student number.</param>
    /// <param name="course">The created course when successful.</param>
    /// <param name="errorMessage">A user-facing validation message when unsuccessful.</param>
    /// <returns><see langword="true"/> when all fields are valid; otherwise, <see langword="false"/>.</returns>
    public bool TryToCourseRecord(string studentId, out CourseRecord? course, out string? errorMessage)
    {
        course = null;
        errorMessage = null;
        if (!AcademicTerm.TryParse(Term, out var academicTerm))
        {
            errorMessage = "开课学期必须采用连续学年的格式，例如 2025-2026-1。";
            return false;
        }

        if (!int.TryParse(ScoreText, NumberStyles.None, CultureInfo.InvariantCulture, out var score) || score is < 0 or > 100)
        {
            errorMessage = "成绩必须为 0 至 100 的整数。";
            return false;
        }

        if (!decimal.TryParse(CreditText, NumberStyles.Number, CultureInfo.InvariantCulture, out var credit) || credit < 0)
        {
            errorMessage = "学分必须为非负数。";
            return false;
        }

        try
        {
            course = new CourseRecord(Id, studentId, academicTerm, CourseCode, CourseName, score, credit, IsIncluded);
            return true;
        }
        catch (ArgumentException exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
