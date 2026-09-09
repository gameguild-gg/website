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

    AssessmentAuthoringProjectionV1 ProjectAuthoring(JsonElement authoringDocument);
    JsonElement GenerateDelivery(JsonElement projectedItem);
    JsonElement DecodeResponse(AssessmentResponseEnvelopeV1 envelope);
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
    ValueTask<GradeResultV1> ExecuteAsync(ReviewStageRequest request, CancellationToken cancellationToken);
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
    ReviewExecutionContext Context,
    string ExecutionSnapshotHash,
    AssessmentExecutionSnapshotV1 Snapshot,
    AssessmentExecutionDeliveryV1 Delivery,
    AssessmentResponseEnvelopeV1 Response,
    GradeResultV1? PreviousStageResult);
