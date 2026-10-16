using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed class PeerReviewStageHandler : IReviewStageHandler
{
    private static readonly IReadOnlySet<ReviewExecutionContext> SupportedContexts =
        new HashSet<ReviewExecutionContext>
        {
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        };

    public ReviewMethod Method => ReviewMethod.PeerReview;
    public string Key => "peer-review";
    public string Version => "1";
    public string? ProviderKey => null;
    public string? ProviderPolicyVersion => null;
    public IReadOnlySet<ReviewExecutionContext> Contexts => SupportedContexts;

    public ValueTask<ReviewStageTransitionV1> StartAsync(
        ReviewStageRequest request,
        CancellationToken cancellationToken)
    {
        var policy = RequirePolicy(request);
        return ValueTask.FromResult(ReviewStageTransitionV1.AwaitingEvidence(
            "peer-review-evidence-required",
            SystemClock.UtcNow.AddMinutes(policy.EvidenceWindowMinutes)));
    }

    public ValueTask<ReviewStageTransitionV1> AcceptEvidenceAsync(
        ReviewStageRequest request,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken)
    {
        if (string.Equals(evidence.EvidenceType, "peer-review-insufficiency-resolution", StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveInsufficiency(request, evidence));
        }

        ValidatePeerEvidence(request, evidence);
        var policy = RequirePolicy(request);
        return policy.MinimumReviewsToFinalize == 1
            ? ValueTask.FromResult(Aggregate(request, [evidence], policy))
            : ValueTask.FromResult(ReviewStageTransitionV1.AwaitingEvidence(
                "peer-review-threshold-not-reached",
                request.EvidenceDeadlineAt));
    }

    public ValueTask<ReviewStageTransitionV1> TryCompleteAsync(
        ReviewStageRequest request,
        IReadOnlyList<ReviewStageEvidenceV1> evidence,
        CancellationToken cancellationToken)
    {
        var resolution = evidence.LastOrDefault(item =>
            string.Equals(item.EvidenceType, "peer-review-insufficiency-resolution", StringComparison.Ordinal));
        if (resolution is not null)
        {
            return ValueTask.FromResult(ResolveInsufficiency(request, resolution));
        }

        var policy = RequirePolicy(request);
        var peerEvidence = evidence
            .Where(item => string.Equals(item.EvidenceType, "peer-review-evidence", StringComparison.Ordinal))
            .ToArray();
        foreach (var item in peerEvidence)
        {
            ValidatePeerEvidence(request, item);
        }

        var distinctReviewers = peerEvidence
            .Select(item => ReadEvidence(item).ReviewerUserId?.ToString("N") ??
                            $"test:{ReadEvidence(item).ReviewerTestRunSubjectId:N}")
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctReviewers != peerEvidence.Length)
        {
            throw new InvalidOperationException("A reviewer can contribute only once to a peer-review stage.");
        }

        if (peerEvidence.Length >= policy.MinimumReviewsToFinalize)
        {
            return ValueTask.FromResult(Aggregate(request, peerEvidence, policy));
        }

        if (request.EvidenceDeadlineAt.HasValue && request.EvidenceDeadlineAt <= SystemClock.UtcNow)
        {
            return ValueTask.FromResult(ReviewStageTransitionV1.AwaitingInstructorResolution(
                "peer-review-evidence-window-expired"));
        }

        return ValueTask.FromResult(ReviewStageTransitionV1.AwaitingEvidence(
            "peer-review-threshold-not-reached",
            request.EvidenceDeadlineAt));
    }

    private ReviewStageTransitionV1 Aggregate(
        ReviewStageRequest request,
        IReadOnlyList<ReviewStageEvidenceV1> evidence,
        PeerReviewPolicyV1 policy)
    {
        var submitted = evidence.Select(ReadEvidence).ToArray();
        var scoreUnits = submitted.Select(item => item.Score.Units).Order().ToArray();
        var aggregateUnits = policy.Aggregation switch
        {
            "mean" => DivideRounded(scoreUnits.Sum(value => (long)value), scoreUnits.Length),
            "median" when scoreUnits.Length % 2 == 1 => scoreUnits[scoreUnits.Length / 2],
            "median" => DivideRounded(
                (long)scoreUnits[(scoreUnits.Length / 2) - 1] + scoreUnits[scoreUnits.Length / 2],
                2),
            _ => throw new InvalidOperationException($"Unsupported peer aggregation '{policy.Aggregation}'."),
        };
        var maximums = ReadMaximums(request);
        var aggregate = ScoreValue.FromUnits(checked((int)aggregateUnits));
        var totalMaximum = ScoreValue.Sum(maximums.Values);
        if (aggregate.CompareTo(totalMaximum) > 0)
        {
            throw new InvalidOperationException("Aggregated peer score exceeds the assessment maximum.");
        }

        var allocated = Allocate(aggregate, maximums);
        var evidenceKeys = evidence.Select(item => item.EvidenceKey).ToArray();
        var feedback = string.Join(
            "\n\n",
            submitted.Select(item => item.Feedback.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal));
        var results = maximums.Select(item => new GradeItemResultV1(
            item.Key,
            GradeItemState.Graded,
            allocated[item.Key],
            item.Value,
            evidenceKeys,
            ReviewMethod.PeerReview,
            Key,
            Version,
            feedback.Length == 0 ? null : feedback)).ToArray();
        return ReviewStageTransitionV1.Completed(new GradeResultV1(
            GradingContractVersions.GradeResult,
            "final",
            aggregate,
            totalMaximum,
            results,
            evidenceKeys,
            feedback.Length == 0 ? null : feedback));
    }

    private ReviewStageTransitionV1 ResolveInsufficiency(
        ReviewStageRequest request,
        ReviewStageEvidenceV1 evidence)
    {
        if (!evidence.ProducedByActorId.HasValue || evidence.ProducedByActorId == Guid.Empty ||
            !string.Equals(evidence.SchemaVersion, "1", StringComparison.Ordinal))
        {
            throw new ArgumentException("Peer insufficiency resolution requires authenticated instructor evidence.", nameof(evidence));
        }

        var resolution = evidence.Payload.Deserialize<PeerReviewInsufficiencyResolutionV1>(GradingJson.Options)
            ?? throw new JsonException("Peer insufficiency resolution payload is invalid.");
        if (resolution.SchemaVersion != 1 || string.IsNullOrWhiteSpace(resolution.Reason))
        {
            throw new ArgumentException("Peer insufficiency resolution requires schemaVersion 1 and a reason.", nameof(evidence));
        }

        var maximums = ReadMaximums(request);
        var resolved = resolution.Items.ToDictionary(item => RequireItemId(item.ItemId), StringComparer.Ordinal);
        if (!maximums.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(resolved.Keys))
        {
            throw new ArgumentException("Peer insufficiency resolution must contain every item exactly once.", nameof(evidence));
        }

        foreach (var (itemId, item) in resolved)
        {
            if (item.Score.CompareTo(ScoreValue.Zero) < 0 || item.Score.CompareTo(maximums[itemId]) > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evidence), $"Resolved score for {itemId} is outside its maximum.");
            }
        }

        var results = maximums.Select(item => new GradeItemResultV1(
            item.Key,
            GradeItemState.Graded,
            resolved[item.Key].Score,
            item.Value,
            [evidence.EvidenceKey],
            ReviewMethod.PeerReview,
            Key,
            Version,
            resolved[item.Key].Feedback)).ToArray();
        return ReviewStageTransitionV1.Completed(new GradeResultV1(
            GradingContractVersions.GradeResult,
            "final",
            ScoreValue.Sum(results.Select(item => item.Score!.Value)),
            ScoreValue.Sum(results.Select(item => item.MaxScore)),
            results,
            [evidence.EvidenceKey],
            resolution.Feedback));
    }

    private static void ValidatePeerEvidence(ReviewStageRequest request, ReviewStageEvidenceV1 evidence)
    {
        if (!string.Equals(evidence.EvidenceType, "peer-review-evidence", StringComparison.Ordinal) ||
            !string.Equals(evidence.SchemaVersion, "1", StringComparison.Ordinal))
        {
            throw new ArgumentException("PeerReview requires peer-review-evidence schemaVersion 1.", nameof(evidence));
        }

        var submitted = ReadEvidence(evidence);
        if (submitted.SchemaVersion != 1 || submitted.ClaimId == Guid.Empty || string.IsNullOrWhiteSpace(submitted.Feedback))
        {
            throw new ArgumentException("Peer review claim, score, and feedback are required.", nameof(evidence));
        }

        var official = request.Context == ReviewExecutionContext.OfficialSubmission;
        if (official != submitted.ReviewerUserId.HasValue || official == submitted.ReviewerTestRunSubjectId.HasValue)
        {
            throw new ArgumentException("Peer reviewer identity does not match the execution context.", nameof(evidence));
        }

        if (official && evidence.ProducedByActorId != submitted.ReviewerUserId)
        {
            throw new ArgumentException("Peer evidence actor does not match its reviewer.", nameof(evidence));
        }

        if (!official && !string.Equals(
                evidence.ProducedByService,
                $"author-test-subject:{submitted.ReviewerTestRunSubjectId:N}",
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Peer evidence test subject does not match its producer.", nameof(evidence));
        }

        var maximum = ScoreValue.Sum(ReadMaximums(request).Values);
        if (submitted.Score.CompareTo(ScoreValue.Zero) < 0 || submitted.Score.CompareTo(maximum) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidence), "Peer score is outside the assessment maximum.");
        }
    }

    private static PeerReviewEvidenceV1 ReadEvidence(ReviewStageEvidenceV1 evidence) =>
        evidence.Payload.Deserialize<PeerReviewEvidenceV1>(GradingJson.Options)
        ?? throw new JsonException("Peer review evidence payload is invalid.");

    private static PeerReviewPolicyV1 RequirePolicy(ReviewStageRequest request) =>
        request.Snapshot.AuthoringSource.Policy.Review.Peer
        ?? throw new InvalidOperationException("PeerReview stage is missing its frozen policy.");

    private static IReadOnlyDictionary<string, ScoreValue> ReadMaximums(ReviewStageRequest request) =>
        request.Snapshot.Manifest.Items.ToDictionary(
            item => item.ItemId,
            item => ScoreValue.FromUnits(request.Snapshot.ItemProjections[item.ItemId]
                .GetProperty("maxScore")
                .GetInt32()),
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, ScoreValue> Allocate(
        ScoreValue total,
        IReadOnlyDictionary<string, ScoreValue> maximums)
    {
        var totalMaximum = maximums.Values.Sum(value => value.Units);
        var remaining = total.Units;
        var remainingMaximum = totalMaximum;
        var ordered = maximums.ToArray();
        var result = new Dictionary<string, ScoreValue>(StringComparer.Ordinal);
        for (var index = 0; index < ordered.Length; index++)
        {
            var (itemId, maximum) = ordered[index];
            var units = index == ordered.Length - 1
                ? remaining
                : Math.Min(maximum.Units, checked((int)(((long)remaining * maximum.Units) / remainingMaximum)));
            result[itemId] = ScoreValue.FromUnits(units);
            remaining -= units;
            remainingMaximum -= maximum.Units;
        }

        return result;
    }

    private static long DivideRounded(long numerator, int denominator) =>
        (numerator + (denominator / 2)) / denominator;

    private static string RequireItemId(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Resolved peer item ID is required.")
            : value.Trim();
}
