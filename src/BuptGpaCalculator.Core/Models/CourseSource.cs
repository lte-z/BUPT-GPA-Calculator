namespace BuptGpaCalculator.Core.Models;

/// <summary>Identifies how trustworthy and local a course row is.</summary>
public enum CourseSource
{
    /// <summary>The user added the course manually.</summary>
    Manual = 0,

    /// <summary>The course was imported from copied academic-system results and not edited locally.</summary>
    AcademicSystem = 1,

    /// <summary>The course came from the academic-system table but was edited locally.</summary>
    Modified = 2,
}
