using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Code;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed class InstructorReviewStageHandler : IReviewStageHandler
{
    private static readonly IReadOnlySet<ReviewExecutionContext> SupportedContexts =
        new HashSet<ReviewExecutionContext>
        {
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        };

    public ReviewMethod Method => ReviewMethod.InstructorReview;
    public string Key => "instructor-review";
    public string Version => "1";
    public string? ProviderKey => null;
    public string? ProviderPolicyVersion => null;
    public IReadOnlySet<ReviewExecutionContext> Contexts => SupportedContexts;

    public ValueTask<ReviewStageTransitionV1> StartAsync(
        ReviewStageRequest request,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ReviewStageTransitionV1.AwaitingInstructorResolution());

    public ValueTask<ReviewStageTransitionV1> AcceptEvidenceAsync(
        ReviewStageRequest request,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(evidence.EvidenceType, "instructor-review-evidence", StringComparison.Ordinal) ||
            !string.Equals(evidence.SchemaVersion, "1", StringComparison.Ordinal) ||
            !evidence.ProducedByActorId.HasValue ||
            evidence.ProducedByActorId == Guid.Empty)
        {
            throw new ArgumentException("InstructorReview requires authenticated instructor evidence.", nameof(evidence));
        }

        var payload = evidence.Payload.Deserialize<InstructorReviewEvidencePayloadV1>(GradingJson.Options)
            ?? throw new JsonException("Instructor review evidence payload is invalid.");
        if (payload.ExecutionId != request.GradingExecutionId ||
            payload.RoundId != request.GradeRoundId ||
            payload.ActorId != evidence.ProducedByActorId)
        {
            throw new ArgumentException("Instructor review evidence does not belong to the active execution.", nameof(evidence));
        }

        var resolution = new InstructorReviewResolutionV1(
            payload.SchemaVersion,
            payload.Items,
            payload.Feedback,
            payload.OverrideReason,
            payload.RubricScores);
        if (resolution.SchemaVersion != 1)
        {
            throw new ArgumentException("Instructor review schemaVersion must be 1.", nameof(evidence));
        }

        var projections = request.Snapshot.Manifest.Items.ToDictionary(
            item => item.ItemId,
            item => ReadProjectionScore(request.Snapshot.ItemProjections[item.ItemId]),
            StringComparer.Ordinal);
        var resolutions = resolution.Items.ToDictionary(item => RequireItemId(item.ItemId), StringComparer.Ordinal);
        if (!projections.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(resolutions.Keys))
        {
            throw new ArgumentException("Instructor resolution must contain every item exactly once.", nameof(evidence));
        }

        foreach (var (itemId, item) in resolutions)
        {
            if (item.Score.CompareTo(ScoreValue.Zero) < 0 || item.Score.CompareTo(projections[itemId]) > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evidence), $"Instructor score for {itemId} is outside its maximum.");
            }
        }

        CodeRubricSnapshot.ValidateResolution(request.Snapshot, resolution);
        var previousCodeFeedback = request.Snapshot.AuthoringSource.ContentType == CodeAssessmentContracts.ContentType
            ? request.PreviousStageResult?.Items.SingleOrDefault(item =>
                item.ItemId == CodeAssessmentContracts.ItemId &&
                item.ReviewMethod == ReviewMethod.AutomatedReview &&
                item.HandlerKey == CodeAssessmentContracts.HandlerKey &&
                item.HandlerVersion == CodeAssessmentContracts.Version)?.Feedback
            : null;
        string? ResolveFeedback(string? supplied) =>
            previousCodeFeedback is not null && string.IsNullOrWhiteSpace(supplied)
                ? previousCodeFeedback
                : supplied;

        var changedPreviousScore = request.PreviousStageResult is not null &&
            request.PreviousStageResult.Items.Any(previousItem =>
                previousItem.Score.HasValue && resolutions[previousItem.ItemId].Score != previousItem.Score.Value);
        if (changedPreviousScore &&
            request.Snapshot.AuthoringSource.Policy.Review.Instructor?.RequireOverrideReason == true &&
            string.IsNullOrWhiteSpace(resolution.OverrideReason))
        {
            throw new ArgumentException(
                "An override reason is required when changing a previous review result.",
                nameof(evidence));
        }

        var itemResults = request.Snapshot.Manifest.Items.Select(item => new GradeItemResultV1(
            item.ItemId,
            GradeItemState.Graded,
            resolutions[item.ItemId].Score,
            projections[item.ItemId],
            [evidence.EvidenceKey],
            ReviewMethod.InstructorReview,
            Key,
            Version,
            ResolveFeedback(resolutions[item.ItemId].Feedback))).ToArray();
        var result = new GradeResultV1(
            GradingContractVersions.GradeResult,
            "final",
            ScoreValue.Sum(itemResults.Select(item => item.Score!.Value)),
            ScoreValue.Sum(itemResults.Select(item => item.MaxScore)),
            itemResults,
            [evidence.EvidenceKey],
            ResolveFeedback(resolution.Feedback));
        GradingContractValidator.Validate(result);
        return ValueTask.FromResult(ReviewStageTransitionV1.Completed(result));
    }

    public ValueTask<ReviewStageTransitionV1> TryCompleteAsync(
        ReviewStageRequest request,
        IReadOnlyList<ReviewStageEvidenceV1> evidence,
        CancellationToken cancellationToken)
    {
        var resolution = evidence.LastOrDefault(item =>
            string.Equals(item.EvidenceType, "instructor-review-evidence", StringComparison.Ordinal));
        return resolution is null
            ? ValueTask.FromResult(ReviewStageTransitionV1.AwaitingInstructorResolution())
            : AcceptEvidenceAsync(request, resolution, cancellationToken);
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
            ? throw new ArgumentException("Instructor item ID is required.")
            : value.Trim();

    private sealed record InstructorReviewEvidencePayloadV1(
        int SchemaVersion,
        Guid ExecutionId,
        Guid RoundId,
        Guid ActorId,
        IReadOnlyList<InstructorItemResolutionV1> Items,
        string? Feedback,
        string? OverrideReason,
        IReadOnlyDictionary<Guid, ScoreValue>? RubricScores,
        GradeResultV1? PreviousResult);
}
