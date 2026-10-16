using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed class SelfReviewStageHandler : IReviewStageHandler
{
    private static readonly IReadOnlySet<ReviewExecutionContext> SupportedContexts =
        new HashSet<ReviewExecutionContext>
        {
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        };

    public ReviewMethod Method => ReviewMethod.SelfReview;
    public string Key => "self-review";
    public string Version => "1";
    public string? ProviderKey => null;
    public string? ProviderPolicyVersion => null;
    public IReadOnlySet<ReviewExecutionContext> Contexts => SupportedContexts;

    public ValueTask<ReviewStageTransitionV1> StartAsync(
        ReviewStageRequest request,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ReviewStageTransitionV1.AwaitingEvidence("self-review-required"));

    public ValueTask<ReviewStageTransitionV1> AcceptEvidenceAsync(
        ReviewStageRequest request,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(evidence.EvidenceType, "self-review-evidence", StringComparison.Ordinal) ||
            !string.Equals(evidence.SchemaVersion, "1", StringComparison.Ordinal) ||
            !evidence.ProducedByActorId.HasValue ||
            evidence.ProducedByActorId == Guid.Empty)
        {
            throw new ArgumentException("SelfReview requires authenticated subject evidence.", nameof(evidence));
        }

        var submitted = evidence.Payload.Deserialize<SelfReviewEvidenceV1>(GradingJson.Options)
            ?? throw new JsonException("Self review evidence payload is invalid.");
        if (submitted.SchemaVersion != 1)
        {
            throw new ArgumentException("Self review schemaVersion must be 1.", nameof(evidence));
        }

        var maximums = request.Snapshot.Manifest.Items.ToDictionary(
            item => item.ItemId,
            item => ReadProjectionScore(request.Snapshot.ItemProjections[item.ItemId]),
            StringComparer.Ordinal);
        var submittedItems = submitted.Items.ToDictionary(
            item => RequireItemId(item.ItemId),
            StringComparer.Ordinal);
        if (!maximums.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(submittedItems.Keys))
        {
            throw new ArgumentException("Self review must contain every item exactly once.", nameof(evidence));
        }

        var requireFeedback = request.Snapshot.AuthoringSource.Policy.Review.Self?.RequireFeedback == true;
        foreach (var (itemId, item) in submittedItems)
        {
            if (item.Score.CompareTo(ScoreValue.Zero) < 0 || item.Score.CompareTo(maximums[itemId]) > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evidence), $"Self review score for {itemId} is outside its maximum.");
            }

            if (requireFeedback && string.IsNullOrWhiteSpace(item.Feedback) && string.IsNullOrWhiteSpace(submitted.Feedback))
            {
                throw new ArgumentException($"Self review feedback is required for {itemId}.", nameof(evidence));
            }
        }

        var results = request.Snapshot.Manifest.Items.Select(item => new GradeItemResultV1(
            item.ItemId,
            GradeItemState.Graded,
            submittedItems[item.ItemId].Score,
            maximums[item.ItemId],
            [evidence.EvidenceKey],
            ReviewMethod.SelfReview,
            Key,
            Version,
            submittedItems[item.ItemId].Feedback)).ToArray();
        return ValueTask.FromResult(ReviewStageTransitionV1.Completed(new GradeResultV1(
            GradingContractVersions.GradeResult,
            "final",
            ScoreValue.Sum(results.Select(item => item.Score!.Value)),
            ScoreValue.Sum(results.Select(item => item.MaxScore)),
            results,
            [evidence.EvidenceKey],
            submitted.Feedback)));
    }

    public ValueTask<ReviewStageTransitionV1> TryCompleteAsync(
        ReviewStageRequest request,
        IReadOnlyList<ReviewStageEvidenceV1> evidence,
        CancellationToken cancellationToken)
    {
        var submitted = evidence.LastOrDefault(item =>
            string.Equals(item.EvidenceType, "self-review-evidence", StringComparison.Ordinal));
        return submitted is null
            ? ValueTask.FromResult(ReviewStageTransitionV1.AwaitingEvidence("self-review-required"))
            : AcceptEvidenceAsync(request, submitted, cancellationToken);
    }

    private static ScoreValue ReadProjectionScore(JsonElement projection)
    {
        var value = projection.GetProperty("maxScore");
        return ScoreValue.FromUnits(value.TryGetInt32(out var units)
            ? units
            : throw new JsonException("Assessment item maxScore must be an integer."));
    }

    private static string RequireItemId(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Self review item ID is required.")
            : value.Trim();
}
