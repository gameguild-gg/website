using Asp.Versioning;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
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
    private readonly IProgramCrudService _programService;
    private readonly IPermissionQueryService _permissionQueryService;
    private readonly ILogger<PeerReviewsController> _logger;

    public PeerReviewsController(
        IPeerReviewAssignmentService peerReviewService,
        IAssessmentService assessmentService,
        IRubricService rubricService,
        IActorContextAccessor actorContextAccessor,
        IProgramCrudService programService,
        IPermissionQueryService permissionQueryService,
        ILogger<PeerReviewsController> logger)
    {
        _peerReviewService = peerReviewService;
        _assessmentService = assessmentService;
        _rubricService = rubricService;
        _actorContextAccessor = actorContextAccessor;
        _programService = programService;
        _permissionQueryService = permissionQueryService;
        _logger = logger;
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
        if (assessment == null) return NotFound();
        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();

        var result = await _peerReviewService
            .ClaimAsync(assessmentId, actor.SubjectIdAsGuid.Value)
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
        if (actor.SubjectIdAsGuid == null) return Unauthorized();

        var review = await _peerReviewService.GetReviewAsync(reviewId).ConfigureAwait(false);
        if (review == null) return NotFound();
        if (review.ReviewerUserId != actor.SubjectIdAsGuid.Value) return Forbid();

        var assessment = await _assessmentService.GetAssessmentByIdAsync(review.AssessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();
        if (!IsReviewWindowOpen(assessment))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = "Review window closed"
            });
        }

        var submission = await _assessmentService.GetSubmissionByIdAsync(review.SubmissionId).ConfigureAwait(false);
        if (submission == null) return NotFound();

        var rubric = await _rubricService.GetAsync(assessment.Id).ConfigureAwait(false);
        return Ok(new AnonymousReviewSubmissionDto(
            review.Id,
            review.Status,
            new AnonymousReviewAssessmentDto(assessment.Id, assessment.Title, assessment.MaxScore),
            rubric.IsSuccess ? new AnonymousReviewRubricDto(rubric.Value.Criteria) : null,
            submission.AttemptNumber,
            submission.SubmittedAt,
            submission.Status,
            submission.TextPayload,
            submission.UrlPayload,
            submission.CodePayload,
            submission.MediaPayload,
            submission.FilePayload,
            submission.ProjectPayload));
    }

    /// <summary>
    /// Submit a claimed peer review. Feedback is mandatory; scores follow the assessment's
    /// rubric rules (rubric grid when one exists, plain 0..MaxScore otherwise).
    /// </summary>
    [HttpPost("peer-reviews/{reviewId:guid}/submit")]
    public async Task<IActionResult> SubmitReview(Guid reviewId, [FromBody] PeerReviewSubmitRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null) return Unauthorized();

        var review = await _peerReviewService.GetReviewAsync(reviewId).ConfigureAwait(false);
        if (review == null) return NotFound();
        if (review.ReviewerUserId != actor.SubjectIdAsGuid.Value) return Forbid();

        var assessment = await _assessmentService.GetAssessmentByIdAsync(review.AssessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();
        if (!IsReviewWindowOpen(assessment))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = "Review window closed"
            });
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

        var result = await _peerReviewService
            .SubmitReviewAsync(review, score, request.Feedback.Trim(), request.RubricScores)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Peer review submit rejected",
                Detail = result.Error.Description
            });
        }

        return Ok(new PeerReviewSubmitDto(result.Value.Id, result.Value.Status));
    }

    /// <summary>
    /// Reviews received on a submission (own row, or the group's rows for group submissions).
    /// Owner-only, anonymized: no reviewer identity exists in the DTO at all.
    /// </summary>
    [HttpGet("submissions/{submissionId:guid}/received-peer-reviews")]
    public async Task<ActionResult<IEnumerable<ReceivedPeerReviewDto>>> GetReceivedPeerReviews(Guid submissionId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null) return Unauthorized();

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission == null) return NotFound();
        if (submission.UserId != actor.SubjectIdAsGuid.Value) return Forbid();

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await IsActorInProgramTenantAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();

        var reviews = await _peerReviewService.GetReviewsForSubmissionAsync(submissionId).ConfigureAwait(false);
        return Ok(reviews.Select(r => new ReceivedPeerReviewDto(
            r.Id, r.Score, r.Feedback, r.RubricScoresPayload, r.SubmittedAt)));
    }

    /// <summary>
    /// Same reviews for instructors, with reviewer names. CanManageCourse-only.
    /// </summary>
    [HttpGet("submissions/{submissionId:guid}/peer-reviews")]
    public async Task<ActionResult<IEnumerable<InstructorPeerReviewDto>>> GetPeerReviews(Guid submissionId)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null) return Unauthorized();

        var submission = await _assessmentService.GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
        if (submission == null) return NotFound();

        var assessment = await _assessmentService.GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
        if (assessment == null) return NotFound();
        if (!await CanManageCourseAsync(assessment.CourseId).ConfigureAwait(false)) return Forbid();

        var reviews = await _peerReviewService.GetReviewsForSubmissionAsync(submissionId).ConfigureAwait(false);
        var names = await _peerReviewService
            .GetReviewerDisplayNamesAsync(reviews.Select(r => r.ReviewerUserId).Distinct().ToList())
            .ConfigureAwait(false);
        return Ok(reviews.Select(r => new InstructorPeerReviewDto(
            r.Id,
            r.Score,
            r.Feedback,
            r.RubricScoresPayload,
            r.SubmittedAt,
            r.ReviewerUserId,
            names.TryGetValue(r.ReviewerUserId, out var name) ? name : r.ReviewerUserId.ToString())));
    }

    // Reviews run to the assessment close (DueAt ?? AvailableUntil ?? LateSubmissionDeadline) —
    // deliberately INCLUDING the late deadline, unlike group-join locking which stops at due.
    private static bool IsReviewWindowOpen(Assessment assessment)
    {
        var closesAt = assessment.DueAt ?? assessment.AvailableUntil ?? assessment.LateSubmissionDeadline;
        return closesAt is null || closesAt >= SystemClock.UtcNow;
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

public sealed record PeerReviewSubmitDto(Guid ReviewId, PeerReviewStatus Status);

/// <summary>
/// THE anonymity boundary for reviewers: the reviewee's submission stripped of every identity,
/// grade, and instructor-feedback field. Never add UserId/EnrollmentId/Score/Passed/Feedback/
/// GradedBy/GradedAt/CourseGroupId here; the reviewee's name must never appear either.
/// </summary>
public sealed record AnonymousReviewSubmissionDto(
    Guid ReviewId,
    PeerReviewStatus Status,
    AnonymousReviewAssessmentDto Assessment,
    AnonymousReviewRubricDto? Rubric,
    int AttemptNumber,
    DateTime? SubmittedAt,
    SubmissionStatus SubmissionStatus,
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
