using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Owns peer-review leases and evidence while ReviewStage remains the only grading authority.
/// </summary>
public interface IPeerReviewAssignmentService
{
    Task<Result<PeerReviewClaimResult>> ClaimAsync(Guid assessmentId, Guid actorUserId);

    Task<Result<PeerReviewClaimResult>> ClaimAuthorTestAsync(
        Guid testRunId,
        Guid reviewerTestRunSubjectId,
        Guid actorUserId);

    Task<PeerReviewClaimView?> GetReviewAsync(Guid reviewId);

    Task<Result<PeerReviewClaimView>> SubmitReviewAsync(
        Guid reviewId,
        Guid actorUserId,
        ScoreValue score,
        string feedback,
        string? rubricScores);

    Task<IReadOnlyList<PeerReviewEvidenceView>> GetReviewsForSubmissionAsync(Guid submissionId);

    Task<bool> IsSubmissionOwnerOrParticipantAsync(Guid submissionId, Guid userId);

    Task<IReadOnlyDictionary<Guid, string>> GetReviewerDisplayNamesAsync(IReadOnlyCollection<Guid> userIds);

    Task<Guid?> GetAssessmentIdForStageAsync(
        Guid reviewStageId,
        CancellationToken cancellationToken = default);

    Task ExtendEvidenceWindowAsync(
        Guid reviewStageId,
        Guid actorUserId,
        int expectedStageVersion,
        DateTime deadlineAt,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task ReassignClaimAsync(
        Guid reviewId,
        Guid actorUserId,
        int expectedClaimVersion,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task ResolveInsufficientEvidenceAsync(
        Guid reviewStageId,
        Guid actorUserId,
        PeerReviewInsufficiencyResolutionV1 resolution,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public sealed record PeerReviewClaimResult(Guid ReviewId, string MaskedSubmission);

public sealed record PeerReviewClaimView(
    Guid Id,
    Guid ReviewStageId,
    Guid AssessmentId,
    Guid? SubmissionId,
    Guid? ReviewerUserId,
    Guid? ReviewerTestRunSubjectId,
    PeerReviewClaimStatus Status,
    int Version,
    DateTime ClaimedAt,
    DateTime LeaseExpiresAt,
    DateTime? SubmittedAt,
    int AttemptNumber,
    string? DeliveryCanonicalJson,
    string? ResponseEnvelopeCanonicalJson);

public sealed record PeerReviewEvidenceView(
    Guid ClaimId,
    Guid ReviewEvidenceId,
    Guid ReviewerUserId,
    ScoreValue Score,
    string Feedback,
    string? RubricScores,
    DateTime SubmittedAt);
