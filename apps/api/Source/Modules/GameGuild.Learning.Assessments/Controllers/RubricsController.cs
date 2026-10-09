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
    private readonly ICourseAccessEvaluator _courseAccessEvaluator;
    private readonly ILogger<RubricsController> _logger;
    private readonly ISender _sender;

    public RubricsController(
        IRubricService rubricService,
        IAssessmentService assessmentService,
        ICourseAccessEvaluator courseAccessEvaluator,
        ILogger<RubricsController> logger,
        ISender sender)
    {
        _rubricService = rubricService;
        _assessmentService = assessmentService;
        _courseAccessEvaluator = courseAccessEvaluator;
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
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

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
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanReadRubricAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

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
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

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

    private async Task<bool> CanReadRubricAsync(Guid courseId) =>
        await CanManageCourseAsync(courseId).ConfigureAwait(false) ||
        await _courseAccessEvaluator
            .HasCapabilityAsync(courseId, CourseCapability.StaffReview, HttpContext.RequestAborted)
            .ConfigureAwait(false);
            .ConfigureAwait(false);

    private Task<bool> CanManageCourseAsync(Guid courseId) =>
        _courseAccessEvaluator.HasCapabilityAsync(
            courseId,
            CourseCapability.Edit,
            HttpContext.RequestAborted);
}
