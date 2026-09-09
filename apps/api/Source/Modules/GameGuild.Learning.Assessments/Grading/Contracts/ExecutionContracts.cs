using System.Text.Json;
using System.Text.Json.Serialization;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Contracts;

public static class GradingContractVersions
{
    public const int ContentGrading = 2;
    public const int ExecutionPolicy = 1;
    public const int ResponseEnvelope = 1;
    public const int ExecutionManifest = 1;
    public const int ExecutionDelivery = 1;
    public const int GradeResult = 1;
    public const string Hash = "sha256-jcs-v1";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GradingItemAuthoringV2(string? RubricRef = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentGradingDefinitionV2(
    int SchemaVersion,
    IReadOnlyDictionary<string, GradingItemAuthoringV2> Items);

[JsonConverter(typeof(JsonStringEnumConverter<ReviewExecutionContext>))]
public enum ReviewExecutionContext
{
    [JsonStringEnumMemberName("author-test")]
    AuthorTest,
    [JsonStringEnumMemberName("official-submission")]
    OfficialSubmission,
}

[JsonConverter(typeof(JsonStringEnumConverter<AttemptContributionMode>))]
public enum AttemptContributionMode
{
    [JsonStringEnumMemberName("first-finalized")]
    FirstFinalized,
    [JsonStringEnumMemberName("last-finalized")]
    LastFinalized,
    [JsonStringEnumMemberName("highest-finalized")]
    HighestFinalized,
}

[JsonConverter(typeof(JsonStringEnumConverter<ContentCompletionMode>))]
public enum ContentCompletionMode
{
    [JsonStringEnumMemberName("on-submit")]
    OnSubmit,
    [JsonStringEnumMemberName("on-finalize")]
    OnFinalize,
    [JsonStringEnumMemberName("on-release")]
    OnRelease,
    [JsonStringEnumMemberName("on-release-and-pass")]
    OnReleaseAndPass,
}

[JsonConverter(typeof(JsonStringEnumConverter<ResultReleaseMode>))]
public enum ResultReleaseMode
{
    [JsonStringEnumMemberName("immediate")]
    Immediate,
    [JsonStringEnumMemberName("manual")]
    Manual,
    [JsonStringEnumMemberName("scheduled")]
    Scheduled,
}

public sealed record AttemptContributionPolicyV1(AttemptContributionMode Mode);

public sealed record AssessmentAvailabilityPolicyV1(
    string? AvailableFrom,
    string? AvailableUntil,
    string? DueAt,
    bool AllowLateSubmissions,
    string? LateSubmissionDeadline);

public sealed record AssessmentContentCompletionPolicyV1(ContentCompletionMode Mode);

public sealed record AssessmentResultReleasePolicyV1(ResultReleaseMode Mode, string? ScheduledFor = null);

public sealed record AssessmentPresentationPolicyV1(string Mode);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PeerReviewPolicyV1(
    int ReviewsPerReviewer,
    int ReviewsRequiredPerSubmission,
    int MinimumReviewsToFinalize,
    string Aggregation,
    int ClaimLeaseMinutes,
    int EvidenceWindowMinutes,
    string OnInsufficientEvidence);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AiReviewPolicyV1(string ProviderKey, string PolicyVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SelfReviewPolicyV1(string? Instructions, bool RequireFeedback);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstructorReviewPolicyV1(bool RequireOverrideReason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssessmentReviewConfigurationV1(
    int SchemaVersion,
    PeerReviewPolicyV1? Peer = null,
    AiReviewPolicyV1? Ai = null,
    SelfReviewPolicyV1? Self = null,
    InstructorReviewPolicyV1? Instructor = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssessmentReviewPolicyV1(
    int SchemaVersion,
    ReviewMethods Methods,
    PeerReviewPolicyV1? Peer = null,
    AiReviewPolicyV1? Ai = null,
    SelfReviewPolicyV1? Self = null,
    InstructorReviewPolicyV1? Instructor = null);

public sealed record AssessmentExecutionPolicyV1(
    int SchemaVersion,
    ScoreValue? PassingScore,
    int? MaxAttempts,
    AttemptContributionPolicyV1? AttemptContribution,
    int? TimeLimitMinutes,
    AssessmentAvailabilityPolicyV1 Availability,
    AssessmentContentCompletionPolicyV1 Completion,
    AssessmentResultReleasePolicyV1 ResultRelease,
    AssessmentPresentationPolicyV1 Presentation,
    AssessmentReviewPolicyV1 Review);

public sealed record AssessmentAuthoringSourceV1(
    int SchemaVersion,
    string ContentType,
    JsonElement Content,
    ContentGradingDefinitionV2 Grading,
    AssessmentExecutionPolicyV1 Policy);

public sealed record AssessmentItemManifestV1(
    string ItemId,
    string ItemType,
    string AdapterKey,
    string AdapterVersion);

public sealed record AssessmentReviewStageManifestV1(
    ReviewMethod Method,
    string HandlerKey,
    string HandlerVersion,
    string? ProviderKey = null,
    string? ProviderPolicyVersion = null);

public sealed record AssessmentPolicyManifestV1(string PolicyKey, string PolicyVersion);

public sealed record AssessmentExecutionManifestV1(
    int SchemaVersion,
    IReadOnlyList<AssessmentItemManifestV1> Items,
    IReadOnlyList<AssessmentReviewStageManifestV1> Stages,
    IReadOnlyList<AssessmentPolicyManifestV1> Policies);

public sealed record AssessmentExecutionSnapshotV1(
    int SchemaVersion,
    AssessmentAuthoringSourceV1 AuthoringSource,
    AssessmentExecutionManifestV1 Manifest,
    IReadOnlyDictionary<string, JsonElement> ItemProjections);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssessmentResponseEnvelopeV1(
    int SchemaVersion,
    string ContentType,
    string PayloadSchema,
    JsonElement Payload);

public sealed record AssessmentExecutionDeliveryItemV1(
    string AdapterKey,
    string AdapterVersion,
    JsonElement LearnerPayload);

public sealed record AssessmentExecutionDeliveryV1(
    int SchemaVersion,
    Guid DefinitionRevisionId,
    string ExecutionSnapshotHash,
    IReadOnlyList<string> ItemOrder,
    IReadOnlyDictionary<string, AssessmentExecutionDeliveryItemV1> Items);

[JsonConverter(typeof(JsonStringEnumConverter<GradeItemState>))]
public enum GradeItemState
{
    [JsonStringEnumMemberName("graded")]
    Graded,
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("unsupported")]
    Unsupported,
}

public sealed record GradeItemResultV1(
    string ItemId,
    GradeItemState State,
    ScoreValue? Score,
    ScoreValue MaxScore,
    IReadOnlyList<string> EvidenceRefs,
    ReviewMethod ReviewMethod,
    string HandlerKey,
    string HandlerVersion,
    string? Feedback = null,
    string? ProviderKey = null);

public sealed record GradeResultV1(
    int SchemaVersion,
    string State,
    ScoreValue? Score,
    ScoreValue MaxScore,
    IReadOnlyList<GradeItemResultV1> Items,
    IReadOnlyList<string> EvidenceRefs,
    string? Feedback = null);

[JsonConverter(typeof(JsonStringEnumConverter<GradeRoundStatusV1>))]
public enum GradeRoundStatusV1
{
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("running")]
    Running,
    [JsonStringEnumMemberName("awaiting-evidence")]
    AwaitingEvidence,
    [JsonStringEnumMemberName("awaiting-instructor-resolution")]
    AwaitingInstructorResolution,
    [JsonStringEnumMemberName("failed")]
    Failed,
    [JsonStringEnumMemberName("finalized")]
    Finalized,
}

[JsonConverter(typeof(JsonStringEnumConverter<ReviewStageStatusV1>))]
public enum ReviewStageStatusV1
{
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("running")]
    Running,
    [JsonStringEnumMemberName("awaiting-evidence")]
    AwaitingEvidence,
    [JsonStringEnumMemberName("awaiting-instructor-resolution")]
    AwaitingInstructorResolution,
    [JsonStringEnumMemberName("completed")]
    Completed,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

[JsonConverter(typeof(JsonStringEnumConverter<GradeRoundReasonV1>))]
public enum GradeRoundReasonV1
{
    [JsonStringEnumMemberName("initial")]
    Initial,
    [JsonStringEnumMemberName("regrade")]
    Regrade,
}

[JsonConverter(typeof(JsonStringEnumConverter<GradingExecutionStateV1>))]
public enum GradingExecutionStateV1
{
    [JsonStringEnumMemberName("pending")]
    Pending,
    [JsonStringEnumMemberName("running")]
    Running,
    [JsonStringEnumMemberName("awaiting-review")]
    AwaitingReview,
    [JsonStringEnumMemberName("completed")]
    Completed,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

public sealed record ReviewStageV1(
    Guid Id,
    ReviewMethod Method,
    ReviewStageStatusV1 Status,
    string HandlerKey,
    string HandlerVersion,
    string? ProviderKey = null,
    IReadOnlyList<Guid>? ActorIds = null,
    IReadOnlyList<string>? EvidenceRefs = null,
    GradeResultV1? Result = null,
    string? StartedAt = null,
    string? CompletedAt = null);

public sealed record GradeRoundV1(
    Guid Id,
    Guid? SupersedesRoundId,
    GradeRoundReasonV1 Reason,
    Guid DefinitionRevisionId,
    IReadOnlyList<ReviewMethod> ConfiguredReviews,
    ReviewMethod? CurrentReview,
    GradeRoundStatusV1 Status,
    IReadOnlyList<ReviewStageV1> Stages,
    GradeResultV1? FinalResult,
    Guid? InitiatedBy,
    string InitiatedAt,
    string? FinalizedAt = null);

public sealed record AssessmentEvaluationV1(
    int SchemaVersion,
    Guid ActiveRoundId,
    IReadOnlyList<GradeRoundV1> Rounds);

public sealed record GradingExecutionV1(
    int SchemaVersion,
    Guid Id,
    ReviewExecutionContext Context,
    Guid DefinitionRevisionId,
    string ExecutionSnapshotHash,
    string? DeliveryHash,
    GradingExecutionStateV1 State);

public sealed record IdempotentCommandEnvelopeV1<TPayload>(
    int SchemaVersion,
    Guid TenantId,
    Guid ResourceId,
    string Command,
    Guid ActorId,
    string IdempotencyKey,
    string RequestHash,
    TPayload Payload);

public sealed record VersionedCollectiveCommandV1(long ExpectedVersion);

public sealed record SaveCollectiveAttemptDraftPayloadV1(
    long ExpectedVersion,
    AssessmentResponseEnvelopeV1 Response);

public sealed record SubmitCollectiveAttemptPayloadV1(long ExpectedVersion);

public sealed record SaveCollectiveSelfReviewDraftPayloadV1(
    long ExpectedVersion,
    JsonElement Evidence);

public sealed record SubmitCollectiveSelfReviewPayloadV1(long ExpectedVersion);

public sealed record ReleaseGradeResultPayloadV1(
    Guid SubmissionId,
    Guid ExpectedRoundId,
    long ExpectedVersion,
    string? Reason = null);

public sealed record SaveCollectiveAttemptDraftV1(
    int SchemaVersion,
    Guid TenantId,
    Guid ResourceId,
    string Command,
    Guid ActorId,
    string IdempotencyKey,
    string RequestHash,
    SaveCollectiveAttemptDraftPayloadV1 Payload);

public sealed record SubmitCollectiveAttemptV1(
    int SchemaVersion,
    Guid TenantId,
    Guid ResourceId,
    string Command,
    Guid ActorId,
    string IdempotencyKey,
    string RequestHash,
    SubmitCollectiveAttemptPayloadV1 Payload);

public sealed record SaveCollectiveSelfReviewDraftV1(
    int SchemaVersion,
    Guid TenantId,
    Guid ResourceId,
    string Command,
    Guid ActorId,
    string IdempotencyKey,
    string RequestHash,
    SaveCollectiveSelfReviewDraftPayloadV1 Payload);

public sealed record SubmitCollectiveSelfReviewV1(
    int SchemaVersion,
    Guid TenantId,
    Guid ResourceId,
    string Command,
    Guid ActorId,
    string IdempotencyKey,
    string RequestHash,
    SubmitCollectiveSelfReviewPayloadV1 Payload);

public sealed record ReleaseGradeResultV1(
    int SchemaVersion,
    Guid TenantId,
    Guid ResourceId,
    string Command,
    Guid ActorId,
    string IdempotencyKey,
    string RequestHash,
    ReleaseGradeResultPayloadV1 Payload);

public sealed record GradebookAssessmentContributionV1(
    Guid AssessmentId,
    ScoreValue EffectiveScore,
    ScoreValue CapturedMaxScore);

public sealed record GradebookGroupContributionV1(
    Guid AssessmentGroupId,
    PercentValue WeightPercent,
    IReadOnlyList<GradebookAssessmentContributionV1> Assessments);
