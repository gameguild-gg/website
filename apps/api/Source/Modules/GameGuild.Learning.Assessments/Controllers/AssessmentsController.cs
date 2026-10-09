using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Runtime;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Controller for assessment management and submission handling
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/assessments")]
[Authorize]
public class AssessmentsController : BaseApiController
{
    private readonly IAssessmentService _assessmentService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly IProgramCrudService _programService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly ICourseAccessEvaluator _courseAccessEvaluator;
    private readonly IGradingQueueService _gradingQueueService;
    private readonly IAssessmentAuthoringService _authoringService;
    private readonly IAssessmentGradingRuntimeService? _gradingRuntime;
    private readonly IAssessmentGradebookProjectionService? _gradebookProjection;
    private readonly IAssessmentLearnerResultProjectionService? _learnerResultProjection;
    private readonly ILogger<AssessmentsController> _logger;
    private readonly ISender _sender;

    public AssessmentsController(
        IAssessmentService assessmentService,
        IActorContextAccessor actorContextAccessor,
        IProgramCrudService programService,
        IEnrollmentService enrollmentService,
        ICourseAccessEvaluator courseAccessEvaluator,
        IGradingQueueService gradingQueueService,
        IAssessmentAuthoringService authoringService,
        ILogger<AssessmentsController> logger,
        ISender sender,
        IAssessmentGradingRuntimeService? gradingRuntime = null,
        IAssessmentGradebookProjectionService? gradebookProjection = null,
        IAssessmentLearnerResultProjectionService? learnerResultProjection = null)
    {
        _assessmentService = assessmentService;
        _actorContextAccessor = actorContextAccessor;
        _programService = programService;
        _enrollmentService = enrollmentService;
        _courseAccessEvaluator = courseAccessEvaluator;
        _gradingQueueService = gradingQueueService;
        _authoringService = authoringService;
        _logger = logger;
        _sender = sender;
        _gradingRuntime = gradingRuntime;
        _gradebookProjection = gradebookProjection;
        _learnerResultProjection = learnerResultProjection;
    }

    // ===== ASSESSMENT MANAGEMENT =====

    /// <summary>Atomically saves assessable content and its assessment policy.</summary>
    [HttpPut("course/{courseId:guid}/content/{contentId:guid}/draft")]
    public async Task<ActionResult<AssessmentDraftResult>> SaveAssessmentDraft(
        Guid courseId,
        Guid contentId,
        [FromBody] SaveAssessmentDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanManageCourseAsync(courseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _sender
            .Send(
                new SaveAssessmentDraftEndpointCommand(courseId, contentId, actorId.Value, request),
                cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>Prepares an immutable candidate revision for instructor testing.</summary>
    [HttpGet("{id:guid}/authoring-state")]
    public async Task<ActionResult<AssessmentAuthoringStateResult>> GetAssessmentAuthoringState(
        Guid id,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _authoringService.GetStateAsync(id, cancellationToken).ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>Prepares an immutable candidate revision for instructor testing.</summary>
    [HttpPost("{id:guid}/revisions/prepare")]
    public async Task<ActionResult<PreparedAssessmentRevisionResult>> PrepareAssessmentRevision(
        Guid id,
        [FromBody] PrepareAssessmentRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _sender
            .Send(new PrepareAssessmentRevisionEndpointCommand(id, actorId.Value, request), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>Publishes exactly the prepared revision after official capability validation.</summary>
    [HttpPost("{id:guid}/revisions/publish")]
    public async Task<ActionResult<PreparedAssessmentRevisionResult>> PublishAssessmentRevision(
        Guid id,
        [FromBody] PublishAssessmentRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Publish).ConfigureAwait(false))
        {
            return Forbid();
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _sender
            .Send(new PublishAssessmentRevisionEndpointCommand(id, actorId.Value, request), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>Stops new official starts without deleting revisions or existing executions.</summary>
    [HttpPost("{id:guid}/revisions/unpublish")]
    public async Task<IActionResult> UnpublishAssessmentRevision(
        Guid id,
        [FromBody] UnpublishAssessmentRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Publish).ConfigureAwait(false))
        {
            return Forbid();
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _sender
            .Send(new UnpublishAssessmentRevisionEndpointCommand(id, actorId.Value, request), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    // ===== VERSIONED GRADING RUNTIME =====

    [HttpPost("{id:guid}/test-runs")]
    public async Task<ActionResult<AssessmentTestRunViewV1>> StartAssessmentTestRun(
        Guid id,
        [FromBody] StartAssessmentTestRunRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        return await ExecuteRuntimeAsync(() => _sender.Send(
            new StartAssessmentTestRunEndpointCommand(
                id,
                actorId.Value,
                new StartAssessmentTestRunCommand(
                    request.RevisionId,
                    request.PersonaKey,
                    request.PersonaDisplayName,
                    request.IdempotencyKey)),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpGet("test-runs/{testRunId:guid}")]
    public async Task<ActionResult<AssessmentTestRunViewV1>> GetAssessmentTestRun(
        Guid testRunId,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        return await ExecuteRuntimeAsync(async () =>
        {
            var run = await RequireRuntime().GetTestRunAsync(testRunId, actorId.Value, cancellationToken).ConfigureAwait(false);
            await RequireTestRunManagementPermissionAsync(run).ConfigureAwait(false);
            return run;
        }).ConfigureAwait(false);
    }

    [HttpPost("test-runs/{testRunId:guid}/submit")]
    public async Task<ActionResult<AssessmentTestRunViewV1>> SubmitAssessmentTestRun(
        Guid testRunId,
        [FromBody] SubmitAssessmentRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        return await ExecuteRuntimeAsync(async () =>
        {
            var run = await RequireRuntime().GetTestRunAsync(testRunId, actorId.Value, cancellationToken).ConfigureAwait(false);
            await RequireTestRunManagementPermissionAsync(run).ConfigureAwait(false);
            return await _sender.Send(
                new SubmitAssessmentTestRunEndpointCommand(
                    testRunId,
                    actorId.Value,
                    new SubmitAssessmentResponseCommand(request.Response, request.IdempotencyKey)),
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [HttpPost("test-runs/{testRunId:guid}/instructor-review")]
    public async Task<ActionResult<AssessmentTestRunViewV1>> ResolveAssessmentTestRun(
        Guid testRunId,
        [FromBody] ResolveInstructorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        return await ExecuteRuntimeAsync(async () =>
        {
            var run = await RequireRuntime().GetTestRunAsync(testRunId, actorId.Value, cancellationToken).ConfigureAwait(false);
            await RequireTestRunManagementPermissionAsync(run).ConfigureAwait(false);
            return await _sender.Send(
                new ResolveAssessmentTestRunEndpointCommand(
                    testRunId,
                    actorId.Value,
                    request.Resolution,
                    request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [HttpPost("test-runs/{testRunId:guid}/restart")]
    public async Task<ActionResult<AssessmentTestRunViewV1>> RestartAssessmentTestRun(
        Guid testRunId,
        [FromBody] IdempotentRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        return await ExecuteRuntimeAsync(async () =>
        {
            var run = await RequireRuntime().GetTestRunAsync(testRunId, actorId.Value, cancellationToken).ConfigureAwait(false);
            await RequireTestRunManagementPermissionAsync(run).ConfigureAwait(false);
            return await _sender.Send(
                new RestartAssessmentTestRunEndpointCommand(
                    testRunId,
                    actorId.Value,
                    request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [HttpPost("{id:guid}/runtime-submissions/individual")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> StartIndividualRuntimeSubmission(
        Guid id,
        [FromBody] StartIndividualRuntimeSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        var userId = await ResolveEnrollmentUserIdAsync(
            assessment.CourseId,
            request.EnrollmentId,
            actorId.Value,
            canManage: false).ConfigureAwait(false);
        if (!userId.HasValue || userId.Value != actorId.Value ||
            !await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }
        return await ExecuteRuntimeAsync(() => _sender.Send(
            new StartIndividualRuntimeSubmissionEndpointCommand(
                id,
                new StartIndividualSubmissionCommand(
                    request.EnrollmentId,
                    actorId.Value,
                    actorId.Value,
                    request.IdempotencyKey)),
            cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts or resumes the current learner's official attempt for graded content.
    /// The server resolves both the linked assessment and the active course membership.
    /// </summary>
    [HttpPost("content/{contentId:guid}/runtime-submissions/individual")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> StartIndividualRuntimeSubmissionForContent(
        Guid contentId,
        [FromBody] StartContentRuntimeSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentByContentIdAsync(contentId).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        var enrollmentId = await ResolveActorMembershipIdAsync(assessment.CourseId, actorId.Value).ConfigureAwait(false);
        if (!enrollmentId.HasValue ||
            !await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }

        return await ExecuteRuntimeAsync(() => _sender.Send(
            new StartIndividualRuntimeSubmissionEndpointCommand(
                assessment.Id,
                new StartIndividualSubmissionCommand(
                    enrollmentId.Value,
                    actorId.Value,
                    actorId.Value,
                    request.IdempotencyKey)),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("{id:guid}/runtime-submissions/collective")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> StartCollectiveRuntimeSubmission(
        Guid id,
        [FromBody] StartCollectiveRuntimeSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }
        return await ExecuteRuntimeAsync(() => _sender.Send(
            new StartCollectiveRuntimeSubmissionEndpointCommand(
                id,
                new StartCollectiveSubmissionCommand(
                    request.CourseGroupId,
                    actorId.Value,
                    request.IdempotencyKey)),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpGet("runtime-submissions/{submissionId:guid}")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> GetRuntimeSubmission(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission is null)
        {
            return NotFound();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        var capabilities = await _courseAccessEvaluator
            .GetCapabilitiesAsync(assessment.CourseId, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        if (!capabilities.CanReviewAsStaff && !capabilities.CanLearn)
        {
            return Forbid();
        }

        var instructorView = capabilities.CanReviewAsStaff;
        return await ExecuteRuntimeAsync(() => RequireRuntime().GetSubmissionAsync(
            submissionId,
            actorId.Value,
            instructorView,
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPut("runtime-submissions/{submissionId:guid}/draft")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> SaveCollectiveRuntimeDraft(
        Guid submissionId,
        [FromBody] SaveCollectiveRuntimeDraftRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var permission = await RequireSubmissionLearnPermissionAsync(submissionId).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        return await ExecuteRuntimeAsync(() => _sender.Send(
            new SaveCollectiveRuntimeDraftEndpointCommand(
                submissionId,
                actorId.Value,
                new SaveCollectiveAssessmentDraftCommand(
                    request.Response,
                    request.ExpectedVersion,
                    request.IdempotencyKey)),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("runtime-submissions/{submissionId:guid}/submit")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> SubmitRuntimeSubmission(
        Guid submissionId,
        [FromBody] SubmitAssessmentRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var permission = await RequireSubmissionLearnPermissionAsync(submissionId).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        return await ExecuteRuntimeAsync(() => _sender.Send(
            new SubmitRuntimeSubmissionEndpointCommand(
                submissionId,
                actorId.Value,
                new SubmitAssessmentResponseCommand(
                    request.Response,
                    request.IdempotencyKey,
                    request.ExpectedDraftVersion)),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("runtime-submissions/{submissionId:guid}/instructor-review")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> ResolveRuntimeInstructorReview(
        Guid submissionId,
        [FromBody] ResolveInstructorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var permission = await RequireSubmissionReviewPermissionAsync(submissionId).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid!.Value;
        return await ExecuteRuntimeAsync(() => _sender.Send(
            new ResolveRuntimeInstructorReviewEndpointCommand(
                submissionId,
                actorId,
                request.Resolution,
                request.IdempotencyKey),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("runtime-submissions/{submissionId:guid}/regrade")]
    public async Task<ActionResult<AssessmentSubmissionViewV1>> RegradeRuntimeSubmission(
        Guid submissionId,
        [FromBody] RegradeExecutionCommand request,
        CancellationToken cancellationToken)
    {
        var permission = await RequireSubmissionReviewPermissionAsync(submissionId).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid!.Value;
        return await ExecuteRuntimeAsync(() => _sender.Send(
            new RegradeRuntimeSubmissionEndpointCommand(submissionId, actorId, request),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("runtime-submissions/{submissionId:guid}/release")]
    public async Task<ActionResult<GradeResultReleaseResponse>> ReleaseRuntimeSubmission(
        Guid submissionId,
        [FromBody] ReleaseGradeResultCommand request,
        CancellationToken cancellationToken)
    {
        var permission = await RequireSubmissionReviewPermissionAsync(submissionId).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid!.Value;
        return await ExecuteRuntimeAsync(() => _sender.Send(
            new ReleaseRuntimeSubmissionEndpointCommand(submissionId, actorId, request),
            cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a new assessment for a course
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssessmentDto>> CreateAssessment([FromBody] CreateAssessmentRequest request)
    {
        var program = await _programService.GetProgramByIdAsync(request.CourseId).ConfigureAwait(false);
        if (program == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(program.Id).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new CreateAssessmentEndpointCommand(request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetAssessment), new { id = result.Value.Id }, AssessmentDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Get an assessment by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentDto>> GetAssessment(Guid id)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanEditOrReviewCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        return Ok(AssessmentDto.FromEntity(assessment));
    }

    /// <summary>
    /// Get all assessments for a course
    /// </summary>
    [HttpGet("course/{courseId:guid}")]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetCourseAssessments(Guid courseId)
    {
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program is null)
        {
            return NotFound();
        }

        if (!await CanEditOrReviewCourseAsync(courseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var assessments = await _assessmentService.GetCourseAssessmentsAsync(courseId).ConfigureAwait(false);
        return Ok(assessments.Select(AssessmentDto.FromEntity));
    }

    /// <summary>
    /// Get weighted assessment groups for a course.
    /// </summary>
    [HttpGet("course/{courseId:guid}/groups")]
    public async Task<ActionResult<IEnumerable<AssessmentGroupDto>>> GetCourseAssessmentGroups(Guid courseId)
    {
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null)
        {
            return NotFound();
        }

        if (!await CanEditOrReviewCourseAsync(program.Id).ConfigureAwait(false))
        {
            return Forbid();
        }

        var groups = await _assessmentService.GetCourseAssessmentGroupsAsync(courseId).ConfigureAwait(false);
        return Ok(groups.Select(AssessmentGroupDto.FromEntity));
    }

    /// <summary>
    /// Get assessment score distribution and weighted group performance for a course.
    /// </summary>
    [HttpGet("course/{courseId:guid}/analytics")]
    public async Task<ActionResult<CourseAssessmentAnalyticsDto>> GetCourseAssessmentAnalytics(Guid courseId)
    {
        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program == null)
        {
            return NotFound();
        }

        if (!await CanEditOrReviewCourseAsync(program.Id).ConfigureAwait(false))
        {
            return Forbid();
        }

        var analytics = await _assessmentService.GetCourseAssessmentAnalyticsAsync(courseId).ConfigureAwait(false);
        return Ok(analytics);
    }

    /// <summary>Returns the single canonical gradebook projection for one course enrollment.</summary>
    [HttpGet("course/{courseId:guid}/gradebook/{enrollmentId:guid}")]
    public async Task<ActionResult<GradebookCourseProjectionV1>> GetCourseGradebookProjection(
        Guid courseId,
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var program = await _programService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        if (program is null)
        {
            return NotFound();
        }

        var instructorView = await CanReviewCourseAsync(courseId).ConfigureAwait(false);
        if (!instructorView)
        {
            var enrollmentUserId = await ResolveEnrollmentUserIdAsync(courseId, enrollmentId, actorId.Value, canManage: false)
                .ConfigureAwait(false);
            if (enrollmentUserId != actorId.Value)
            {
                return Forbid();
            }
        }

        return Ok(await RequireGradebookProjection().GetCourseProjectionAsync(
            courseId,
            enrollmentId,
            learnerView: !instructorView,
            cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Create a weighted assessment group.
    /// </summary>
    [HttpPost("groups")]
    public async Task<ActionResult<AssessmentGroupDto>> CreateAssessmentGroup([FromBody] CreateAssessmentGroupRequest request)
    {
        var program = await _programService.GetProgramByIdAsync(request.CourseId).ConfigureAwait(false);
        if (program == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(program.Id).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new CreateAssessmentGroupEndpointCommand(request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetCourseAssessmentGroups),
            new { courseId = result.Value.CourseId },
            AssessmentGroupDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Update a weighted assessment group.
    /// </summary>
    [HttpPut("groups/{id:guid}")]
    public async Task<ActionResult<AssessmentGroupDto>> UpdateAssessmentGroup(Guid id, [FromBody] UpdateAssessmentGroupRequest request)
    {
        var group = await _assessmentService.GetAssessmentGroupByIdAsync(id).ConfigureAwait(false);
        if (group == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(group.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new UpdateAssessmentGroupEndpointCommand(id, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(AssessmentGroupDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Delete a weighted assessment group.
    /// </summary>
    [HttpDelete("groups/{id:guid}")]
    public async Task<ActionResult> DeleteAssessmentGroup(Guid id)
    {
        var group = await _assessmentService.GetAssessmentGroupByIdAsync(id).ConfigureAwait(false);
        if (group == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(group.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new DeleteAssessmentGroupEndpointCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Update an assessment
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssessmentDto>> UpdateAssessment(Guid id, [FromBody] UpdateAssessmentRequest request)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new UpdateAssessmentEndpointCommand(id, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(result.Error),
                ErrorType.Conflict => Conflict(result.Error),
                _ => BadRequest(result.Error),
            };
        }

        return Ok(AssessmentDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Assign an assessment to a weighted group or clear the assignment.
    /// </summary>
    [HttpPut("{id:guid}/group")]
    public async Task<ActionResult<AssessmentDto>> AssignAssessmentToGroup(Guid id, [FromBody] AssignAssessmentGroupRequest request)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new AssignAssessmentToGroupEndpointCommand(id, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(AssessmentDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Links this assessment to a cue in an interactive-video lesson.
    /// </summary>
    [HttpPost("{id:guid}/interactive-video-cues")]
    public async Task<ActionResult<InteractiveVideoAssessmentCueDto>> LinkInteractiveVideoCue(
        Guid id,
        [FromBody] LinkInteractiveVideoCueRequest request)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new LinkInteractiveVideoCueEndpointCommand(id, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(InteractiveVideoAssessmentCueDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Gets the interactive-video cue links for this assessment.
    /// </summary>
    [HttpGet("{id:guid}/interactive-video-cues")]
    public async Task<ActionResult<IEnumerable<InteractiveVideoAssessmentCueDto>>> GetInteractiveVideoCues(Guid id)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var cues = await _assessmentService.GetInteractiveVideoCuesAsync(id).ConfigureAwait(false);
        return Ok(cues.Select(InteractiveVideoAssessmentCueDto.FromEntity));
    }

    /// <summary>
    /// Removes a manager-configured interactive-video cue link.
    /// </summary>
    [HttpDelete("{id:guid}/interactive-video-cues/{cueId:guid}")]
    public async Task<ActionResult> UnlinkInteractiveVideoCue(Guid id, Guid cueId)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new UnlinkInteractiveVideoCueEndpointCommand(id, cueId)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound ? NotFound(result.Error) : BadRequest(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Gets delivery-safe active cues for an enrolled learner and one video content item.
    /// </summary>
    [HttpGet("{assessmentId:guid}/interactive-video-cues/content/{contentId:guid}/enrollments/{enrollmentId:guid}")]
    public async Task<ActionResult<IEnumerable<LearnerInteractiveVideoAssessmentCueDto>>> GetLearnerInteractiveVideoCues(
        Guid assessmentId,
        Guid contentId,
        Guid enrollmentId)
    {
        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        var enrollment = await _enrollmentService.GetAsync(enrollmentId).ConfigureAwait(false);
        if (enrollment == null || enrollment.CourseId != assessment.CourseId)
        {
            return NotFound();
        }

        if (enrollment.UserId != actorUserId.Value)
        {
            return Forbid();
        }

        if (!await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }

        var cues = await _assessmentService
            .GetInteractiveVideoCuesForContentAsync(assessmentId, contentId)
            .ConfigureAwait(false);
        return Ok(cues.Select(LearnerInteractiveVideoAssessmentCueDto.FromEntity));
    }

    /// <summary>
    /// Delete an assessment
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAssessment(Guid id)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new DeleteAssessmentEndpointCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Restore a soft-deleted assessment
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult> RestoreAssessment(Guid id)
    {
        var assessment = await _assessmentService.GetAssessmentByIdIncludingDeletedAsync(id).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new RestoreAssessmentEndpointCommand(id)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    // ===== SUBMISSION MANAGEMENT =====

    /// <summary>
    /// Start a new assessment attempt
    /// </summary>
    [HttpPost("{assessmentId:guid}/submissions/start")]
    public async Task<ActionResult<LearnerAssessmentAttemptDto>> StartSubmission(
        Guid assessmentId,
        [FromBody] StartSubmissionRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        var enrollmentUserId = await ResolveEnrollmentUserIdAsync(
            assessment.CourseId,
            request.EnrollmentId,
            actor.SubjectIdAsGuid.Value,
            canManage: false).ConfigureAwait(false);
        if (!enrollmentUserId.HasValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid course membership",
                Detail = "The assessment attempt could not be matched to an active course enrollment."
            });
        }
        if (enrollmentUserId.Value != actor.SubjectIdAsGuid.Value ||
            !await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }

        _logger.LogInformation(
            "Starting assessment {AssessmentId} for enrollment {EnrollmentId} and user {UserId}",
            assessmentId,
            request.EnrollmentId,
            enrollmentUserId.Value);
        var result = await _sender.Send(new StartAssessmentSubmissionEndpointCommand(
            assessmentId,
            request.EnrollmentId,
            enrollmentUserId.Value)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Assessment {AssessmentId} start was rejected for enrollment {EnrollmentId}: {ErrorCode} {ErrorDescription}",
                assessmentId,
                request.EnrollmentId,
                result.Error.Code,
                result.Error.Description);
            return BadRequest(new ProblemDetails
            {
                Title = "Assessment attempt rejected",
                Detail = result.Error.Description
            });
        }

        return CreatedAtAction(
            nameof(GetSubmission),
            new { submissionId = result.Value.Id },
            LearnerAssessmentAttemptDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Submit a completed assessment
    /// </summary>
    [HttpPost("submissions/{submissionId:guid}/submit")]
    public async Task<ActionResult<LearnerAssessmentSubmissionDto>> SubmitAssessment(
        Guid submissionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SubmitAssessmentRequest? request = null)
    {
        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission == null)
        {
            return NotFound();
        }

        if (submission.UserId != actorUserId.Value)
        {
            return Forbid();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender.Send(new SubmitAssessmentEndpointCommand(submissionId, request)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(LearnerAssessmentSubmissionDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Get a submission by ID
    /// </summary>
    [HttpGet("submissions/{submissionId:guid}")]
    public async Task<ActionResult<object>> GetSubmission(Guid submissionId)
    {
        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission == null)
        {
            return NotFound();
        }

        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (submission.DefinitionRevisionId.HasValue)
        {
            var capabilities = await _courseAccessEvaluator
                .GetCapabilitiesAsync(assessment.CourseId, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            if (!capabilities.CanReviewAsStaff && !capabilities.CanLearn)
            {
                return Forbid();
            }

            var instructorView = capabilities.CanReviewAsStaff;
            var runtimeView = await ExecuteRuntimeAsync(() => RequireRuntime().GetSubmissionAsync(
                submissionId,
                actorUserId.Value,
                instructorView,
                HttpContext.RequestAborted)).ConfigureAwait(false);
            if (runtimeView.Result is not OkObjectResult ok || ok.Value is not AssessmentSubmissionViewV1)
            {
                return runtimeView.Result ?? StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (instructorView)
            {
                return Ok(AssessmentSubmissionDto.FromEntity(submission));
            }

            var releasedResults = await RequireLearnerResultProjection()
                .GetLatestReleasedAsync([submission.Id], HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return Ok(LearnerAssessmentSubmissionDto.FromReleasedResult(
                submission,
                releasedResults.GetValueOrDefault(submission.Id)));
        }

        if (submission.UserId == actorUserId.Value)
        {
            if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(LearnerAssessmentSubmissionDto.FromEntity(submission));
        }

        if (!await CanReviewCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        return Ok(AssessmentSubmissionDto.FromEntity(submission));
    }

    /// <summary>
    /// Get all submissions for an assessment
    /// </summary>
    [HttpGet("{assessmentId:guid}/submissions")]
    public async Task<ActionResult<IEnumerable<AssessmentSubmissionDto>>> GetAssessmentSubmissions(Guid assessmentId)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanReviewCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var submissions = await _assessmentService.GetAssessmentSubmissionsAsync(assessmentId).ConfigureAwait(false);
        return Ok(submissions.Select(AssessmentSubmissionDto.FromEntity));
    }

    /// <summary>
    /// Get my submissions for an enrollment
    /// </summary>
    [HttpGet("my-submissions/{enrollmentId:guid}")]
    public async Task<ActionResult<IEnumerable<LearnerAssessmentSubmissionDto>>> GetMySubmissions(Guid enrollmentId)
    {
        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var submissions = await _assessmentService.GetUserSubmissionsAsync(enrollmentId, actorUserId.Value).ConfigureAwait(false);
        var assessmentVisibility = new Dictionary<Guid, bool>();
        var visibleSubmissions = new List<LearnerAssessmentSubmissionDto>();
        var runtimeSubmissionIds = submissions
            .Where(value => value.DefinitionRevisionId.HasValue)
            .Select(value => value.Id)
            .ToArray();
        IReadOnlyDictionary<Guid, LearnerReleasedAssessmentResultV1> releasedResults =
            runtimeSubmissionIds.Length == 0
                ? new Dictionary<Guid, LearnerReleasedAssessmentResultV1>()
                : await RequireLearnerResultProjection()
                    .GetLatestReleasedAsync(runtimeSubmissionIds, HttpContext.RequestAborted)
                    .ConfigureAwait(false);
        foreach (var submission in submissions)
        {
            if (!assessmentVisibility.TryGetValue(submission.AssessmentId, out var isVisible))
            {
                var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
                isVisible = assessment != null &&
                            await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false);
                assessmentVisibility[submission.AssessmentId] = isVisible;
            }

            if (!isVisible)
            {
                continue;
            }

            if (!submission.DefinitionRevisionId.HasValue)
            {
                visibleSubmissions.Add(LearnerAssessmentSubmissionDto.FromEntity(submission));
                continue;
            }

            await RequireRuntime().GetSubmissionAsync(
                submission.Id,
                actorUserId.Value,
                false,
                HttpContext.RequestAborted).ConfigureAwait(false);
            visibleSubmissions.Add(LearnerAssessmentSubmissionDto.FromReleasedResult(
                submission,
                releasedResults.GetValueOrDefault(submission.Id)));
        }

        return Ok(visibleSubmissions);
    }

    /// <summary>
    /// Check if user can attempt an assessment
    /// </summary>
    [HttpGet("{assessmentId:guid}/can-attempt/{enrollmentId:guid}")]
    public async Task<ActionResult<CanAttemptResponse>> CanAttempt(Guid assessmentId, Guid enrollmentId)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        var actorUserId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var enrollmentUserId = await ResolveEnrollmentUserIdAsync(
            assessment.CourseId,
            enrollmentId,
            actorUserId.Value,
            canManage: false).ConfigureAwait(false);
        if (!enrollmentUserId.HasValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid course membership",
                Detail = "The assessment attempt could not be matched to an active course enrollment."
            });
        }
        if (enrollmentUserId.Value != actorUserId.Value ||
            !await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _assessmentService.CanAttemptAsync(assessmentId, enrollmentId).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var attemptCount = await _assessmentService.GetAttemptCountAsync(assessmentId, enrollmentId).ConfigureAwait(false);
        return Ok(new CanAttemptResponse(result.Value, attemptCount));
    }

    /// <summary>
    /// Get the SpeedGrader navigation queue for an assessment (instructor-only):
    /// one item per student/group representing the target's latest gradeable attempt,
    /// excluding InProgress-only targets.
    /// </summary>
    [HttpGet("{assessmentId:guid}/grading-queue")]
    public async Task<ActionResult<GradingQueueDto>> GetGradingQueue(Guid assessmentId)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanReviewCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _gradingQueueService.GetQueueAsync(assessmentId).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    private async Task<Guid?> ResolveEnrollmentUserIdAsync(
        Guid courseId,
        Guid enrollmentId,
        Guid actorUserId,
        bool canManage)
    {
        var enrollment = await _enrollmentService.GetAsync(enrollmentId).ConfigureAwait(false);
        if (enrollment is { CourseId: var enrollmentCourseId } && enrollmentCourseId == courseId)
        {
            _logger.LogDebug("Resolved assessment enrollment {EnrollmentId} through Learning.Enrollments", enrollmentId);
            return enrollment.UserId;
        }

        var canonicalOwner = await _assessmentService
            .GetProgramEnrollmentUserIdAsync(courseId, enrollmentId, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        if (canonicalOwner.HasValue)
        {
            _logger.LogDebug("Resolved assessment enrollment {EnrollmentId} through ProgramEnrollment", enrollmentId);
            return canonicalOwner.Value;
        }

        var actorProgress = await _programService.GetUserProgressDtoAsync(courseId, actorUserId).ConfigureAwait(false);
        if (actorProgress?.EnrollmentId == enrollmentId)
        {
            _logger.LogDebug("Resolved assessment enrollment {EnrollmentId} through ProgramUser", enrollmentId);
            return actorProgress.UserId;
        }

        if (!canManage)
        {
            _logger.LogWarning(
                "Assessment membership {EnrollmentId} could not be resolved for actor {ActorUserId} in course {CourseId}; actor ProgramUser is {ActorProgramUserId}",
                enrollmentId,
                actorUserId,
                courseId,
                actorProgress?.EnrollmentId);
            return null;
        }

        var courseUsers = await _programService.GetProgramUsersAsync(courseId, 0, 1000).ConfigureAwait(false);
        var managedProgress = courseUsers.FirstOrDefault(progress => progress.EnrollmentId == enrollmentId);
        if (managedProgress == null)
        {
            _logger.LogWarning("Managed assessment membership {EnrollmentId} was not found in course {CourseId}", enrollmentId, courseId);
        }
        return managedProgress?.UserId;
    }

    private async Task<Guid?> ResolveActorMembershipIdAsync(Guid courseId, Guid actorUserId)
    {
        var enrollments = await _enrollmentService
            .GetUserEnrollmentsAsync(actorUserId, GameGuild.Learning.Enrollments.EnrollmentStatus.Active)
            .ConfigureAwait(false);
        var enrollment = enrollments.FirstOrDefault(value => value.CourseId == courseId);
        if (enrollment is not null)
        {
            return enrollment.Id;
        }

        var canonicalId = await _assessmentService
            .GetProgramEnrollmentIdAsync(courseId, actorUserId, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        if (canonicalId.HasValue)
        {
            return canonicalId.Value;
        }

        var progress = await _programService.GetUserProgressDtoAsync(courseId, actorUserId).ConfigureAwait(false);
        return progress?.EnrollmentId;
    }

    private Task<bool> CanManageCourseAsync(Guid courseId) =>
        HasCourseCapabilityAsync(courseId, CourseCapability.Edit);

    private Task<bool> IsActorInProgramTenantAsync(Guid courseId) =>
        HasCourseCapabilityAsync(courseId, CourseCapability.Learn);

    private Task<bool> CanReviewCourseAsync(Guid courseId) =>
        HasCourseCapabilityAsync(courseId, CourseCapability.StaffReview);

    private async Task<bool> CanEditOrReviewCourseAsync(Guid courseId)
    {
        var capabilities = await _courseAccessEvaluator
            .GetCapabilitiesAsync(courseId, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        return capabilities.CanEdit || capabilities.CanReviewAsStaff;
    }

    private Task<bool> HasCourseCapabilityAsync(Guid courseId, CourseCapability capability) =>
        _courseAccessEvaluator.HasCapabilityAsync(courseId, capability, HttpContext.RequestAborted);

    private IAssessmentGradingRuntimeService RequireRuntime() =>
        _gradingRuntime ?? throw new InvalidOperationException("The grading runtime is not registered.");

    private IAssessmentGradebookProjectionService RequireGradebookProjection() =>
        _gradebookProjection ?? throw new InvalidOperationException("The gradebook projection service is not registered.");

    private IAssessmentLearnerResultProjectionService RequireLearnerResultProjection() =>
        _learnerResultProjection ?? throw new InvalidOperationException("The learner result projection service is not registered.");

    private async Task RequireTestRunManagementPermissionAsync(AssessmentTestRunViewV1 run)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(run.AssessmentId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Assessment was not found.");
        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Actor cannot manage this assessment test run.");
        }
    }

    private async Task<ActionResult?> RequireSubmissionReviewPermissionAsync(Guid submissionId)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission is null)
        {
            return NotFound();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        return await CanReviewCourseAsync(assessment.CourseId).ConfigureAwait(false) ? null : Forbid();
    }

    private async Task<ActionResult?> RequireSubmissionLearnPermissionAsync(Guid submissionId)
    {
        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission is null)
        {
            return NotFound();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        return await HasCourseCapabilityAsync(assessment.CourseId, CourseCapability.Learn).ConfigureAwait(false)
            ? null
            : Forbid();
    }

    private async Task<ActionResult<T>> ExecuteRuntimeAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action().ConfigureAwait(false));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = exception.Message, Status = StatusCodes.Status404NotFound });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = exception.Message, Status = StatusCodes.Status403Forbidden });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Concurrent grading runtime command failed");
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = "The grading resource changed concurrently. Retry with the same idempotency key.",
                Status = StatusCodes.Status409Conflict,
            });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException)
        {
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

}

// ===== DTOs =====

public sealed record AssessmentDto(
    Guid Id,
    Guid CourseId,
    Guid? ContentId,
    string Title,
    string Slug,
    string? Description,
    AssessmentType Type,
    ScoreValue MaxScore,
    ScoreValue PassingScore,
    int? TimeLimitMinutes,
    int MaxAttempts,
    bool IsRequired,
    int Order,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    Guid? AssessmentGroupId,
    string? AssessmentGroupName,
    PercentValue? AssessmentGroupWeightPercent,
    int? AssessmentGroupOrder,
    bool IsAvailable,
    SubmissionModality SubmissionModalities = SubmissionModality.Text,
    AssessmentPresentationMode PresentationMode = AssessmentPresentationMode.SingleStep,
    DateTime? DueAt = null,
    bool AllowLateSubmissions = false,
    DateTime? LateSubmissionDeadline = null,
    ReviewMethods ReviewMethods = ReviewMethods.InstructorReview,
    Guid? GroupSetId = null,
    Guid? PublishedDefinitionRevisionId = null,
    string? ReviewConfigurationCanonicalJson = null,
    AttemptContributionMode? AttemptContributionMode = null,
    ContentCompletionMode ContentCompletionMode = ContentCompletionMode.OnReleaseAndPass,
    ResultReleaseMode ResultReleaseMode = ResultReleaseMode.Manual,
    DateTime? ResultReleaseScheduledFor = null,
    int Version = 0)
{
    public static AssessmentDto FromEntity(Assessment entity) => new(
        entity.Id,
        entity.CourseId,
        entity.ContentId,
        entity.Title,
        entity.Slug,
        entity.Description,
        entity.Type,
        entity.MaxScore,
        entity.PassingScore,
        entity.TimeLimitMinutes,
        entity.MaxAttempts,
        entity.IsRequired,
        entity.Order,
        entity.AvailableFrom,
        entity.AvailableUntil,
        entity.AssessmentGroupId,
        entity.AssessmentGroup?.Name,
        entity.AssessmentGroup?.WeightPercent,
        entity.AssessmentGroup?.Order,
        entity.IsAvailable(),
        entity.SubmissionModalities,
        entity.PresentationMode,
        entity.DueAt,
        entity.AllowLateSubmissions,
        entity.LateSubmissionDeadline,
        entity.ReviewMethods,
        entity.GroupSetId,
        entity.PublishedDefinitionRevisionId,
        entity.ReviewConfigurationCanonicalJson,
        entity.AttemptContributionMode,
        entity.ContentCompletionMode,
        entity.ResultReleaseMode,
        entity.ResultReleaseScheduledFor,
        entity.Version);
}

public sealed record InteractiveVideoAssessmentCueDto(
    Guid Id,
    Guid AssessmentId,
    Guid ContentId,
    string CueId,
    decimal? CuePositionSeconds)
{
    public static InteractiveVideoAssessmentCueDto FromEntity(InteractiveVideoAssessmentCue entity) => new(
        entity.Id,
        entity.AssessmentId,
        entity.ContentId,
        entity.CueId,
        entity.CuePositionSeconds);
}

public sealed record LearnerInteractiveVideoAssessmentCueDto(
    string CueId,
    decimal? CuePositionSeconds)
{
    public static LearnerInteractiveVideoAssessmentCueDto FromEntity(InteractiveVideoAssessmentCue entity) => new(
        entity.CueId,
        entity.CuePositionSeconds);
}

public sealed record AssessmentGroupDto(
    Guid Id,
    Guid CourseId,
    string Name,
    string? Description,
    PercentValue WeightPercent,
    int Order)
{
    public static AssessmentGroupDto FromEntity(AssessmentGroup entity) => new(
        entity.Id,
        entity.CourseId,
        entity.Name,
        entity.Description,
        entity.WeightPercent,
        entity.Order);
}

public sealed record AssessmentSubmissionDto(
    Guid Id,
    Guid AssessmentId,
    Guid? EnrollmentId,
    Guid? UserId,
    int AttemptNumber,
    ScoreValue? Score,
    bool? Passed,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    DateTime? GradedAt,
    Guid? GradedBy,
    string? Feedback,
    SubmissionStatus Status,
    bool IsLate = false,
    SubmissionModality SubmittedModalities = SubmissionModality.None,
    string? TextPayload = null,
    string? FilePayload = null,
    string? UrlPayload = null,
    string? CodePayload = null,
    string? MediaPayload = null,
    string? ProjectPayload = null)
{
    public static AssessmentSubmissionDto FromEntity(AssessmentSubmission entity) => new(
        entity.Id,
        entity.AssessmentId,
        entity.EnrollmentId,
        entity.UserId,
        entity.AttemptNumber,
        entity.Score,
        entity.Passed,
        entity.StartedAt,
        entity.SubmittedAt,
        entity.GradedAt,
        entity.GradedBy,
        entity.Feedback,
        entity.Status,
        entity.IsLate,
        entity.SubmittedModalities,
        entity.TextPayload,
        entity.FilePayload,
        entity.UrlPayload,
        entity.CodePayload,
        entity.MediaPayload,
        entity.ProjectPayload);
}

public sealed record LearnerAssessmentSubmissionDto(
    Guid Id,
    Guid AssessmentId,
    Guid? EnrollmentId,
    int AttemptNumber,
    ScoreValue? Score,
    bool? Passed,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    DateTime? GradedAt,
    string? Feedback,
    SubmissionStatus Status,
    bool IsLate,
    SubmissionModality SubmittedModalities,
    string? TextPayload,
    string? FilePayload,
    string? UrlPayload,
    string? CodePayload,
    string? MediaPayload,
    string? ProjectPayload)
{
    public static LearnerAssessmentSubmissionDto FromEntity(
        AssessmentSubmission entity,
        bool revealAcademicResult = true) => new(
        entity.Id, entity.AssessmentId, entity.EnrollmentId, entity.AttemptNumber,
        revealAcademicResult ? entity.Score : null,
        revealAcademicResult ? entity.Passed : null,
        entity.StartedAt,
        entity.SubmittedAt,
        revealAcademicResult ? entity.GradedAt : null,
        revealAcademicResult ? entity.Feedback : null,
        entity.Status, entity.IsLate, entity.SubmittedModalities,
        entity.TextPayload, entity.FilePayload, entity.UrlPayload, entity.CodePayload,
        entity.MediaPayload, entity.ProjectPayload);

    public static LearnerAssessmentSubmissionDto FromReleasedResult(
        AssessmentSubmission entity,
        LearnerReleasedAssessmentResultV1? releasedResult) => new(
        entity.Id, entity.AssessmentId, entity.EnrollmentId, entity.AttemptNumber,
        releasedResult?.Score,
        releasedResult?.Passed,
        entity.StartedAt,
        entity.SubmittedAt,
        releasedResult?.FinalizedAt,
        releasedResult?.Feedback,
        entity.Status, entity.IsLate, entity.SubmittedModalities,
        entity.TextPayload, entity.FilePayload, entity.UrlPayload, entity.CodePayload,
        entity.MediaPayload, entity.ProjectPayload);
}

public sealed record LearnerAssessmentAttemptDto(
    LearnerAssessmentSubmissionDto Submission)
{
    public static LearnerAssessmentAttemptDto FromEntity(AssessmentSubmission submission) => new(
        LearnerAssessmentSubmissionDto.FromEntity(submission));
}

public sealed record StartSubmissionRequest(Guid EnrollmentId);

public sealed record StartAssessmentTestRunRequest(
    Guid RevisionId,
    string PersonaKey,
    string PersonaDisplayName,
    string IdempotencyKey);

public sealed record IdempotentRuntimeRequest(string IdempotencyKey);

public sealed record StartIndividualRuntimeSubmissionRequest(Guid EnrollmentId, string IdempotencyKey);

public sealed record StartContentRuntimeSubmissionRequest(string IdempotencyKey);

public sealed record StartCollectiveRuntimeSubmissionRequest(Guid CourseGroupId, string IdempotencyKey);

public sealed record SubmitAssessmentRuntimeRequest(
    AssessmentResponseEnvelopeV1 Response,
    string IdempotencyKey,
    long? ExpectedDraftVersion = null);

public sealed record SaveCollectiveRuntimeDraftRequest(
    AssessmentResponseEnvelopeV1 Response,
    long ExpectedVersion,
    string IdempotencyKey);

public sealed record ResolveInstructorReviewRequest(
    InstructorReviewResolutionV1 Resolution,
    string IdempotencyKey);

public sealed record GradeResultReleaseResponse(Guid ReleaseId, Guid GradeRoundId, DateTime ReleasedAt);

public sealed record CanAttemptResponse(bool CanAttempt, int CurrentAttemptCount);
