using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service interface for assessment management and submission processing
/// </summary>
public interface IAssessmentService
{
    // ===== ASSESSMENT MANAGEMENT =====

    /// <summary>
    /// Creates a new assessment for a course
    /// </summary>
    Task<Result<Assessment>> CreateAssessmentAsync(CreateAssessmentRequest request);

    /// <summary>
    /// Gets an assessment by ID
    /// </summary>
    Task<Assessment?> GetAssessmentByIdAsync(Guid id);

    /// <summary>
    /// Gets an assessment by ID, ignoring the soft-delete filter. Used by the restore flow.
    /// </summary>
    Task<Assessment?> GetAssessmentByIdIncludingDeletedAsync(Guid id);

    /// <summary>
    /// Gets all assessments for a course
    /// </summary>
    Task<IEnumerable<Assessment>> GetCourseAssessmentsAsync(Guid courseId);

    /// <summary>
    /// Gets score distribution and weighted group performance for a course.
    /// </summary>
    Task<CourseAssessmentAnalyticsDto> GetCourseAssessmentAnalyticsAsync(Guid courseId);

    /// <summary>
    /// Updates an existing assessment
    /// </summary>
    Task<Result<Assessment>> UpdateAssessmentAsync(Guid id, UpdateAssessmentRequest request);

    /// <summary>
    /// Deletes an assessment
    /// </summary>
    Task<Result> DeleteAssessmentAsync(Guid id);

    /// <summary>
    /// Restores a soft-deleted assessment. Idempotent if the assessment is already active.
    /// </summary>
    Task<Result> RestoreAssessmentAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all weighted assessment groups for a course.
    /// </summary>
    Task<IEnumerable<AssessmentGroup>> GetCourseAssessmentGroupsAsync(Guid courseId);

    /// <summary>
    /// Gets an assessment group by its persisted identifier.
    /// </summary>
    Task<AssessmentGroup?> GetAssessmentGroupByIdAsync(Guid id);

    /// <summary>
    /// Creates a weighted assessment group for a course.
    /// </summary>
    Task<Result<AssessmentGroup>> CreateAssessmentGroupAsync(CreateAssessmentGroupRequest request);

    /// <summary>
    /// Updates a weighted assessment group.
    /// </summary>
    Task<Result<AssessmentGroup>> UpdateAssessmentGroupAsync(Guid id, UpdateAssessmentGroupRequest request);

    /// <summary>
    /// Deletes a weighted assessment group and unassigns its assessments.
    /// </summary>
    Task<Result> DeleteAssessmentGroupAsync(Guid id);

    /// <summary>
    /// Assigns an assessment to a weighted group or clears the assignment.
    /// </summary>
    Task<Result<Assessment>> AssignAssessmentToGroupAsync(Guid assessmentId, AssignAssessmentGroupRequest request);

    /// <summary>
    /// Links an assessment to a cue in an interactive-video lesson.
    /// </summary>
    Task<Result<InteractiveVideoAssessmentCue>> LinkInteractiveVideoCueAsync(Guid assessmentId, LinkInteractiveVideoCueRequest request);

    /// <summary>
    /// Removes a cue link. Cue links are hard deleted so the same stable cue may be linked again.
    /// </summary>
    Task<Result> UnlinkInteractiveVideoCueAsync(Guid assessmentId, Guid cueId);

    /// <summary>
    /// Gets the interactive-video cue links configured for an assessment.
    /// </summary>
    Task<IEnumerable<InteractiveVideoAssessmentCue>> GetInteractiveVideoCuesAsync(Guid assessmentId);

    /// <summary>
    /// Gets active cue links for one delivery content item.
    /// </summary>
    Task<IEnumerable<InteractiveVideoAssessmentCue>> GetInteractiveVideoCuesForContentAsync(Guid assessmentId, Guid contentId);

    // ===== SUBMISSION MANAGEMENT =====

    /// <summary>
    /// Starts a new assessment submission attempt
    /// </summary>
    Task<Result<AssessmentSubmission>> StartSubmissionAsync(Guid assessmentId, Guid enrollmentId, Guid userId);

    /// <summary>
    /// Submits a completed assessment
    /// </summary>
    Task<Result<AssessmentSubmission>> SubmitAsync(Guid submissionId, SubmitAssessmentRequest? request = null);

    /// <summary>
    /// Grades a submission
    /// </summary>
    Task<Result<AssessmentSubmission>> GradeSubmissionAsync(Guid submissionId, GradeSubmissionRequest request);

    /// <summary>
    /// Gets a submission by ID
    /// </summary>
    Task<AssessmentSubmission?> GetSubmissionByIdAsync(Guid id);

    /// <summary>
    /// Gets all submissions for an assessment
    /// </summary>
    Task<IEnumerable<AssessmentSubmission>> GetAssessmentSubmissionsAsync(Guid assessmentId);

    /// <summary>
    /// Gets all submissions for a user enrollment
    /// </summary>
    Task<IEnumerable<AssessmentSubmission>> GetUserSubmissionsAsync(Guid enrollmentId);

    Task<IEnumerable<AssessmentSubmission>> GetUserSubmissionsAsync(Guid enrollmentId, Guid userId);

    /// <summary>
    /// Gets the number of attempts a user has made for an assessment
    /// </summary>
    Task<int> GetAttemptCountAsync(Guid assessmentId, Guid enrollmentId);

    /// <summary>
    /// Checks if a user can attempt an assessment
    /// </summary>
    Task<Result<bool>> CanAttemptAsync(Guid assessmentId, Guid enrollmentId);
}

/// <summary>
/// Request to create a new assessment
/// </summary>
public sealed record CreateAssessmentRequest(
    Guid CourseId,
    string Title,
    string? Description,
    AssessmentType Type,
    ScoreValue MaxScore,
    ScoreValue? PassingScore = null,
    int? TimeLimitMinutes = null,
    int MaxAttempts = 1,
    bool IsRequired = true,
    DateTime? AvailableFrom = null,
    DateTime? AvailableUntil = null,
    Guid? AssessmentGroupId = null,
    SubmissionModality SubmissionModalities = SubmissionModality.Text,
    AssessmentPresentationMode PresentationMode = AssessmentPresentationMode.SingleStep,
    DateTime? DueAt = null,
    bool AllowLateSubmissions = false,
    DateTime? LateSubmissionDeadline = null,
    Guid? ContentId = null,
    ReviewMethods ReviewMethods = ReviewMethods.InstructorReview,
    string? Slug = null
);

/// <summary>
/// Request to update an assessment
/// </summary>
public sealed record UpdateAssessmentRequest(
    int ExpectedVersion,
    string? Title = null,
    string? Description = null,
    bool ClearDescription = false,
    ScoreValue? MaxScore = null,
    ScoreValue? PassingScore = null,
    int? TimeLimitMinutes = null,
    bool ClearTimeLimitMinutes = false,
    int? MaxAttempts = null,
    bool? IsRequired = null,
    DateTime? AvailableFrom = null,
    bool ClearAvailableFrom = false,
    DateTime? AvailableUntil = null,
    bool ClearAvailableUntil = false,
    Guid? ContentId = null,
    bool ClearContentId = false,
    Guid? AssessmentGroupId = null,
    bool ClearAssessmentGroupId = false,
    SubmissionModality? SubmissionModalities = null,
    AssessmentPresentationMode? PresentationMode = null,
    DateTime? DueAt = null,
    bool ClearDueAt = false,
    bool? AllowLateSubmissions = null,
    DateTime? LateSubmissionDeadline = null,
    bool ClearLateSubmissionDeadline = false,
    ReviewMethods? ReviewMethods = null,
    Guid? GroupSetId = null,
    bool ClearGroupSetId = false,
    string? ReviewConfigurationCanonicalJson = null,
    AttemptContributionMode? AttemptContributionMode = null,
    ContentCompletionMode? ContentCompletionMode = null,
    ResultReleaseMode? ResultReleaseMode = null,
    DateTime? ResultReleaseScheduledFor = null,
    string? Slug = null
);

/// <summary>
/// Request to create a weighted assessment group.
/// </summary>
public sealed record CreateAssessmentGroupRequest(
    Guid CourseId,
    string Name,
    PercentValue WeightPercent,
    int Order = 0,
    string? Description = null
);

/// <summary>
/// Request to update a weighted assessment group.
/// </summary>
public sealed record UpdateAssessmentGroupRequest(
    string? Name = null,
    string? Description = null,
    PercentValue? WeightPercent = null,
    int? Order = null
);

/// <summary>
/// Request to assign or clear an assessment group.
/// </summary>
public sealed record AssignAssessmentGroupRequest(
    Guid? AssessmentGroupId = null,
    bool ClearAssessmentGroup = false
);

/// <summary>
/// Links a graded assessment to a stable cue in a delivery-owned interactive video.
/// </summary>
public sealed record LinkInteractiveVideoCueRequest(
    Guid ContentId,
    string CueId,
    decimal? CuePositionSeconds = null
);

/// <summary>
/// Persists one or more answer payloads when a learner submits an assessment.
/// </summary>
public sealed record SubmitAssessmentRequest(
    string? TextPayload = null,
    string? FilePayload = null,
    string? UrlPayload = null,
    string? CodePayload = null,
    string? MediaPayload = null,
    string? ProjectPayload = null
);

/// <summary>
/// Request to grade a submission
/// </summary>
public sealed record GradeSubmissionRequest(
    ScoreValue Score,
    Guid? GradedBy = null,
    string? Feedback = null,
    string? RubricScores = null
);

public sealed record AssessmentScoreBucketDto(
    string Label,
    int MinPercent,
    int MaxPercent,
    int Count
);

public sealed record AssessmentGroupAnalyticsDto(
    Guid? GroupId,
    string GroupName,
    PercentValue? WeightPercent,
    int AssessmentCount,
    int GradedCount,
    int UngradedCount,
    PercentValue AveragePercent,
    PercentValue PassRate,
    IReadOnlyCollection<AssessmentScoreBucketDto> Distribution
);

public sealed record CourseAssessmentAnalyticsDto(
    Guid CourseId,
    int AssessmentCount,
    int GradedCount,
    int UngradedCount,
    PercentValue AveragePercent,
    PercentValue PassRate,
    IReadOnlyCollection<AssessmentScoreBucketDto> Distribution,
    IReadOnlyCollection<AssessmentGroupAnalyticsDto> Groups
);
