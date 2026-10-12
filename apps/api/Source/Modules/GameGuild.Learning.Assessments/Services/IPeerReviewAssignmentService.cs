using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Assigns anonymous peer reviews by claiming the least-reviewed eligible submission,
/// preserves legacy claims and reads while canonical grading owns review writes.
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
    /// Legacy write boundary. It remains fail-closed until peer review is implemented as a
    /// canonical grading stage.
    /// </summary>
    Task<Result<AssessmentPeerReview>> SubmitReviewAsync(
        AssessmentPeerReview review, ScoreValue score, string feedback, string? rubricScores);

    /// <summary>
    /// Submitted reviews attached to the single supplied submission.
    /// </summary>
    Task<IReadOnlyList<AssessmentPeerReview>> GetReviewsForSubmissionAsync(Guid submissionId);

    /// <summary>
    /// Checks the individual owner or frozen participant snapshot of a collective submission.
    /// </summary>
    Task<bool> IsSubmissionOwnerOrParticipantAsync(Guid submissionId, Guid userId);

    /// <summary>
    /// Display names (User.Name) for reviewer ids, missing names excluded.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetReviewerDisplayNamesAsync(IReadOnlyCollection<Guid> userIds);
}

/// <summary>
/// Claim outcome. <see cref="MaskedSubmission"/> is the ONLY reviewee information students ever see.
/// </summary>
public sealed record PeerReviewClaimResult(Guid ReviewId, string MaskedSubmission);
