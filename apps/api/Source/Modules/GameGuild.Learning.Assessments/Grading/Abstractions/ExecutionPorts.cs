using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Abstractions;

public sealed record AssessmentAuthoringItemProjectionV1(
    string ItemId,
    string ItemType,
    ScoreValue MaxScore,
    JsonElement PrivateProjection,
    string AdapterKey,
    string AdapterVersion);

public sealed record AssessmentAuthoringProjectionV1(
    string ContentType,
    JsonElement Content,
    ContentGradingDefinitionV2? Grading,
    IReadOnlyList<AssessmentAuthoringItemProjectionV1> Items)
{
    public ScoreValue MaxScore => ScoreValue.Sum(Items.Select(item => item.MaxScore));
}

public sealed record AssessmentReviewHandlerBinding(string Key, string Version);

/// <summary>
/// Owns the complete executable boundary for one assessment content type.
/// Its key and version bind authoring projection, delivery, response decoding,
/// and deterministic evaluation as one compatible unit.
/// </summary>
public interface IAssessmentTypeAdapter
{
    string Key { get; }
    string Version { get; }
    string ContentType { get; }
    bool IsCurrentForAuthoring { get; }
    ProgramContentType ProgramContentType { get; }
    AssessmentType AssessmentType { get; }
    SubmissionModality SubmissionModalities { get; }
    IReadOnlySet<ReviewExecutionContext> Contexts { get; }
    IReadOnlyDictionary<ReviewMethod, AssessmentReviewHandlerBinding> ReviewHandlers { get; }

    AssessmentAuthoringProjectionV1 ProjectAuthoring(JsonElement authoringDocument);
    JsonElement GenerateDelivery(JsonElement projectedItem);
    JsonElement DecodeResponse(
        AssessmentResponseEnvelopeV1 envelope,
        IReadOnlyList<JsonElement> projectedItems);
    bool CanEvaluateDeterministically(JsonElement projectedItem);
    ValueTask<GradeResultV1> EvaluateDeterministicAsync(
        DeterministicReviewRequest request,
        CancellationToken cancellationToken);
}

public interface IAssessmentTypeAdapterResolver
{
    IAssessmentTypeAdapter ResolveForAuthoring(ProgramContentType programContentType);

    IAssessmentTypeAdapter Resolve(
        string contentType,
        string key,
        string version,
        ReviewExecutionContext context);
}

public sealed record DeterministicReviewRequest(
    IReadOnlyList<JsonElement> ProjectedItems,
    AssessmentExecutionDeliveryV1 Delivery,
    JsonElement NormalizedResponse,
    string HandlerKey,
    string HandlerVersion);

public interface IAssessmentExecutionPolicyResolver
{
    void Resolve(string key, string version, ReviewExecutionContext context);
}

public interface IReviewStageHandler
{
    ReviewMethod Method { get; }
    string Key { get; }
    string Version { get; }
    string? ProviderKey { get; }
    string? ProviderPolicyVersion { get; }
    IReadOnlySet<ReviewExecutionContext> Contexts { get; }
    ValueTask<ReviewStageTransitionV1> StartAsync(
        ReviewStageRequest request,
        CancellationToken cancellationToken);
    ValueTask<ReviewStageTransitionV1> AcceptEvidenceAsync(
        ReviewStageRequest request,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken);
    ValueTask<ReviewStageTransitionV1> TryCompleteAsync(
        ReviewStageRequest request,
        IReadOnlyList<ReviewStageEvidenceV1> evidence,
        CancellationToken cancellationToken);
}

public interface IReviewStageHandlerResolver
{
    IReviewStageHandler Resolve(
        ReviewMethod method,
        string key,
        string version,
        ReviewExecutionContext context);
}

public sealed record ReviewStageRequest(
    Guid GradingExecutionId,
    Guid GradeRoundId,
    Guid ReviewStageId,
    ReviewExecutionContext Context,
    string ExecutionSnapshotHash,
    AssessmentExecutionSnapshotV1 Snapshot,
    AssessmentExecutionDeliveryV1 Delivery,
    AssessmentResponseEnvelopeV1 Response,
    GradeResultV1? PreviousStageResult,
    string HandlerKey,
    string HandlerVersion,
    string? ProviderKey,
    string? ProviderPolicyVersion,
    DateTime? EvidenceDeadlineAt);

public sealed record DurableReviewRequestV1(
    string EventType,
    string SchemaVersion,
    JsonElement Payload);

public enum ReviewStageTransitionKindV1
{
    Completed,
    AwaitingEvidence,
    AwaitingInstructorResolution,
    RetryableFailure,
    TerminalFailure,
}

public sealed record ReviewStageTransitionV1(
    int SchemaVersion,
    ReviewStageTransitionKindV1 Kind,
    GradeResultV1? Result = null,
    string? Diagnostic = null,
    DateTime? EvidenceDeadlineAt = null,
    DurableReviewRequestV1? DurableRequest = null)
{
    public static ReviewStageTransitionV1 Completed(GradeResultV1 result) => new(1, ReviewStageTransitionKindV1.Completed, result);

    public static ReviewStageTransitionV1 AwaitingEvidence(
        string? diagnostic = null,
        DateTime? evidenceDeadlineAt = null,
        DurableReviewRequestV1? durableRequest = null) =>
        new(
            1,
            ReviewStageTransitionKindV1.AwaitingEvidence,
            Diagnostic: diagnostic,
            EvidenceDeadlineAt: evidenceDeadlineAt,
            DurableRequest: durableRequest);

    public static ReviewStageTransitionV1 AwaitingInstructorResolution(string? diagnostic = null) =>
        new(1, ReviewStageTransitionKindV1.AwaitingInstructorResolution, Diagnostic: diagnostic);

    public static ReviewStageTransitionV1 RetryableFailure(string diagnostic) =>
        new(1, ReviewStageTransitionKindV1.RetryableFailure, Diagnostic: diagnostic);

    public static ReviewStageTransitionV1 TerminalFailure(string diagnostic) =>
        new(1, ReviewStageTransitionKindV1.TerminalFailure, Diagnostic: diagnostic);
}

public sealed record ReviewStageEvidenceV1(
    string EvidenceKey,
    string EvidenceType,
    string SchemaVersion,
    JsonElement Payload,
    Guid? ProducedByActorId = null,
    string? ProducedByService = null,
    string? ItemId = null);

public sealed record AIReviewProviderRequestV1(
    int SchemaVersion,
    Guid RequestId,
    Guid GradingExecutionId,
    Guid GradeRoundId,
    Guid ReviewStageId,
    ReviewExecutionContext Context,
    string ExecutionSnapshotHash,
    AssessmentExecutionSnapshotV1 Snapshot,
    AssessmentExecutionDeliveryV1 Delivery,
    AssessmentResponseEnvelopeV1 Response,
    GradeResultV1? PreviousStageResult,
    string ProviderKey,
    string ProviderPolicyVersion);

public sealed record AIReviewProviderResponseV1(
    int SchemaVersion,
    Guid RequestId,
    Guid GradingExecutionId,
    Guid GradeRoundId,
    Guid ReviewStageId,
    string ExecutionSnapshotHash,
    string ProviderKey,
    string ProviderPolicyVersion,
    string ModelKey,
    string ModelVersion,
    GradeResultV1 Result);

/// <summary>
/// Durable AI-review boundary. Implementations must treat RequestId as an
/// idempotency key and return the same semantic response when it is retried.
/// Concrete providers and credentials are composed outside this module.
/// </summary>
public interface IAIReviewProvider
{
    string Key { get; }
    IReadOnlySet<string> PolicyVersions { get; }
    IReadOnlySet<ReviewExecutionContext> Contexts { get; }

    ValueTask<AIReviewProviderResponseV1> ReviewAsync(
        AIReviewProviderRequestV1 request,
        CancellationToken cancellationToken);
}

public interface IAIReviewProviderRegistry
{
    IAIReviewProvider Resolve(
        string providerKey,
        string policyVersion,
        ReviewExecutionContext context);
}
