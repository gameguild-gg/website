using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Represents a prerequisite relationship between courses.
/// A course can require completion of other courses before enrollment.
/// </summary>
[Table("course_prerequisites")]
[Index(nameof(CourseId), nameof(PrerequisiteCourseId), IsUnique = true)]
[Index(nameof(CourseId))]
[Index(nameof(PrerequisiteCourseId))]
[Index(nameof(TenantId))]
public class CoursePrerequisite : EntityBase
{
    /// <summary>
    /// The course that has prerequisites
    /// </summary>
    [Required]
    public Guid CourseId { get; private set; }

    /// <summary>
    /// The course that must be completed first
    /// </summary>
    [Required]
    public Guid PrerequisiteCourseId { get; private set; }

    /// <summary>
    /// Type of prerequisite requirement
    /// </summary>
    public PrerequisiteType Type { get; private set; } = PrerequisiteType.Required;

    /// <summary>
    /// Minimum grade required to satisfy the prerequisite (0-100).
    /// Null means completion is sufficient.
    /// </summary>
    public PercentValue? MinimumGrade { get; private set; }

    /// <summary>
    /// Optional description explaining the prerequisite requirement
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; private set; }

    /// <summary>
    /// Order for displaying prerequisites
    /// </summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Prerequisite group for OR logic. Prerequisites with the same group
    /// are alternatives (only one needs to be satisfied).
    /// </summary>
    [MaxLength(50)]
    public string? PrerequisiteGroup { get; private set; }

    // Navigation properties
    public virtual Program? Course { get; private set; }
    public virtual Program? PrerequisiteCourse { get; private set; }

    // Private constructor for EF Core
    private CoursePrerequisite() { }

    /// <summary>
    /// Creates a new course prerequisite
    /// </summary>
    public static CoursePrerequisite Create(
        Guid courseId,
        Guid prerequisiteCourseId,
        Guid? tenantId,
        PrerequisiteType type = PrerequisiteType.Required,
        PercentValue? minimumGrade = null,
        string? description = null,
        int displayOrder = 0,
        string? prerequisiteGroup = null)
    {
        if (courseId == prerequisiteCourseId)
        {
            throw new ArgumentException("A course cannot be a prerequisite of itself.");
        }

        return new CoursePrerequisite
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            PrerequisiteCourseId = prerequisiteCourseId,
            TenantId = tenantId,
            Type = type,
            MinimumGrade = minimumGrade,
            Description = description,
            DisplayOrder = displayOrder,
            PrerequisiteGroup = prerequisiteGroup
        };
    }

    /// <summary>
    /// Updates the prerequisite requirements
    /// </summary>
    public void Update(
        PrerequisiteType? type = null,
        PercentValue? minimumGrade = null,
        string? description = null,
        int? displayOrder = null,
        string? prerequisiteGroup = null)
    {
        if (type.HasValue)
            Type = type.Value;
        
        MinimumGrade = minimumGrade;
        Description = description;
        
        if (displayOrder.HasValue)
            DisplayOrder = displayOrder.Value;
        
        PrerequisiteGroup = prerequisiteGroup;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Sets the minimum grade requirement
    /// </summary>
    public void SetMinimumGrade(PercentValue? grade)
    {
        MinimumGrade = grade;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Type of prerequisite requirement
/// </summary>
public enum PrerequisiteType
{
    /// <summary>
    /// Must be completed before enrollment
    /// </summary>
    Required = 0,

    /// <summary>
    /// Recommended but not required
    /// </summary>
    Recommended = 1,

    /// <summary>
    /// Can be taken concurrently
    /// </summary>
    Corequisite = 2
}
