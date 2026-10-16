using System.Text.Json.Serialization;
using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Peer review endpoints. Student-facing responses are anonymous by construction: reviewers
/// never see the reviewee's identity or grades, reviewees never see the reviewer's identity.
/// Only the instructor endpoint (<see cref="GetPeerReviews"/>) carries names.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/assessments")]
[Authorize]
public class PeerReviewsController : BaseApiController
{
    private readonly IPeerReviewAssignmentService _peerReviewService;
    private readonly IAssessmentService _assessmentService;
    private readonly IRubricService _rubricService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly ICourseAccessEvaluator _courseAccessEvaluator;
    private readonly ILogger<PeerReviewsController> _logger;
    private readonly ISender _sender;

    public PeerReviewsController(
        IPeerReviewAssignmentService peerReviewService,
        IAssessmentService assessmentService,
        IRubricService rubricService,
        IActorContextAccessor actorContextAccessor,
        ICourseAccessEvaluator courseAccessEvaluator,
        ILogger<PeerReviewsController> logger,
        ISender sender)
    {
        _peerReviewService = peerReviewService;
        _assessmentService = assessmentService;
        _rubricService = rubricService;
        _actorContextAccessor = actorContextAccessor;
        _courseAccessEvaluator = courseAccessEvaluator;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// Claim the next peer review: a random submission among those tied for the fewest existing reviews.
    /// </summary>
    [HttpPost("{assessmentId:guid}/peer-reviews/claim")]
    public async Task<ActionResult<PeerReviewClaimDto>> ClaimPeerReview(Guid assessmentId)
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

        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _sender
            .Send(new ClaimPeerReviewEndpointCommand(assessmentId, actor.SubjectIdAsGuid.Value))
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Peer review claim was rejected on assessment {AssessmentId}: {ErrorCode} {ErrorDescription}",
                assessmentId,
                result.Error.Code,
                result.Error.Description);
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(result.Error)
                : BadRequest(new ProblemDetails
                {
                    Title = "Peer review claim rejected",
                    Detail = result.Error.Description
                });
        }

        return Ok(new PeerReviewClaimDto(result.Value.ReviewId, result.Value.MaskedSubmission));
    }

    /// <summary>
    /// Get the anonymous submission a claimed review refers to. Reviewer-only.
    /// </summary>
    [HttpGet("peer-reviews/{reviewId:guid}")]
    public async Task<ActionResult<AnonymousReviewSubmissionDto>> GetReview(Guid reviewId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var review = await _peerReviewService.GetReviewAsync(reviewId).ConfigureAwait(false);
        if (review == null)
        {
            return NotFound();
        }

        if (review.ReviewerUserId != actor.SubjectIdAsGuid.Value)
        {
            return Forbid();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(review.AssessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        if (!review.SubmissionId.HasValue)
        {
            return NotFound();
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(review.SubmissionId.Value).ConfigureAwait(false);
        if (submission == null)
        {
            return NotFound();
        }

        var rubric = await _rubricService.GetAsync(assessment.Id).ConfigureAwait(false);
        return Ok(new AnonymousReviewSubmissionDto(
            review.Id,
            review.Status,
            new AnonymousReviewAssessmentDto(assessment.Id, assessment.Title, assessment.MaxScore),
            rubric.IsSuccess ? new AnonymousReviewRubricDto(rubric.Value.Criteria) : null,
            submission.AttemptNumber,
            submission.SubmittedAt,
            submission.Status,
            review.DeliveryCanonicalJson,
            review.ResponseEnvelopeCanonicalJson,
            submission.TextPayload,
            submission.UrlPayload,
            submission.CodePayload,
            submission.MediaPayload,
            submission.FilePayload,
            submission.ProjectPayload));
    }

    /// <summary>
    /// Submit evidence for the canonical peer-review stage assigned to the learner.
    /// </summary>
    [HttpPost("peer-reviews/{reviewId:guid}/submit")]
    public async Task<IActionResult> SubmitReview(Guid reviewId, [FromBody] PeerReviewSubmitRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var review = await _peerReviewService.GetReviewAsync(reviewId).ConfigureAwait(false);
        if (review == null)
        {
            return NotFound();
        }

        if (review.ReviewerUserId != actor.SubjectIdAsGuid.Value)
        {
            return Forbid();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(review.AssessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Feedback))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = "Feedback comment is required to complete a peer review"
            });
        }

        if (request.Score is not { } score || score.CompareTo(ScoreValue.Zero) < 0 || score.CompareTo(assessment.MaxScore) > 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = "Score must be between 0 and the assessment maximum"
            });
        }

        // Shared validator: rubric required + per-criterion bounds + sum == score when rubric-graded;
        // rubricScores must be absent when not.
        var rubricValidation = await _rubricService
            .ValidateScoresAsync(assessment.Id, score, request.RubricScores)
            .ConfigureAwait(false);
        if (!rubricValidation.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = rubricValidation.Error.Description
            });
        }

        var result = await _sender
            .Send(new SubmitPeerReviewEndpointCommand(
                review.Id,
                actor.SubjectIdAsGuid.Value,
                score,
                request.Feedback.Trim(),
                request.RubricScores))
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            if (result.Error.Type == ErrorType.Forbidden)
            {
                return Forbid();
            }

            var problem = new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = result.Error.Description
            };
            return result.Error.Type == ErrorType.Conflict
                ? Conflict(problem)
                : BadRequest(problem);
        }

        return Ok(new PeerReviewSubmitDto(result.Value.Id, result.Value.Status));
    }

    /// <summary>Extend an insufficient peer-review evidence window.</summary>
    [HttpPost("peer-review-stages/{reviewStageId:guid}/extend")]
    public async Task<IActionResult> ExtendEvidenceWindow(
        Guid reviewStageId,
        [FromBody] ExtendPeerReviewWindowRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var permission = await RequireStageStaffReviewAsync(reviewStageId, cancellationToken).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        return await ExecuteOperationAsync(
            () => _sender.Send(new ExtendPeerReviewWindowEndpointCommand(
                reviewStageId,
                actorId.Value,
                request.ExpectedStageVersion,
                request.DeadlineAt,
                request.Reason,
                request.IdempotencyKey), cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Cancel an active claim so another eligible reviewer can claim the stage.</summary>
    [HttpPost("peer-reviews/{reviewId:guid}/reassign")]
    public async Task<IActionResult> ReassignClaim(
        Guid reviewId,
        [FromBody] ReassignPeerReviewClaimRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var review = await _peerReviewService.GetReviewAsync(reviewId).ConfigureAwait(false);
        if (review is null)
        {
            return NotFound();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(review.AssessmentId).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        return await ExecuteOperationAsync(
            () => _sender.Send(new ReassignPeerReviewClaimEndpointCommand(
                reviewId,
                actorId.Value,
                request.ExpectedClaimVersion,
                request.Reason,
                request.IdempotencyKey), cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Resolve a peer stage whose evidence threshold was not reached before its deadline.</summary>
    [HttpPost("peer-review-stages/{reviewStageId:guid}/resolve-insufficient")]
    public async Task<IActionResult> ResolveInsufficientEvidence(
        Guid reviewStageId,
        [FromBody] ResolvePeerReviewInsufficiencyRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = _actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
        {
            return Unauthorized();
        }

        var permission = await RequireStageStaffReviewAsync(reviewStageId, cancellationToken).ConfigureAwait(false);
        if (permission is not null)
        {
            return permission;
        }

        return await ExecuteOperationAsync(
            () => _sender.Send(new ResolvePeerReviewInsufficiencyEndpointCommand(
                reviewStageId,
                actorId.Value,
                request.Resolution,
                request.IdempotencyKey), cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Reviews received on a single individual or collective submission.
    /// Owner-only, anonymized: no reviewer identity exists in the DTO at all.
    /// </summary>
    [HttpGet("submissions/{submissionId:guid}/received-peer-reviews")]
    public async Task<ActionResult<IEnumerable<ReceivedPeerReviewDto>>> GetReceivedPeerReviews(Guid submissionId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission == null)
        {
            return NotFound();
        }

        if (!await _peerReviewService
                .IsSubmissionOwnerOrParticipantAsync(submissionId, actor.SubjectIdAsGuid.Value)
                .ConfigureAwait(false))
        {
            return Forbid();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var reviews = await _peerReviewService.GetReviewsForSubmissionAsync(submissionId).ConfigureAwait(false);
        return Ok(reviews.Select(r => new ReceivedPeerReviewDto(
            r.ClaimId, r.Score, r.Feedback, r.RubricScores, r.SubmittedAt)));
    }

    /// <summary>
    /// Same reviews for instructors, with reviewer names. CanManageCourse-only.
    /// </summary>
    [HttpGet("submissions/{submissionId:guid}/peer-reviews")]
    public async Task<ActionResult<IEnumerable<InstructorPeerReviewDto>>> GetPeerReviews(Guid submissionId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission == null)
        {
            return NotFound();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return NotFound();
        }

        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false))
        {
            return Forbid();
        }

        var reviews = await _peerReviewService.GetReviewsForSubmissionAsync(submissionId).ConfigureAwait(false);
        var names = await _peerReviewService
            .GetReviewerDisplayNamesAsync(reviews.Select(r => r.ReviewerUserId).Distinct().ToList())
            .ConfigureAwait(false);
        return Ok(reviews.Select(r => new InstructorPeerReviewDto(
            r.ClaimId,
            r.Score,
            r.Feedback,
            r.RubricScores,
            r.SubmittedAt,
            r.ReviewerUserId,
            names.TryGetValue(r.ReviewerUserId, out var name) ? name : r.ReviewerUserId.ToString())));
    }

    private Task<bool> IsActorInProgramTenantAsync(Guid courseId) =>
        _courseAccessEvaluator.HasCapabilityAsync(
            courseId,
            CourseCapability.Learn,
            HttpContext.RequestAborted);

    private Task<bool> CanManageCourseAsync(Guid courseId) =>
        _courseAccessEvaluator.HasCapabilityAsync(
            courseId,
            CourseCapability.StaffReview,
            HttpContext.RequestAborted);

    private async Task<IActionResult?> RequireStageStaffReviewAsync(
        Guid reviewStageId,
        CancellationToken cancellationToken)
    {
        var assessmentId = await _peerReviewService
            .GetAssessmentIdForStageAsync(reviewStageId, cancellationToken)
            .ConfigureAwait(false);
        if (!assessmentId.HasValue)
        {
            return NotFound();
        }

        var assessment = await _assessmentService.GetAssessmentByIdAsync(assessmentId.Value).ConfigureAwait(false);
        if (assessment is null)
        {
            return NotFound();
        }

        return await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false) ? null : Forbid();
    }

    private static async Task<IActionResult> ExecuteOperationAsync(Func<Task<Result>> operation)
    {
        try
        {
            var result = await operation().ConfigureAwait(false);
            return result.IsSuccess
                ? new NoContentResult()
                : new BadRequestObjectResult(new ProblemDetails { Detail = result.Error.Description });
        }
        catch (KeyNotFoundException exception)
        {
            return new NotFoundObjectResult(new ProblemDetails { Detail = exception.Message });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return new ConflictObjectResult(new ProblemDetails { Detail = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return new ConflictObjectResult(new ProblemDetails { Detail = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return new BadRequestObjectResult(new ProblemDetails { Detail = exception.Message });
        }
    }
}

// ===== DTOs =====

/// <summary>
/// Claim response. Deliberately carries no reviewee identity (no userId, no group id/name)
/// and no submission id — only the review to work on and its masked descriptor.
/// </summary>
public sealed record PeerReviewClaimDto(Guid ReviewId, string MaskedSubmission);

/// <summary>
/// Body of a peer review submit: plain score XOR rubric scores (rubric rules enforced server-side),
/// plus the mandatory feedback comment.
/// </summary>
public sealed record PeerReviewSubmitRequest(ScoreValue? Score, string? Feedback, string? RubricScores);

public sealed record ExtendPeerReviewWindowRequest(
    int ExpectedStageVersion,
    DateTime DeadlineAt,
    string Reason,
    string IdempotencyKey);

public sealed record ReassignPeerReviewClaimRequest(
    int ExpectedClaimVersion,
    string Reason,
    string IdempotencyKey);

public sealed record ResolvePeerReviewInsufficiencyRequest(
    PeerReviewInsufficiencyResolutionV1 Resolution,
    string IdempotencyKey);

public sealed record PeerReviewSubmitDto(Guid ReviewId, PeerReviewClaimStatus Status);

/// <summary>
/// THE anonymity boundary for reviewers: the reviewee's submission stripped of every identity,
/// grade, and instructor-feedback field. Never add UserId/EnrollmentId/Score/Passed/Feedback/
/// GradedBy/GradedAt/CourseGroupId here; the reviewee's name must never appear either.
/// </summary>
public sealed record AnonymousReviewSubmissionDto(
    Guid ReviewId,
    PeerReviewClaimStatus Status,
    AnonymousReviewAssessmentDto Assessment,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AnonymousReviewRubricDto? Rubric,
    int AttemptNumber,
    DateTime? SubmittedAt,
    SubmissionStatus SubmissionStatus,
    string? DeliveryCanonicalJson,
    string? ResponseEnvelopeCanonicalJson,
    string? TextPayload,
    string? UrlPayload,
    string? CodePayload,
    string? MediaPayload,
    string? FilePayload,
    string? ProjectPayload);

public sealed record AnonymousReviewAssessmentDto(Guid Id, string Title, ScoreValue MaxScore);

public sealed record AnonymousReviewRubricDto(IReadOnlyList<RubricCriterionDto> Criteria);

/// <summary>
/// THE anonymity boundary for reviewees: what a student sees of reviews they received.
/// Reviewer identity is absent from the DTO, not merely null.
/// </summary>
public sealed record ReceivedPeerReviewDto(
    Guid ReviewId,
    ScoreValue? Score,
    string? Feedback,
    string? RubricScoresPayload,
    DateTime? SubmittedAt);

/// <summary>
/// Instructor-only view of the same reviews, with reviewer identity (SpeedGrader peer panel).
/// </summary>
public sealed record InstructorPeerReviewDto(
    Guid ReviewId,
    ScoreValue? Score,
    string? Feedback,
    string? RubricScoresPayload,
    DateTime? SubmittedAt,
    Guid ReviewerUserId,
    string ReviewerName);
