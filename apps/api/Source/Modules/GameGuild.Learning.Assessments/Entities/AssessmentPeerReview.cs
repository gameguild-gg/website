
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Represents a peer review assigned to one reviewer for a single submission of an assessment.
/// </summary>
public class AssessmentPeerReview : EntityBase
{
    public Guid AssessmentId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public PeerReviewStatus Status { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public ScoreValue? Score { get; private set; }
    public string? Feedback { get; private set; }
    public string? RubricScoresPayload { get; private set; }

    private AssessmentPeerReview() { } // EF Core

    public static AssessmentPeerReview Create(Guid assessmentId, Guid submissionId, Guid reviewerUserId)
    {
        return new AssessmentPeerReview
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            SubmissionId = submissionId,
            ReviewerUserId = reviewerUserId,
            Status = PeerReviewStatus.Assigned,
            AssignedAt = SystemClock.UtcNow
        };
    }

    public void SubmitReview(ScoreValue? score, string? feedback, string? rubricScores)
    {
        if (Status != PeerReviewStatus.Assigned)
        {
            throw new InvalidOperationException("Only an assigned peer review can be submitted.");
        }

        Score = score;
        Feedback = feedback;
        RubricScoresPayload = rubricScores;
        Status = PeerReviewStatus.Submitted;
        SubmittedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }
}

public enum PeerReviewStatus
{
    Assigned = 0,
    Submitted = 1
}
