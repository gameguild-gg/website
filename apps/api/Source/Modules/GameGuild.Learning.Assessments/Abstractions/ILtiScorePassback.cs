using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Posts a released grade back to the LTI platform line item mapped to the assessment.
/// A missing mapping is a successful no-op. Delivery failures must propagate so the
/// academic outbox can retry them without reopening the finalized grade.
/// </summary>
public interface ILtiScorePassback
{
    Task<LtiScorePassbackOutcome> PostScoreIfMappedAsync(
        Guid assessmentId,
        Guid userId,
        ScoreValue score,
        ScoreValue maxScore,
        CancellationToken cancellationToken = default);
}

public enum LtiScorePassbackOutcome
{
    NotMapped,
    UserNotMapped,
    Delivered,
}
