using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Controller for managing course prerequisites
/// </summary>
[Route("api/prerequisites")]
[Authorize]
public class PrerequisitesController : BaseApiController
{
    private readonly IPrerequisiteService _prerequisiteService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly ILogger<PrerequisitesController> _logger;
    private readonly ISender _sender;

    public PrerequisitesController(
        IPrerequisiteService prerequisiteService,
        IActorContextAccessor actorContextAccessor,
        ILogger<PrerequisitesController> logger,
        ISender sender)
    {
        _prerequisiteService = prerequisiteService;
        _actorContextAccessor = actorContextAccessor;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// Create a new prerequisite for a course
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PrerequisiteDto>> CreatePrerequisite([FromBody] CreatePrerequisiteApiRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;

        var createRequest = new CreatePrerequisiteRequest(
            request.CourseId,
            request.PrerequisiteCourseId,
            actor.TenantId,
            request.Type,
            request.MinimumGrade,
            request.Description,
            request.DisplayOrder,
            request.PrerequisiteGroup);

        var result = await _sender.Send(new CreatePrerequisiteEndpointCommand(createRequest)).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetPrerequisite), 
            new { id = result.Value.Id }, 
            PrerequisiteDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Get a prerequisite by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrerequisiteDto>> GetPrerequisite(Guid id)
    {
        var prerequisite = await _prerequisiteService.GetPrerequisiteByIdAsync(id).ConfigureAwait(false);
        
        if (prerequisite == null)
        {
            return NotFound();
        }

        return Ok(PrerequisiteDto.FromEntity(prerequisite));
    }

    /// <summary>
    /// Get all prerequisites for a course
    /// </summary>
    [HttpGet("course/{courseId:guid}")]
    public async Task<ActionResult<IEnumerable<PrerequisiteDto>>> GetCoursePrerequisites(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var prerequisites = await _prerequisiteService.GetCoursePrerequisitesAsync(courseId, actor.TenantId).ConfigureAwait(false);
        
        return Ok(prerequisites.Select(PrerequisiteDto.FromEntity));
    }

    /// <summary>
    /// Get courses that depend on a specific course as a prerequisite
    /// </summary>
    [HttpGet("dependents/{courseId:guid}")]
    public async Task<ActionResult<IEnumerable<PrerequisiteDto>>> GetDependentCourses(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var dependents = await _prerequisiteService.GetDependentCoursesAsync(courseId, actor.TenantId).ConfigureAwait(false);
        
        return Ok(dependents.Select(PrerequisiteDto.FromEntity));
    }

    /// <summary>
    /// Get the full prerequisite chain for a course (all nested prerequisites)
    /// </summary>
    [HttpGet("course/{courseId:guid}/chain")]
    public async Task<ActionResult<IEnumerable<PrerequisiteDto>>> GetPrerequisiteChain(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var chain = await _prerequisiteService.GetPrerequisiteChainAsync(courseId, actor.TenantId).ConfigureAwait(false);
        
        return Ok(chain.Select(PrerequisiteDto.FromEntity));
    }

    /// <summary>
    /// Check if the current user satisfies all prerequisites for a course
    /// </summary>
    [HttpGet("course/{courseId:guid}/check")]
    public async Task<ActionResult<PrerequisiteCheckResultDto>> CheckPrerequisites(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        
        if (!actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized("User ID is required to check prerequisites.");
        }

        var result = await _prerequisiteService.CheckPrerequisitesAsync(
            courseId, 
            actor.SubjectIdAsGuid.Value, 
            actor.TenantId).ConfigureAwait(false);

        return Ok(new PrerequisiteCheckResultDto(
            result.IsSatisfied,
            result.Prerequisites.Select(p => new PrerequisiteStatusDto(
                p.PrerequisiteId,
                p.PrerequisiteCourseId,
                p.CourseName,
                p.Type,
                p.IsSatisfied,
                p.RequiredGrade,
                p.AchievedGrade,
                p.Reason))));
    }

    /// <summary>
    /// Check if a specific user satisfies all prerequisites for a course (admin)
    /// </summary>
    [HttpGet("course/{courseId:guid}/check/{userId:guid}")]
    public async Task<ActionResult<PrerequisiteCheckResultDto>> CheckPrerequisitesForUser(Guid courseId, Guid userId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var result = await _prerequisiteService.CheckPrerequisitesAsync(courseId, userId, actor.TenantId).ConfigureAwait(false);

        return Ok(new PrerequisiteCheckResultDto(
            result.IsSatisfied,
            result.Prerequisites.Select(p => new PrerequisiteStatusDto(
                p.PrerequisiteId,
                p.PrerequisiteCourseId,
                p.CourseName,
                p.Type,
                p.IsSatisfied,
                p.RequiredGrade,
                p.AchievedGrade,
                p.Reason))));
    }

    /// <summary>
    /// Update a prerequisite
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PrerequisiteDto>> UpdatePrerequisite(
        Guid id, 
        [FromBody] UpdatePrerequisiteApiRequest request)
    {
        var updateRequest = new UpdatePrerequisiteRequest(
            request.Type,
            request.MinimumGrade,
            request.Description,
            request.DisplayOrder,
            request.PrerequisiteGroup);

        var result = await _sender.Send(new UpdatePrerequisiteEndpointCommand(id, updateRequest)).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return Ok(PrerequisiteDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Delete a prerequisite
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeletePrerequisite(Guid id)
    {
        var result = await _sender.Send(new DeletePrerequisiteEndpointCommand(id)).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound 
                ? NotFound(result.Error) 
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Reorder prerequisites for a course
    /// </summary>
    [HttpPost("course/{courseId:guid}/reorder")]
    public async Task<ActionResult> ReorderPrerequisites(Guid courseId, [FromBody] ReorderPrerequisitesRequest request)
    {
        var result = await _sender.Send(new ReorderPrerequisitesEndpointCommand(courseId, request.PrerequisiteIds)).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok();
    }

    /// <summary>
    /// Check if adding a prerequisite would create a circular dependency
    /// </summary>
    [HttpGet("course/{courseId:guid}/would-create-cycle/{prerequisiteCourseId:guid}")]
    public async Task<ActionResult<CircularDependencyCheckResult>> CheckCircularDependency(
        Guid courseId, 
        Guid prerequisiteCourseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var wouldCreateCycle = await _prerequisiteService.WouldCreateCircularDependencyAsync(
            courseId, 
            prerequisiteCourseId, 
            actor.TenantId).ConfigureAwait(false);

        return Ok(new CircularDependencyCheckResult(wouldCreateCycle));
    }
}

// ===== API Request DTOs =====

public sealed record CreatePrerequisiteApiRequest(
    Guid CourseId,
    Guid PrerequisiteCourseId,
    PrerequisiteType Type = PrerequisiteType.Required,
    int? MinimumGrade = null,
    string? Description = null,
    int DisplayOrder = 0,
    string? PrerequisiteGroup = null);

public sealed record UpdatePrerequisiteApiRequest(
    PrerequisiteType? Type = null,
    int? MinimumGrade = null,
    string? Description = null,
    int? DisplayOrder = null,
    string? PrerequisiteGroup = null);

public sealed record ReorderPrerequisitesRequest(IEnumerable<Guid> PrerequisiteIds);

// ===== Response DTOs =====

public sealed record PrerequisiteDto(
    Guid Id,
    Guid CourseId,
    Guid PrerequisiteCourseId,
    string? PrerequisiteCourseName,
    Guid? TenantId,
    PrerequisiteType Type,
    int? MinimumGrade,
    string? Description,
    int DisplayOrder,
    string? PrerequisiteGroup,
    DateTime CreatedAt)
{
    public static PrerequisiteDto FromEntity(CoursePrerequisite entity) => new(
        entity.Id,
        entity.CourseId,
        entity.PrerequisiteCourseId,
        entity.PrerequisiteCourse?.Title,
        entity.TenantId,
        entity.Type,
        entity.MinimumGrade,
        entity.Description,
        entity.DisplayOrder,
        entity.PrerequisiteGroup,
        entity.CreatedAt);
}

public sealed record PrerequisiteCheckResultDto(
    bool IsSatisfied,
    IEnumerable<PrerequisiteStatusDto> Prerequisites);

public sealed record PrerequisiteStatusDto(
    Guid PrerequisiteId,
    Guid PrerequisiteCourseId,
    string CourseName,
    PrerequisiteType Type,
    bool IsSatisfied,
    int? RequiredGrade,
    int? AchievedGrade,
    string? Reason);

public sealed record CircularDependencyCheckResult(bool WouldCreateCycle);
