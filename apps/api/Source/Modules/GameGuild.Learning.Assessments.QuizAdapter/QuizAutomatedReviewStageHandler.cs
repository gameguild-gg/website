using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizAutomatedReviewStageHandler(QuizAssessmentTypeAdapter adapter) : IReviewStageHandler
{
    public ReviewMethod Method => ReviewMethod.AutomatedReview;
    public string Key => QuizAdapterContracts.AutomatedReviewHandlerKey;
    public string Version => QuizAdapterContracts.Version;
    public string? ProviderKey => null;
    public string? ProviderPolicyVersion => null;
    public IReadOnlySet<ReviewExecutionContext> Contexts => adapter.Contexts;

    public async ValueTask<ReviewStageTransitionV1> StartAsync(
        ReviewStageRequest request,
        CancellationToken cancellationToken)
    {
        GradingContractValidator.ValidateBindings(
            request.ExecutionSnapshotHash,
            request.Snapshot,
            request.Delivery,
            request.Response);

        var manifestItems = request.Snapshot.Manifest.Items;
        if (manifestItems.Any(item =>
                !string.Equals(item.AdapterKey, adapter.Key, StringComparison.Ordinal) ||
                !string.Equals(item.AdapterVersion, adapter.Version, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Quiz automated review received an item owned by another assessment adapter.");
        }

        var projections = manifestItems
            .Select(item => request.Snapshot.ItemProjections[item.ItemId])
            .ToArray();
        var normalizedResponse = adapter.DecodeResponse(request.Response, projections);
        var result = await adapter.EvaluateDeterministicAsync(
            new DeterministicReviewRequest(
                projections,
                request.Delivery,
                normalizedResponse,
                Key,
                Version),
            cancellationToken).ConfigureAwait(false);
        return ReviewStageTransitionV1.Completed(result);
    }

    public ValueTask<ReviewStageTransitionV1> AcceptEvidenceAsync(
        ReviewStageRequest request,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken) =>
        ValueTask.FromException<ReviewStageTransitionV1>(
            new InvalidOperationException("AutomatedReview does not accept external evidence."));

    public ValueTask<ReviewStageTransitionV1> TryCompleteAsync(
        ReviewStageRequest request,
        IReadOnlyList<ReviewStageEvidenceV1> evidence,
        CancellationToken cancellationToken) => StartAsync(request, cancellationToken);
}
