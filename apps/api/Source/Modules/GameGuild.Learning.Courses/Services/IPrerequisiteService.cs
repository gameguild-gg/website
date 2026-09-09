
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Service interface for managing course prerequisites
/// </summary>
public interface IPrerequisiteService
{
    /// <summary>
    /// Creates a new prerequisite for a course
    /// </summary>
    Task<Result<CoursePrerequisite>> CreatePrerequisiteAsync(CreatePrerequisiteRequest request);

    /// <summary>
    /// Gets a prerequisite by ID
    /// </summary>
    Task<CoursePrerequisite?> GetPrerequisiteByIdAsync(Guid id);

    /// <summary>
    /// Gets all prerequisites for a course
    /// </summary>
    Task<IEnumerable<CoursePrerequisite>> GetCoursePrerequisitesAsync(Guid courseId, Guid? tenantId);

    /// <summary>
    /// Gets courses that require a specific course as prerequisite
    /// </summary>
    Task<IEnumerable<CoursePrerequisite>> GetDependentCoursesAsync(Guid prerequisiteCourseId, Guid? tenantId);

    /// <summary>
    /// Updates a prerequisite
    /// </summary>
    Task<Result<CoursePrerequisite>> UpdatePrerequisiteAsync(Guid id, UpdatePrerequisiteRequest request);

    /// <summary>
    /// Deletes a prerequisite
    /// </summary>
    Task<Result<bool>> DeletePrerequisiteAsync(Guid id);

    /// <summary>
    /// Checks if a user satisfies all prerequisites for a course
    /// </summary>
    Task<PrerequisiteCheckResult> CheckPrerequisitesAsync(Guid courseId, Guid userId, Guid? tenantId);

    /// <summary>
    /// Gets prerequisite chain (all prerequisites including nested)
    /// </summary>
    Task<IEnumerable<CoursePrerequisite>> GetPrerequisiteChainAsync(Guid courseId, Guid? tenantId);

    /// <summary>
    /// Validates that adding a prerequisite won't create a circular dependency
    /// </summary>
    Task<bool> WouldCreateCircularDependencyAsync(Guid courseId, Guid prerequisiteCourseId, Guid? tenantId);

    /// <summary>
    /// Reorders prerequisites for a course
    /// </summary>
    Task<Result<bool>> ReorderPrerequisitesAsync(Guid courseId, IEnumerable<Guid> prerequisiteIds);
}

// ===== Request DTOs =====

public sealed record CreatePrerequisiteRequest(
    Guid CourseId,
    Guid PrerequisiteCourseId,
    Guid? TenantId,
    PrerequisiteType Type = PrerequisiteType.Required,
    PercentValue? MinimumGrade = null,
    string? Description = null,
    int DisplayOrder = 0,
    string? PrerequisiteGroup = null);

public sealed record UpdatePrerequisiteRequest(
    PrerequisiteType? Type = null,
    PercentValue? MinimumGrade = null,
    string? Description = null,
    int? DisplayOrder = null,
    string? PrerequisiteGroup = null);

// ===== Response DTOs =====

public sealed record PrerequisiteCheckResult(
    bool IsSatisfied,
    IEnumerable<PrerequisiteStatus> Prerequisites);

public record PrerequisiteStatus(
    Guid PrerequisiteId,
    Guid PrerequisiteCourseId,
    string CourseName,
    PrerequisiteType Type,
    bool IsSatisfied,
    PercentValue? RequiredGrade,
    PercentValue? AchievedGrade,
    string? Reason);
