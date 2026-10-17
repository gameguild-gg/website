using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service interface for the instructor grading queue (SpeedGrader navigation bundle).
/// </summary>
public interface IGradingQueueService
{
    /// <summary>
    /// Builds the grading queue for an assessment: ONE item per student (individual assessments)
    /// or group (group assessments), representing the target's latest gradeable attempt.
    /// Targets with only InProgress rows are excluded.
    /// </summary>
    Task<Result<GradingQueueDto>> GetQueueAsync(Guid assessmentId);
}

/// <summary>
/// SpeedGrader navigation bundle: assessment summary plus one queue item per student/group.
/// No peer-review data here — SpeedGrader fetches reviews per submission.
/// </summary>
public sealed record GradingQueueDto(
    GradingQueueAssessmentDto Assessment,
    IReadOnlyList<GradingQueueItemDto> Items,
    int Total,
    int NeedsGrading);

/// <summary>
/// Assessment summary fields the SpeedGrader header and grading panel need.
/// </summary>
public sealed record GradingQueueAssessmentDto(
    Guid Id,
    string Title,
    AssessmentType Type,
    ScoreValue MaxScore,
    ReviewMethods ReviewMethods,
    Guid? GroupSetId,
    bool HasRubric,
    RubricDto? Rubric);

/// <summary>
/// One navigable queue entry = one student or group. SubmissionId is the target's latest
/// attempt and CanonicalSubmissionId mirrors it for existing clients. AssignmentScore and
/// AssignmentPassed are the assignment-level grade from
/// the target's LATEST GRADED attempt — they persist across resubmissions until a newer
/// attempt is graded (one grade per assignment; regrades land on a fresh submission).
/// When the submission has a grading execution, the active round/stage fields are the
/// authoritative operational state and RequiresStaffReview drives the instructor queue.
/// </summary>
public sealed record GradingQueueItemDto(
    Guid SubmissionId,
    Guid CanonicalSubmissionId,
    int AttemptNumber,
    int AttemptCount,
    SubmissionStatus Status,
    bool IsLate,
    DateTime? SubmittedAt,
    ScoreValue? AssignmentScore,
    bool? AssignmentPassed,
    bool IsGroup,
    Guid? UserId = null,
    string? DisplayName = null,
    Guid? GroupId = null,
    string? GroupName = null,
    IReadOnlyList<string>? MemberNames = null,
    Guid? ActiveGradeRoundId = null,
    GradeRoundStatusV1? ActiveGradeRoundStatus = null,
    Guid? ActiveReviewStageId = null,
    ReviewMethod? ActiveReviewMethod = null,
    ReviewStageStatusV1? ActiveReviewStageStatus = null,
    bool RequiresStaffReview = false);
