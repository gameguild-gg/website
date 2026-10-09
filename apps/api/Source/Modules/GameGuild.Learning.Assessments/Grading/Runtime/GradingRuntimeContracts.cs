using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed record InstructorItemResolutionV1(
    string ItemId,
    ScoreValue Score,
    string? Feedback = null);

public sealed record InstructorReviewResolutionV1(
    int SchemaVersion,
    IReadOnlyList<InstructorItemResolutionV1> Items,
    string? Feedback = null,
    string? OverrideReason = null,
    IReadOnlyDictionary<Guid, ScoreValue>? RubricScores = null);

public sealed record GradingExecutionOutcome(
    Guid ExecutionId,
    Guid RoundId,
    PersistedGradingExecutionStatus ExecutionStatus,
    PersistedGradeRoundStatus RoundStatus,
    GradeResultV1? Result,
    bool RequiresInstructorReview);

public sealed record GradeRoundViewV1(
    Guid RoundId,
    int RoundNumber,
    string Reason,
    string? ReasonDetail,
    Guid? InitiatedByActorId,
    PersistedGradeRoundStatus Status,
    DateTime StartedAt,
    DateTime? FinalizedAt,
    GradeResultV1? Result,
    bool Released,
    DateTime? ReleasedAt);

public sealed record AssessmentExecutionViewV1(
    Guid ExecutionId,
    Guid DefinitionRevisionId,
    ReviewExecutionContext Context,
    string ExecutionSnapshotHash,
    string DeliveryHash,
    AssessmentExecutionDeliveryV1 Delivery,
    IReadOnlyDictionary<string, ScoreValue> ItemMaxScores,
    AssessmentResponseEnvelopeV1? SubmittedResponse,
    PersistedGradingExecutionStatus Status,
    Guid? ActiveRoundId,
    GradeResultV1? InstructorVisibleResult,
    GradeResultV1? LearnerVisibleResult,
    bool RequiresInstructorReview,
    bool Released,
    IReadOnlyList<GradeRoundViewV1> History,
    System.Text.Json.JsonElement? InstructorVisibleContent = null,
    System.Text.Json.JsonElement? InstructorVisibleRubric = null,
    IReadOnlyDictionary<Guid, ScoreValue>? InstructorVisibleRubricScores = null);

public sealed record AssessmentTestRunViewV1(
    Guid TestRunId,
    Guid AssessmentId,
    Guid DefinitionRevisionId,
    AssessmentTestRunStatus Status,
    string PersonaKey,
    string PersonaDisplayName,
    AssessmentExecutionViewV1 Execution,
    bool CandidateStillMatchesDraft,
    bool ReadyForPublication,
    IReadOnlyList<string> Diagnostics);

public sealed record AssessmentSubmissionViewV1(
    Guid SubmissionId,
    Guid AssessmentId,
    Guid DefinitionRevisionId,
    Guid? EnrollmentId,
    Guid? CourseGroupId,
    int AttemptNumber,
    SubmissionStatus Status,
    long DraftVersion,
    int Version,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    Guid? SubmittedByUserId,
    bool ContentCompleted,
    AssessmentExecutionViewV1 Execution);

public sealed record StartAssessmentTestRunCommand(
    Guid RevisionId,
    string PersonaKey,
    string PersonaDisplayName,
    string IdempotencyKey);

public sealed record StartIndividualSubmissionCommand(
    Guid EnrollmentId,
    Guid UserId,
    Guid ActorId,
    string IdempotencyKey);

public sealed record StartCollectiveSubmissionCommand(
    Guid CourseGroupId,
    Guid ActorId,
    string IdempotencyKey);

public sealed record SubmitAssessmentResponseCommand(
    AssessmentResponseEnvelopeV1 Response,
    string IdempotencyKey,
    long? ExpectedDraftVersion = null);

public sealed record SaveCollectiveAssessmentDraftCommand(
    AssessmentResponseEnvelopeV1 Response,
    long ExpectedVersion,
    string IdempotencyKey);

public sealed record ReleaseGradeResultCommand(
    Guid ExpectedRoundId,
    long ExpectedSubmissionVersion,
    string IdempotencyKey,
    string? Reason = null);

public sealed record RegradeExecutionCommand(
    string IdempotencyKey,
    string Reason);
