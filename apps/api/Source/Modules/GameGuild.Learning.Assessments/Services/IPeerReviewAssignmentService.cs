using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Assigns anonymous peer reviews by claiming the least-reviewed eligible submission,
/// submits them, and reads them back with role-appropriate identity stripping.
/// </summary>
public interface IPeerReviewAssignmentService
{
    /// <summary>
    /// Claims one peer review for the actor: gates (own submission, quota), then picks a random
    /// submission among those tied for the fewest existing reviews. No skip/unassign exists.
    /// </summary>
    Task<Result<PeerReviewClaimResult>> ClaimAsync(Guid assessmentId, Guid actorUserId);

    /// <summary>
    /// Loads a single review (null when not found). Callers enforce the reviewer-only rule.
    /// </summary>
    Task<AssessmentPeerReview?> GetReviewAsync(Guid reviewId);

    /// <summary>
    /// Submits an already-validated review (feedback/score/rubric rules live in the controller).
    /// Fails with Conflict when the review was already submitted.
    /// </summary>
    Task<Result<AssessmentPeerReview>> SubmitReviewAsync(
        AssessmentPeerReview review, ScoreValue score, string feedback, string? rubricScores);

    /// <summary>
    /// Submitted reviews visible on a submission: its own rows, plus — for a group submission —
    /// the union of reviews on any row sharing (CourseGroupId, AttemptNumber).
    /// </summary>
    Task<IReadOnlyList<AssessmentPeerReview>> GetReviewsForSubmissionAsync(Guid submissionId);

    /// <summary>
    /// Display names (User.Name) for reviewer ids, missing names excluded.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetReviewerDisplayNamesAsync(IReadOnlyCollection<Guid> userIds);
}

/// <summary>
/// Claim outcome. <see cref="MaskedSubmission"/> is the ONLY reviewee information students ever see.
/// </summary>
public sealed record PeerReviewClaimResult(Guid ReviewId, string MaskedSubmission);
