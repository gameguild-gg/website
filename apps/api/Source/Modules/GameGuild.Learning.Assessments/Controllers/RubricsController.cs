using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Controller for an assessment's rubric: instructor authoring (PUT/DELETE) and reviewer reads (GET).
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/assessments/{assessmentId:guid}/rubric")]
[Authorize]
public class RubricsController : BaseApiController
{
    private readonly IRubricService _rubricService;
    private readonly IAssessmentService _assessmentService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly IProgramCrudService _programService;
    private readonly IPermissionQueryService _permissionQueryService;
    private readonly ILogger<RubricsController> _logger;
    private readonly ISender _sender;

    public RubricsController(
        IRubricService rubricService,
        IAssessmentService assessmentService,
        IActorContextAccessor actorContextAccessor,
        IProgramCrudService programService,
        IPermissionQueryService permissionQueryService,
        ILogger<RubricsController> logger,
        ISender sender)
    {
        _rubricService = rubricService;
        _assessmentService = assessmentService;
        _actorContextAccessor = actorContextAccessor;
        _programService = programService;
        _permissionQueryService = permissionQueryService;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// Create or fully replace the assessment's rubric. Instructor only.
    /// Locked (409) once any submission of the assessment is graded.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<RubricDto>> PutRubric(Guid assessmentId, [FromBody] SaveRubricRequest request)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();

        var result = await _sender.Send(new PutAssessmentRubricEndpointCommand(assessmentId, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return RubricRejection(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get the assessment's rubric. Open to course managers and reviewers
    /// (used by the grading panel and peer-review workspace).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RubricDto>> GetRubric(Guid assessmentId)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await CanReadRubricAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();

        var result = await _rubricService.GetAsync(assessmentId).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return RubricRejection(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Remove the rubric from the assessment. Instructor only. Locked once grading started.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> DeleteRubric(Guid assessmentId)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();

        var result = await _sender.Send(new DeleteAssessmentRubricEndpointCommand(assessmentId)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return RubricRejection(result.Error);
        }

        return NoContent();
    }

    private ActionResult RubricRejection(Error error)
    {
        _logger.LogWarning("Rubric request rejected: {ErrorCode} {ErrorDescription}", error.Code, error.Description);
        return error.Type switch
        {
            ErrorType.NotFound => NotFound(error),
            ErrorType.Conflict => Conflict(error),
            _ => BadRequest(error)
        };
    }

    private async Task<bool> CanReadRubricAsync(Guid courseId)
    {
        // Mirrors the GetSubmission reviewer gate: managers or Review-permission holders
        // (CanReviewCourseAsync includes the program-tenant check).
        return await CanManageCourseAsync(courseId).ConfigureAwait(false) ||
               await CanReviewCourseAsync(courseId).ConfigureAwait(false);
    }

    private async Task<bool> CanManageCourseAsync(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.IsSystemAdmin) return true;
        if (!actor.SubjectIdAsGuid.HasValue) return false;

        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null) return false;
        if (!actor.TenantId.HasValue) return false;
        if (program.TenantId.HasValue && program.TenantId != actor.TenantId) return false;
        if (program.CreatorId == actor.SubjectIdAsGuid.Value) return true;

        foreach (var permission in new[] { PermissionType.Edit, PermissionType.Create, PermissionType.Delete })
        {
            var permissionName = $"{nameof(Program)}.{courseId}.{permission}";
            if (await _permissionQueryService.HasTenantPermissionAsync(
                    actor.SubjectIdAsGuid.Value,
                    actor.TenantId,
                    permissionName).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsActorInProgramTenantAsync(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null) return false;
        if (actor.IsSystemAdmin) return true;

        return actor.TenantId.HasValue &&
               (!program.TenantId.HasValue || program.TenantId == actor.TenantId);
    }

    private async Task<bool> CanReviewCourseAsync(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue) return false;
        if (!await IsActorInProgramTenantAsync(courseId).ConfigureAwait(false)) return false;

        var permissionName = $"{nameof(Program)}.{courseId}.{PermissionType.Review}";
        return await _permissionQueryService.HasTenantPermissionAsync(
                actor.SubjectIdAsGuid.Value,
                actor.TenantId,
                permissionName)
            .ConfigureAwait(false);
    }
}
