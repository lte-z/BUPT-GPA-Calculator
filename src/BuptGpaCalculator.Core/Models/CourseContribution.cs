namespace BuptGpaCalculator.Core.Models;

/// <summary>Represents a course and its contribution to an aggregate calculation.</summary>
/// <param name="Course">The source course.</param>
/// <param name="GradePoint">The grade point determined by the GPA scale.</param>
/// <param name="GradeAverageWeight">The course's weighted-score contribution.</param>
/// <param name="GpaWeight">The course's weighted-GPA contribution.</param>
public sealed record CourseContribution(CourseRecord Course, decimal GradePoint, decimal GradeAverageWeight, decimal GpaWeight);
