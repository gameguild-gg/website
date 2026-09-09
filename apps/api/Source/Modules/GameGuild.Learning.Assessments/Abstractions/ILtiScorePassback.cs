using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Posts a released grade back to the LTI platform line item mapped to the assessment
/// (AGS score passback). Implemented by the LTI module; failures are swallowed by the
/// implementation — grading must never fail because a platform is slow or broken.
/// </summary>
public interface ILtiScorePassback
{
    Task PostScoreIfMappedAsync(Guid assessmentId, Guid userId, ScoreValue score, ScoreValue maxScore);
}
