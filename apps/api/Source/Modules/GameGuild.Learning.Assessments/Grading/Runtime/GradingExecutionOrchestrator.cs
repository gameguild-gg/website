using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public interface IGradingFinalizationSink
{
    Task OnFinalizedAsync(
        GradingExecution execution,
        GradeRound round,
        GradeResultV1 result,
        AssessmentExecutionSnapshotV1 snapshot,
        CancellationToken cancellationToken);
}

public interface IGradingExecutionOrchestrator
{
    AssessmentExecutionDeliveryV1 MaterializeDelivery(
        GradingExecution execution,
        AssessmentDefinitionRevision revision);

    void ValidateResponse(
        GradingExecution execution,
        AssessmentDefinitionRevision revision,
        AssessmentResponseEnvelopeV1 response);

    Task<GradingExecutionOutcome> SubmitAsync(
        Guid executionId,
        AssessmentResponseEnvelopeV1 response,
        CancellationToken cancellationToken = default);

    Task<GradingExecutionOutcome> ResolveInstructorReviewAsync(
        Guid executionId,
        Guid actorId,
        InstructorReviewResolutionV1 resolution,
        CancellationToken cancellationToken = default);

    Task<GradingExecutionOutcome> RegradeAsync(
        Guid executionId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class GradingExecutionOrchestrator(
    IApplicationDbContext context,
    IAssessmentTypeAdapterResolver assessmentTypeAdapters,
    IReviewStageHandlerResolver stageHandlers,
    IGradingFinalizationSink finalizationSink) : IGradingExecutionOrchestrator
{
    public AssessmentExecutionDeliveryV1 MaterializeDelivery(
        GradingExecution execution,
        AssessmentDefinitionRevision revision)
    {
        if (execution.DefinitionRevisionId != revision.Id)
            throw new InvalidOperationException("Execution and revision do not match.");
        if (execution.DeliveryCanonicalJson is not null)
        {
            return Deserialize<AssessmentExecutionDeliveryV1>(execution.DeliveryCanonicalJson, "execution delivery");
        }

        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var items = new Dictionary<string, AssessmentExecutionDeliveryItemV1>(StringComparer.Ordinal);
        foreach (var manifestItem in snapshot.Manifest.Items)
        {
            var adapter = assessmentTypeAdapters.Resolve(
                snapshot.AuthoringSource.ContentType,
                manifestItem.AdapterKey,
                manifestItem.AdapterVersion,
                execution.ExecutionContext);
            var learnerPayload = adapter.GenerateDelivery(snapshot.ItemProjections[manifestItem.ItemId]);
            items.Add(
                manifestItem.ItemId,
                new AssessmentExecutionDeliveryItemV1(
                    manifestItem.AdapterKey,
                    manifestItem.AdapterVersion,
                    learnerPayload));
        }

        var delivery = new AssessmentExecutionDeliveryV1(
            GradingContractVersions.ExecutionDelivery,
            revision.Id,
            revision.ExecutionSnapshotHash,
            snapshot.Manifest.Items.Select(item => item.ItemId).ToArray(),
            items);
        var canonical = Serialize(delivery);
        execution.MaterializeDelivery(delivery, canonical);
        return delivery;
    }

    public async Task<GradingExecutionOutcome> SubmitAsync(
        Guid executionId,
        AssessmentResponseEnvelopeV1 response,
        CancellationToken cancellationToken = default)
    {
        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        var revision = await RequireRevisionAsync(execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = MaterializeDelivery(execution, revision);
        ValidateResponse(execution, revision, response);

        if (execution.SubmittedAt.HasValue)
        {
            var incomingHash = CanonicalPayload.Hash(Serialize(response));
            if (!string.Equals(execution.ResponseHash, incomingHash, StringComparison.Ordinal))
                throw new InvalidOperationException("The execution was already submitted with a different response.");
            return await ResumeAsync(execution, revision, snapshot, delivery, cancellationToken).ConfigureAwait(false);
        }

        execution.SaveResponseDraft(response, Serialize(response));
        execution.Submit(SystemClock.UtcNow);
        var round = CreateRound(execution, snapshot, GradeRoundReasonV1.Initial, null, 1);
        context.Set<GradeRound>().Add(round);
        AddStages(round, snapshot);
        execution.SetActiveRound(round.Id);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await RunAvailableStagesAsync(execution, revision, snapshot, delivery, round, cancellationToken)
            .ConfigureAwait(false);
    }

    public void ValidateResponse(
        GradingExecution execution,
        AssessmentDefinitionRevision revision,
        AssessmentResponseEnvelopeV1 response)
    {
        if (execution.DefinitionRevisionId != revision.Id)
            throw new InvalidOperationException("Execution and revision do not match.");
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = MaterializeDelivery(execution, revision);
        GradingContractValidator.ValidateBindings(revision.ExecutionSnapshotHash, snapshot, delivery, response);
        var bindings = snapshot.Manifest.Items
            .Select(item => (item.AdapterKey, item.AdapterVersion))
            .Distinct()
            .ToArray();
        if (bindings.Length != 1)
            throw new InvalidOperationException("An assessment response must be owned by exactly one assessment-type adapter.");
        var adapter = assessmentTypeAdapters.Resolve(
            snapshot.AuthoringSource.ContentType,
            bindings[0].AdapterKey,
            bindings[0].AdapterVersion,
            execution.ExecutionContext);
        adapter.DecodeResponse(
            response,
            snapshot.Manifest.Items.Select(item => snapshot.ItemProjections[item.ItemId]).ToArray());
    }

    public async Task<GradingExecutionOutcome> ResolveInstructorReviewAsync(
        Guid executionId,
        Guid actorId,
        InstructorReviewResolutionV1 resolution,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID is required.", nameof(actorId));
        if (resolution.SchemaVersion != 1) throw new ArgumentException("Instructor review schemaVersion must be 1.", nameof(resolution));
        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        var revision = await RequireRevisionAsync(execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var round = await RequireActiveRoundAsync(execution, cancellationToken).ConfigureAwait(false);
        if (round.Status == PersistedGradeRoundStatus.Finalized)
            return await OutcomeAsync(execution, round, cancellationToken).ConfigureAwait(false);

        var stage = await context.Set<ReviewStage>()
            .Where(value => value.GradeRoundId == round.Id && value.ReviewMethod == ReviewMethod.InstructorReview)
            .OrderBy(value => value.Sequence)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The active round does not contain an InstructorReview stage.");
        if (stage.Status != PersistedReviewStageStatus.AwaitingInstructorResolution)
            throw new InvalidOperationException("The instructor stage is not awaiting resolution.");

        var projections = snapshot.Manifest.Items.ToDictionary(
            item => item.ItemId,
            item => ReadProjectionScore(snapshot.ItemProjections[item.ItemId]),
            StringComparer.Ordinal);
        var resolutions = resolution.Items.ToDictionary(item => RequireItemId(item.ItemId), StringComparer.Ordinal);
        if (!projections.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(resolutions.Keys))
            throw new ArgumentException("Instructor resolution must contain every item exactly once.", nameof(resolution));
        foreach (var (itemId, item) in resolutions)
        {
            if (item.Score.CompareTo(ScoreValue.Zero) < 0 || item.Score.CompareTo(projections[itemId]) > 0)
                throw new ArgumentOutOfRangeException(nameof(resolution), $"Instructor score for {itemId} is outside its maximum.");
        }

        Code.CodeRubricSnapshot.ValidateResolution(snapshot, resolution);
        var previous = await PreviousStageResultAsync(round.Id, stage.Sequence, cancellationToken).ConfigureAwait(false);
        // Code's trusted worker emits redacted feedback on its single persisted item.
        // An omitted optional instructor comment must not erase that feedback.
        var previousCodeFeedback = snapshot.AuthoringSource.ContentType == Code.CodeAssessmentContracts.ContentType
            ? previous?.Items.SingleOrDefault(item => item.ItemId == Code.CodeAssessmentContracts.ItemId &&
                item.ReviewMethod == ReviewMethod.AutomatedReview &&
                item.HandlerKey == Code.CodeAssessmentContracts.HandlerKey &&
                item.HandlerVersion == Code.CodeAssessmentContracts.Version)?.Feedback
            : null;
        string? ResolveFeedback(string? supplied) => previousCodeFeedback is not null && string.IsNullOrWhiteSpace(supplied)
            ? previousCodeFeedback : supplied;
        var changedAutomatedScore = previous is not null && previous.Items.Any(previousItem =>
            previousItem.Score.HasValue && resolutions[previousItem.ItemId].Score != previousItem.Score.Value);
        if (changedAutomatedScore &&
            snapshot.AuthoringSource.Policy.Review.Instructor?.RequireOverrideReason == true &&
            string.IsNullOrWhiteSpace(resolution.OverrideReason))
        {
            throw new ArgumentException("An override reason is required when changing a previous review result.", nameof(resolution));
        }

        var evidenceKey = $"instructor-resolution:{round.RoundNumber}";
        var evidencePayload = new
        {
            schemaVersion = 1,
            executionId,
            roundId = round.Id,
            actorId,
            resolution.Items,
            resolution.Feedback,
            resolution.OverrideReason,
            resolution.RubricScores,
            previousResult = previous,
        };
        var evidenceCanonical = Serialize(evidencePayload);
        context.Set<ReviewEvidence>().Add(ReviewEvidence.CreateForActor(
            execution.TenantId,
            stage.Id,
            evidenceKey,
            "instructor-review-evidence",
            "1",
            evidenceCanonical,
            actorId));

        var itemResults = snapshot.Manifest.Items.Select(item => new GradeItemResultV1(
            item.ItemId,
            GradeItemState.Graded,
            resolutions[item.ItemId].Score,
            projections[item.ItemId],
            [evidenceKey],
            ReviewMethod.InstructorReview,
            stage.HandlerKey,
            stage.HandlerVersion,
            ResolveFeedback(resolutions[item.ItemId].Feedback))).ToArray();
        var result = new GradeResultV1(
            GradingContractVersions.GradeResult,
            "final",
            ScoreValue.Sum(itemResults.Select(item => item.Score!.Value)),
            ScoreValue.Sum(itemResults.Select(item => item.MaxScore)),
            itemResults,
            [evidenceKey],
            ResolveFeedback(resolution.Feedback));
        GradingContractValidator.Validate(result);
        PersistStageResult(execution, stage, result);
        stage.Complete(SystemClock.UtcNow);
        await FinalizeAsync(execution, round, result, snapshot, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToOutcome(execution, round, result);
    }

    public async Task<GradingExecutionOutcome> RegradeAsync(
        Guid executionId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Regrade reason is required.", nameof(reason));
        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        if (execution.Status != PersistedGradingExecutionStatus.Completed || !execution.ActiveGradeRoundId.HasValue)
            throw new InvalidOperationException("Only a completed execution can be regraded.");
        var previous = await RequireActiveRoundAsync(execution, cancellationToken).ConfigureAwait(false);
        var revision = await RequireRevisionAsync(execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = MaterializeDelivery(execution, revision);
        var roundNumber = await context.Set<GradeRound>()
            .Where(value => value.GradingExecutionId == execution.Id)
            .MaxAsync(value => value.RoundNumber, cancellationToken)
            .ConfigureAwait(false) + 1;
        var round = CreateRound(
            execution,
            snapshot,
            GradeRoundReasonV1.Regrade,
            previous.Id,
            roundNumber,
            actorId,
            reason);
        context.Set<GradeRound>().Add(round);
        AddStages(round, snapshot);
        execution.BeginRegrade(round.Id);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await RunAvailableStagesAsync(execution, revision, snapshot, delivery, round, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<GradingExecutionOutcome> ResumeAsync(
        GradingExecution execution,
        AssessmentDefinitionRevision revision,
        AssessmentExecutionSnapshotV1 snapshot,
        AssessmentExecutionDeliveryV1 delivery,
        CancellationToken cancellationToken)
    {
        var round = await RequireActiveRoundAsync(execution, cancellationToken).ConfigureAwait(false);
        if (round.Status == PersistedGradeRoundStatus.Finalized)
            return await OutcomeAsync(execution, round, cancellationToken).ConfigureAwait(false);
        return await RunAvailableStagesAsync(execution, revision, snapshot, delivery, round, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<GradingExecutionOutcome> RunAvailableStagesAsync(
        GradingExecution execution,
        AssessmentDefinitionRevision revision,
        AssessmentExecutionSnapshotV1 snapshot,
        AssessmentExecutionDeliveryV1 delivery,
        GradeRound round,
        CancellationToken cancellationToken)
    {
        var response = Deserialize<AssessmentResponseEnvelopeV1>(
            execution.ResponseEnvelopeCanonicalJson ?? throw new InvalidOperationException("Submitted response is missing."),
            "response envelope");
        var stages = await context.Set<ReviewStage>()
            .Where(value => value.GradeRoundId == round.Id)
            .OrderBy(value => value.Sequence)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        GradeResultV1? previousResult = null;
        foreach (var stage in stages)
        {
            if (stage.Status == PersistedReviewStageStatus.Completed)
            {
                previousResult = await StageResultAsync(stage, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (stage.ReviewMethod == ReviewMethod.InstructorReview)
            {
                stageHandlers.Resolve(stage.ReviewMethod, stage.HandlerKey, stage.HandlerVersion, execution.ExecutionContext);
                stage.AwaitInstructorResolution();
                round.AwaitInstructorResolution();
                execution.AwaitReview();
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return ToOutcome(execution, round, previousResult, true);
            }

            var handler = stageHandlers.Resolve(
                stage.ReviewMethod,
                stage.HandlerKey,
                stage.HandlerVersion,
                execution.ExecutionContext);
            stage.Start(SystemClock.UtcNow);
            var result = await handler.ExecuteAsync(
                new ReviewStageRequest(
                    execution.Id,
                    round.Id,
                    execution.ExecutionContext,
                    revision.ExecutionSnapshotHash,
                    snapshot,
                    delivery,
                    response,
                    previousResult),
                cancellationToken).ConfigureAwait(false);
            GradingContractValidator.Validate(result);
            ValidateStageResult(snapshot, stage, result);
            PersistStageResult(execution, stage, result);
            context.Set<ReviewEvidence>().Add(ReviewEvidence.CreateForService(
                execution.TenantId,
                stage.Id,
                $"{stage.HandlerKey}:{stage.HandlerVersion}",
                "review-stage-result",
                "1",
                Serialize(result),
                $"{stage.HandlerKey}@{stage.HandlerVersion}"));
            stage.Complete(SystemClock.UtcNow);
            previousResult = result;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (previousResult is null)
            throw new InvalidOperationException("The grading workflow did not produce a result.");
        if (!string.Equals(previousResult.State, "final", StringComparison.Ordinal))
        {
            round.AwaitEvidence();
            execution.AwaitReview();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToOutcome(execution, round, previousResult);
        }

        await FinalizeAsync(execution, round, previousResult, snapshot, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToOutcome(execution, round, previousResult);
    }

    private async Task FinalizeAsync(
        GradingExecution execution,
        GradeRound round,
        GradeResultV1 result,
        AssessmentExecutionSnapshotV1 snapshot,
        CancellationToken cancellationToken)
    {
        var finalizedAt = SystemClock.UtcNow;
        round.Finalize(result, finalizedAt);
        execution.Complete(finalizedAt);
        if (execution.ExecutionContext == ReviewExecutionContext.AuthorTest)
        {
            var subject = await context.Set<AssessmentTestRunSubject>()
                .SingleAsync(value => value.Id == execution.TestRunSubjectId, cancellationToken)
                .ConfigureAwait(false);
            var run = await context.Set<AssessmentTestRun>()
                .SingleAsync(value => value.Id == subject.TestRunId, cancellationToken)
                .ConfigureAwait(false);
            run.Complete(finalizedAt);
            return;
        }

        await finalizationSink.OnFinalizedAsync(execution, round, result, snapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    private GradeRound CreateRound(
        GradingExecution execution,
        AssessmentExecutionSnapshotV1 snapshot,
        GradeRoundReasonV1 reason,
        Guid? supersedesRoundId,
        int roundNumber,
        Guid? initiatedByActorId = null,
        string? reasonDetail = null)
    {
        var maxScore = ScoreValue.Sum(snapshot.Manifest.Items.Select(item =>
            ReadProjectionScore(snapshot.ItemProjections[item.ItemId])));
        var round = GradeRound.Create(
            execution.TenantId,
            execution.Id,
            roundNumber,
            maxScore,
            reason == GradeRoundReasonV1.Initial ? "initial" : "regrade",
            supersedesRoundId,
            initiatedByActorId,
            reasonDetail);
        round.Start();
        return round;
    }

    private void AddStages(GradeRound round, AssessmentExecutionSnapshotV1 snapshot)
    {
        for (var index = 0; index < snapshot.Manifest.Stages.Count; index++)
        {
            context.Set<ReviewStage>().Add(ReviewStage.Create(
                round.TenantId,
                round.Id,
                index + 1,
                snapshot.Manifest.Stages[index]));
        }
    }

    private void PersistStageResult(GradingExecution execution, ReviewStage stage, GradeResultV1 result)
    {
        foreach (var item in result.Items)
        {
            context.Set<GradeItemResult>().Add(GradeItemResult.Create(execution.TenantId, stage.Id, item));
        }
    }

    private static void ValidateStageResult(
        AssessmentExecutionSnapshotV1 snapshot,
        ReviewStage stage,
        GradeResultV1 result)
    {
        var expected = snapshot.Manifest.Items.ToDictionary(
            item => item.ItemId,
            item => ReadProjectionScore(snapshot.ItemProjections[item.ItemId]),
            StringComparer.Ordinal);
        if (!expected.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(result.Items.Select(item => item.ItemId)))
            throw new InvalidOperationException("Review result items do not match the immutable manifest.");
        foreach (var item in result.Items)
        {
            if (item.MaxScore != expected[item.ItemId] ||
                item.ReviewMethod != stage.ReviewMethod ||
                !string.Equals(item.HandlerKey, stage.HandlerKey, StringComparison.Ordinal) ||
                !string.Equals(item.HandlerVersion, stage.HandlerVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Review result for {item.ItemId} does not match its stage or projection.");
            }
        }
    }

    private async Task<GradeResultV1?> PreviousStageResultAsync(
        Guid roundId,
        int sequence,
        CancellationToken cancellationToken)
    {
        var previous = await context.Set<ReviewStage>()
            .Where(value => value.GradeRoundId == roundId && value.Sequence < sequence &&
                            value.Status == PersistedReviewStageStatus.Completed)
            .OrderByDescending(value => value.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return previous is null ? null : await StageResultAsync(previous, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GradeResultV1> StageResultAsync(
        ReviewStage stage,
        CancellationToken cancellationToken)
    {
        var items = await context.Set<GradeItemResult>()
            .AsNoTracking()
            .Where(value => value.ReviewStageId == stage.Id)
            .OrderBy(value => value.ItemId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (items.Length == 0) throw new InvalidOperationException("Completed review stage has no item results.");
        var results = items.Select(item => new GradeItemResultV1(
            item.ItemId,
            item.State switch
            {
                PersistedGradeItemState.Graded => GradeItemState.Graded,
                PersistedGradeItemState.Pending => GradeItemState.Pending,
                PersistedGradeItemState.Unsupported => GradeItemState.Unsupported,
                _ => throw new ArgumentOutOfRangeException(),
            },
            item.Score,
            item.MaxScore,
            [],
            stage.ReviewMethod,
            stage.HandlerKey,
            stage.HandlerVersion,
            item.Feedback,
            stage.ProviderKey)).ToArray();
        var final = results.All(item => item.State == GradeItemState.Graded);
        return new GradeResultV1(
            GradingContractVersions.GradeResult,
            final ? "final" : "partial",
            final ? ScoreValue.Sum(results.Select(item => item.Score!.Value)) : null,
            ScoreValue.Sum(results.Select(item => item.MaxScore)),
            results,
            []);
    }

    private async Task<GradingExecutionOutcome> OutcomeAsync(
        GradingExecution execution,
        GradeRound round,
        CancellationToken cancellationToken)
    {
        var stage = await context.Set<ReviewStage>()
            .Where(value => value.GradeRoundId == round.Id && value.Status == PersistedReviewStageStatus.Completed)
            .OrderByDescending(value => value.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = stage is null ? null : await StageResultAsync(stage, cancellationToken).ConfigureAwait(false);
        return ToOutcome(execution, round, result,
            round.Status == PersistedGradeRoundStatus.AwaitingInstructorResolution);
    }

    private async Task<GradingExecution> RequireExecutionAsync(Guid executionId, CancellationToken cancellationToken) =>
        await context.Set<GradingExecution>()
            .SingleOrDefaultAsync(value => value.Id == executionId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new KeyNotFoundException("Grading execution was not found.");

    private async Task<AssessmentDefinitionRevision> RequireRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken) =>
        await context.Set<AssessmentDefinitionRevision>()
            .SingleOrDefaultAsync(value => value.Id == revisionId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new KeyNotFoundException("Assessment definition revision was not found.");

    private async Task<GradeRound> RequireActiveRoundAsync(
        GradingExecution execution,
        CancellationToken cancellationToken)
    {
        if (!execution.ActiveGradeRoundId.HasValue)
            throw new InvalidOperationException("Grading execution does not have an active round.");
        return await context.Set<GradeRound>()
            .SingleAsync(value => value.Id == execution.ActiveGradeRoundId && value.GradingExecutionId == execution.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static GradingExecutionOutcome ToOutcome(
        GradingExecution execution,
        GradeRound round,
        GradeResultV1? result,
        bool requiresInstructorReview = false) =>
        new(
            execution.Id,
            round.Id,
            execution.Status,
            round.Status,
            result,
            requiresInstructorReview);

    private static ScoreValue ReadProjectionScore(JsonElement projection)
    {
        var value = projection.GetProperty("maxScore");
        return ScoreValue.FromUnits(value.TryGetInt32(out var units)
            ? units
            : throw new JsonException("Assessment item maxScore must be an integer."));
    }

    private static string RequireItemId(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Instructor item ID is required.") : value.Trim();

    private static string Serialize<T>(T value) =>
        CanonicalJson.Serialize(JsonSerializer.SerializeToElement(value, GradingJson.Options));

    private static T Deserialize<T>(string canonicalJson, string label) =>
        JsonSerializer.Deserialize<T>(canonicalJson, GradingJson.Options)
        ?? throw new JsonException($"Persisted {label} is invalid.");
}
