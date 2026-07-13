using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.Core.Services;

/// <summary>Calculates weighted GPA and grade averages for course records.</summary>
public static class GpaCalculator
{
    /// <summary>Calculates aggregate results for the supplied courses.</summary>
    /// <param name="courses">The courses in the calculation scope.</param>
    /// <returns>The aggregate values and per-course contributions.</returns>
    public static CalculationResult Calculate(IEnumerable<CourseRecord> courses)
    {
        ArgumentNullException.ThrowIfNull(courses);

        var contributions = new List<CourseContribution>();
        var totalCredit = 0m;
        var includedCredit = 0m;
        var gradeAverageTotal = 0m;
        var gpaTotal = 0m;

        foreach (var course in courses)
        {
            ArgumentNullException.ThrowIfNull(course);

            var gradePoint = GpaScale.GetGradePoint(course.Score);
            totalCredit += course.Credit;

            var gradeAverageWeight = 0m;
            var gpaWeight = 0m;
            if (course.IsIncluded)
            {
                includedCredit += course.Credit;
                gradeAverageWeight = course.Score * course.Credit;
                gpaWeight = gradePoint * course.Credit;
                gradeAverageTotal += gradeAverageWeight;
                gpaTotal += gpaWeight;
            }

            contributions.Add(new CourseContribution(course, gradePoint, gradeAverageWeight, gpaWeight));
        }

        return new CalculationResult(
            includedCredit == 0m ? null : gradeAverageTotal / includedCredit,
            includedCredit == 0m ? null : gpaTotal / includedCredit,
            totalCredit,
            includedCredit,
            contributions);
    }
}
