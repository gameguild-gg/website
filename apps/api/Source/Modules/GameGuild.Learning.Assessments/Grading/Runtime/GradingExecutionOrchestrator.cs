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

    Task<GradingExecutionOutcome> AcceptReviewEvidenceAsync(
        Guid executionId,
        ReviewMethod method,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken = default);

    Task<GradingExecutionOutcome> ResumeAsync(
        Guid executionId,
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
    IGradingFinalizationSink finalizationSink,
    IAcademicOutboxWriter? outbox = null) : IGradingExecutionOrchestrator
{
    public AssessmentExecutionDeliveryV1 MaterializeDelivery(
        GradingExecution execution,
        AssessmentDefinitionRevision revision)
    {
        if (execution.DefinitionRevisionId != revision.Id)
        {
            throw new InvalidOperationException("Execution and revision do not match.");
        }

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
            {
                throw new InvalidOperationException("The execution was already submitted with a different response.");
            }

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
        {
            throw new InvalidOperationException("Execution and revision do not match.");
        }

        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = MaterializeDelivery(execution, revision);
        GradingContractValidator.ValidateBindings(revision.ExecutionSnapshotHash, snapshot, delivery, response);
        var bindings = snapshot.Manifest.Items
            .Select(item => (item.AdapterKey, item.AdapterVersion))
            .Distinct()
            .ToArray();
        if (bindings.Length != 1)
        {
            throw new InvalidOperationException("An assessment response must be owned by exactly one assessment-type adapter.");
        }

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
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }

        if (resolution.SchemaVersion != 1)
        {
            throw new ArgumentException("Instructor review schemaVersion must be 1.", nameof(resolution));
        }

        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        var round = await RequireActiveRoundAsync(execution, cancellationToken).ConfigureAwait(false);
        var stage = await context.Set<ReviewStage>()
            .Where(value => value.GradeRoundId == round.Id && value.ReviewMethod == ReviewMethod.InstructorReview)
            .OrderBy(value => value.Sequence)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The active round does not contain an InstructorReview stage.");
        var previous = await PreviousStageResultAsync(round.Id, stage.Sequence, cancellationToken).ConfigureAwait(false);
        var evidencePayload = new
        {
            resolution.SchemaVersion,
            executionId,
            roundId = round.Id,
            actorId,
            resolution.Items,
            resolution.Feedback,
            resolution.OverrideReason,
            resolution.RubricScores,
            previousResult = previous,
        };
        return await AcceptReviewEvidenceAsync(
            executionId,
            ReviewMethod.InstructorReview,
            new ReviewStageEvidenceV1(
                $"instructor-resolution:{round.Id:N}",
                "instructor-review-evidence",
                "1",
                JsonSerializer.SerializeToElement(evidencePayload, GradingJson.Options),
                ProducedByActorId: actorId),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<GradingExecutionOutcome> AcceptReviewEvidenceAsync(
        Guid executionId,
        ReviewMethod method,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken = default)
    {
        ValidateEvidence(evidence);
        return GradingRuntimeDatabaseLock.ExecuteWithStrategyAsync(
            context,
            () => AcceptReviewEvidenceTransactionalAsync(executionId, method, evidence, cancellationToken));
    }

    private async Task<GradingExecutionOutcome> AcceptReviewEvidenceTransactionalAsync(
        Guid executionId,
        ReviewMethod method,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireExecutionAsync(
            context,
            executionId,
            cancellationToken).ConfigureAwait(false);
        var outcome = await AcceptReviewEvidenceCoreAsync(
                executionId,
                method,
                evidence,
                cancellationToken)
            .ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    private async Task<GradingExecutionOutcome> AcceptReviewEvidenceCoreAsync(
        Guid executionId,
        ReviewMethod method,
        ReviewStageEvidenceV1 evidence,
        CancellationToken cancellationToken)
    {
        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        var revision = await RequireRevisionAsync(execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = MaterializeDelivery(execution, revision);
        var round = await RequireActiveRoundAsync(execution, cancellationToken).ConfigureAwait(false);
        var stage = await context.Set<ReviewStage>()
            .SingleOrDefaultAsync(
                value => value.GradeRoundId == round.Id && value.ReviewMethod == method,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The active round does not contain a {method} stage.");
        var canonical = Serialize(evidence.Payload);
        var existing = await context.Set<ReviewEvidence>()
            .SingleOrDefaultAsync(
                value => value.ReviewStageId == stage.Id && value.EvidenceKey == evidence.EvidenceKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null &&
            !string.Equals(existing.PayloadHash, CanonicalPayload.Hash(canonical), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Evidence key was already used with a different payload.");
        }

        if (stage.Status == PersistedReviewStageStatus.Completed)
        {
            if (existing is null)
            {
                throw new InvalidOperationException($"The {method} stage is already complete and cannot accept new evidence.");
            }

            return await OutcomeAsync(execution, round, cancellationToken).ConfigureAwait(false);
        }

        var activeStage = await context.Set<ReviewStage>()
            .Where(value => value.GradeRoundId == round.Id && value.Status != PersistedReviewStageStatus.Completed)
            .OrderBy(value => value.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The active round has no stage awaiting evidence.");
        if (activeStage.Id != stage.Id)
        {
            throw new InvalidOperationException($"The active stage is {activeStage.ReviewMethod}, not {method}.");
        }

        var response = Deserialize<AssessmentResponseEnvelopeV1>(
            execution.ResponseEnvelopeCanonicalJson ?? throw new InvalidOperationException("Submitted response is missing."),
            "response envelope");
        var request = new ReviewStageRequest(
            execution.Id,
            round.Id,
            stage.Id,
            execution.ExecutionContext,
            revision.ExecutionSnapshotHash,
            snapshot,
            delivery,
            response,
            await PreviousStageResultAsync(round.Id, stage.Sequence, cancellationToken).ConfigureAwait(false),
            stage.HandlerKey,
            stage.HandlerVersion,
            stage.ProviderKey,
            stage.ProviderPolicyVersion,
            stage.EvidenceDeadlineAt);
        var handler = stageHandlers.Resolve(method, stage.HandlerKey, stage.HandlerVersion, execution.ExecutionContext);
        ReviewStageTransitionV1 transition;
        if (existing is not null)
        {
            transition = await handler.TryCompleteAsync(
                request,
                await LoadStageEvidenceAsync(stage.Id, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            transition = await handler.AcceptEvidenceAsync(request, evidence, cancellationToken).ConfigureAwait(false);
            context.Set<ReviewEvidence>().Add(CreateEvidence(execution.TenantId, stage.Id, evidence, canonical));
            if (transition.Kind == ReviewStageTransitionKindV1.AwaitingEvidence)
            {
                var accumulatedEvidence = (await LoadStageEvidenceAsync(stage.Id, cancellationToken)
                        .ConfigureAwait(false))
                    .Append(evidence)
                    .ToArray();
                transition = await handler.TryCompleteAsync(request, accumulatedEvidence, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        ApplyTransition(execution, round, stage, snapshot, transition, persistResultEvidence: false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transition.Kind == ReviewStageTransitionKindV1.Completed)
        {
            return await RunAvailableStagesAsync(
                    execution,
                    revision,
                    snapshot,
                    delivery,
                    round,
                    cancellationToken,
                    stage.Id,
                    transition.Result)
                .ConfigureAwait(false);
        }

        return ToOutcome(
            execution,
            round,
            transition.Result,
            transition.Kind == ReviewStageTransitionKindV1.AwaitingInstructorResolution);
    }

    public async Task<GradingExecutionOutcome> RegradeAsync(
        Guid executionId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Regrade reason is required.", nameof(reason));
        }

        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        if (execution.Status != PersistedGradingExecutionStatus.Completed || !execution.ActiveGradeRoundId.HasValue)
        {
            throw new InvalidOperationException("Only a completed execution can be regraded.");
        }

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
        var stages = AddStages(round, snapshot);
        await ReuseHumanEvidenceAsync(previous, stages, cancellationToken).ConfigureAwait(false);
        execution.BeginRegrade(round.Id);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await RunAvailableStagesAsync(execution, revision, snapshot, delivery, round, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<GradingExecutionOutcome> ResumeAsync(
        Guid executionId,
        CancellationToken cancellationToken = default) =>
        GradingRuntimeDatabaseLock.ExecuteWithStrategyAsync(
            context,
            () => ResumeTransactionalAsync(executionId, cancellationToken));

    private async Task<GradingExecutionOutcome> ResumeTransactionalAsync(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireExecutionAsync(
            context,
            executionId,
            cancellationToken).ConfigureAwait(false);
        var execution = await RequireExecutionAsync(executionId, cancellationToken).ConfigureAwait(false);
        var revision = await RequireRevisionAsync(execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = MaterializeDelivery(execution, revision);
        var outcome = await ResumeAsync(execution, revision, snapshot, delivery, cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return outcome;
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
        {
            return await OutcomeAsync(execution, round, cancellationToken).ConfigureAwait(false);
        }

        return await RunAvailableStagesAsync(execution, revision, snapshot, delivery, round, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<GradingExecutionOutcome> RunAvailableStagesAsync(
        GradingExecution execution,
        AssessmentDefinitionRevision revision,
        AssessmentExecutionSnapshotV1 snapshot,
        AssessmentExecutionDeliveryV1 delivery,
        GradeRound round,
        CancellationToken cancellationToken,
        Guid? resumedStageId = null,
        GradeResultV1? resumedStageResult = null)
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
                previousResult = stage.Id == resumedStageId && resumedStageResult is not null
                    ? resumedStageResult
                    : await StageResultAsync(stage, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var handler = stageHandlers.Resolve(
                stage.ReviewMethod,
                stage.HandlerKey,
                stage.HandlerVersion,
                execution.ExecutionContext);
            var request = new ReviewStageRequest(
                execution.Id,
                round.Id,
                stage.Id,
                execution.ExecutionContext,
                revision.ExecutionSnapshotHash,
                snapshot,
                delivery,
                response,
                previousResult,
                stage.HandlerKey,
                stage.HandlerVersion,
                stage.ProviderKey,
                stage.ProviderPolicyVersion,
                stage.EvidenceDeadlineAt);
            ReviewStageTransitionV1 transition;
            if (stage.Status == PersistedReviewStageStatus.Pending)
            {
                stage.Start(SystemClock.UtcNow);
                transition = await handler.StartAsync(request, cancellationToken).ConfigureAwait(false);
                if (transition.Kind == ReviewStageTransitionKindV1.AwaitingEvidence)
                {
                    var existingEvidence = await LoadStageEvidenceAsync(stage.Id, cancellationToken).ConfigureAwait(false);
                    if (existingEvidence.Count > 0)
                    {
                        transition = await handler.TryCompleteAsync(request, existingEvidence, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            else
            {
                transition = await handler.TryCompleteAsync(
                    request,
                    await LoadStageEvidenceAsync(stage.Id, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }

            ApplyTransition(execution, round, stage, snapshot, transition, persistResultEvidence: true);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transition.Kind != ReviewStageTransitionKindV1.Completed)
            {
                return ToOutcome(
                    execution,
                    round,
                    transition.Result ?? previousResult,
                    transition.Kind == ReviewStageTransitionKindV1.AwaitingInstructorResolution);
            }

            previousResult = transition.Result;
        }

        if (previousResult is null)
        {
            throw new InvalidOperationException("The grading workflow did not produce a result.");
        }

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
            var hasOtherNonTerminalExecution = await context.Set<GradingExecution>()
                .Where(value => value.TestRunSubjectId.HasValue && value.Id != execution.Id)
                .Join(
                    context.Set<AssessmentTestRunSubject>().Where(value => value.TestRunId == run.Id),
                    candidate => candidate.TestRunSubjectId,
                    candidateSubject => (Guid?)candidateSubject.Id,
                    (candidate, _) => candidate)
                .AnyAsync(
                    candidate => candidate.Status != PersistedGradingExecutionStatus.Completed &&
                                 candidate.Status != PersistedGradingExecutionStatus.Failed,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!hasOtherNonTerminalExecution)
            {
                run.Complete(finalizedAt);
            }
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

    private IReadOnlyList<ReviewStage> AddStages(GradeRound round, AssessmentExecutionSnapshotV1 snapshot)
    {
        var stages = new List<ReviewStage>(snapshot.Manifest.Stages.Count);
        for (var index = 0; index < snapshot.Manifest.Stages.Count; index++)
        {
            var stage = ReviewStage.Create(
                round.TenantId,
                round.Id,
                index + 1,
                snapshot.Manifest.Stages[index]);
            context.Set<ReviewStage>().Add(stage);
            stages.Add(stage);
        }

        return stages;
    }

    private async Task ReuseHumanEvidenceAsync(
        GradeRound supersededRound,
        IReadOnlyList<ReviewStage> replacementStages,
        CancellationToken cancellationToken)
    {
        var reusableMethods = new[] { ReviewMethod.SelfReview, ReviewMethod.PeerReview };
        var previousStages = await context.Set<ReviewStage>()
            .AsNoTracking()
            .Where(value => value.GradeRoundId == supersededRound.Id &&
                            value.Status == PersistedReviewStageStatus.Completed &&
                            reusableMethods.Contains(value.ReviewMethod))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (previousStages.Length == 0)
        {
            return;
        }

        var previousStageIds = previousStages.Select(value => value.Id).ToArray();
        var previousEvidence = await context.Set<ReviewEvidence>()
            .AsNoTracking()
            .Where(value => previousStageIds.Contains(value.ReviewStageId) &&
                            value.EvidenceType != "review-stage-result")
            .OrderBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var referencedIds = previousEvidence
            .Where(value => value.SourceReviewEvidenceId.HasValue)
            .Select(value => value.SourceReviewEvidenceId!.Value)
            .Distinct()
            .ToArray();
        var sources = referencedIds.Length == 0
            ? new Dictionary<Guid, ReviewEvidence>()
            : await context.Set<ReviewEvidence>()
                .AsNoTracking()
                .Where(value => referencedIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, cancellationToken)
                .ConfigureAwait(false);

        foreach (var previousStage in previousStages)
        {
            var replacement = replacementStages.Single(value =>
                value.Sequence == previousStage.Sequence && value.ReviewMethod == previousStage.ReviewMethod);
            foreach (var prior in previousEvidence.Where(value => value.ReviewStageId == previousStage.Id))
            {
                var source = prior.SourceReviewEvidenceId.HasValue
                    ? sources[prior.SourceReviewEvidenceId.Value]
                    : prior;
                context.Set<ReviewEvidence>().Add(ReviewEvidence.CreateReference(
                    replacement.TenantId,
                    replacement.Id,
                    $"reused:{source.Id:N}",
                    source,
                    supersededRound.Id,
                    "grading-regrade"));
            }
        }
    }

    private void ApplyTransition(
        GradingExecution execution,
        GradeRound round,
        ReviewStage stage,
        AssessmentExecutionSnapshotV1 snapshot,
        ReviewStageTransitionV1 transition,
        bool persistResultEvidence)
    {
        if (transition.SchemaVersion != 1)
        {
            throw new InvalidOperationException("Review stage transition schemaVersion must be 1.");
        }

        if (transition.EvidenceDeadlineAt.HasValue)
        {
            stage.SetEvidenceDeadline(transition.EvidenceDeadlineAt.Value);
        }

        if (transition.DurableRequest is not null)
        {
            if (!execution.TenantId.HasValue || outbox is null)
            {
                throw new InvalidOperationException("A durable review request requires a tenant-scoped outbox.");
            }

            outbox.Enqueue(
                execution.TenantId.Value,
                transition.DurableRequest.EventType,
                transition.DurableRequest.SchemaVersion,
                CanonicalJson.Serialize(transition.DurableRequest.Payload));
        }

        switch (transition.Kind)
        {
            case ReviewStageTransitionKindV1.Completed:
            {
                var result = transition.Result
                    ?? throw new InvalidOperationException("A completed review stage transition requires a result.");
                GradingContractValidator.Validate(result);
                ValidateStageResult(snapshot, stage, result);
                PersistStageResult(execution, stage, result);
                if (persistResultEvidence)
                {
                    context.Set<ReviewEvidence>().Add(ReviewEvidence.CreateForService(
                        execution.TenantId,
                        stage.Id,
                        $"stage-result:{stage.Id:N}",
                        "review-stage-result",
                        "1",
                        Serialize(result),
                        $"{stage.HandlerKey}@{stage.HandlerVersion}"));
                }
                stage.Complete(SystemClock.UtcNow);
                break;
            }
            case ReviewStageTransitionKindV1.AwaitingEvidence:
            case ReviewStageTransitionKindV1.RetryableFailure:
                RequireNoResult(transition);
                stage.AwaitEvidence();
                round.AwaitEvidence();
                execution.AwaitReview();
                GradingOperationalMetrics.RecordReviewWait(stage.ReviewMethod, "awaiting-evidence");
                break;
            case ReviewStageTransitionKindV1.AwaitingInstructorResolution:
                RequireNoResult(transition);
                stage.AwaitInstructorResolution();
                round.AwaitInstructorResolution();
                execution.AwaitReview();
                GradingOperationalMetrics.RecordReviewWait(stage.ReviewMethod, "awaiting-instructor-resolution");
                break;
            case ReviewStageTransitionKindV1.TerminalFailure:
                RequireNoResult(transition);
                var failedAt = SystemClock.UtcNow;
                stage.Fail(failedAt);
                round.Fail(failedAt);
                execution.Fail(failedAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition), transition.Kind, "Review stage transition is unsupported.");
        }
    }

    private async Task<IReadOnlyList<ReviewStageEvidenceV1>> LoadStageEvidenceAsync(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        var evidence = await context.Set<ReviewEvidence>()
            .AsNoTracking()
            .Where(value => value.ReviewStageId == stageId && value.EvidenceType != "review-stage-result")
            .OrderBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var sourceIds = evidence
            .Where(value => value.SourceReviewEvidenceId.HasValue)
            .Select(value => value.SourceReviewEvidenceId!.Value)
            .Distinct()
            .ToArray();
        var sources = sourceIds.Length == 0
            ? new Dictionary<Guid, ReviewEvidence>()
            : await context.Set<ReviewEvidence>()
                .AsNoTracking()
                .Where(value => sourceIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, cancellationToken)
                .ConfigureAwait(false);
        return evidence.Select(value =>
        {
            var effective = value.SourceReviewEvidenceId.HasValue
                ? sources[value.SourceReviewEvidenceId.Value]
                : value;
            return new ReviewStageEvidenceV1(
                value.EvidenceKey,
                effective.EvidenceType,
                effective.SchemaVersion,
                JsonSerializer.Deserialize<JsonElement>(effective.CanonicalJson, GradingJson.Options),
                effective.ProducedByActorId,
                effective.ProducedByService,
                effective.ItemId);
        }).ToArray();
    }

    private static ReviewEvidence CreateEvidence(
        Guid? tenantId,
        Guid stageId,
        ReviewStageEvidenceV1 evidence,
        string canonical)
    {
        if (evidence.ProducedByActorId.HasValue)
        {
            return ReviewEvidence.CreateForActor(
                tenantId,
                stageId,
                evidence.EvidenceKey,
                evidence.EvidenceType,
                evidence.SchemaVersion,
                canonical,
                evidence.ProducedByActorId.Value,
                evidence.ItemId);
        }

        return ReviewEvidence.CreateForService(
            tenantId,
            stageId,
            evidence.EvidenceKey,
            evidence.EvidenceType,
            evidence.SchemaVersion,
            canonical,
            evidence.ProducedByService!,
            evidence.ItemId);
    }

    private static void ValidateEvidence(ReviewStageEvidenceV1 evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.EvidenceKey) ||
            string.IsNullOrWhiteSpace(evidence.EvidenceType) ||
            string.IsNullOrWhiteSpace(evidence.SchemaVersion))
        {
            throw new ArgumentException("Evidence key, type, and schema version are required.", nameof(evidence));
        }

        var hasActor = evidence.ProducedByActorId.HasValue && evidence.ProducedByActorId != Guid.Empty;
        var hasService = !string.IsNullOrWhiteSpace(evidence.ProducedByService);
        if (hasActor == hasService)
        {
            throw new ArgumentException("Evidence must identify exactly one actor or service producer.", nameof(evidence));
        }
    }

    private static void RequireNoResult(ReviewStageTransitionV1 transition)
    {
        if (transition.Result is not null)
        {
            throw new InvalidOperationException($"{transition.Kind} review stage transition cannot carry a result.");
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
        {
            throw new InvalidOperationException("Review result items do not match the immutable manifest.");
        }

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
        if (items.Length == 0)
        {
            throw new InvalidOperationException("Completed review stage has no item results.");
        }

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
        {
            throw new InvalidOperationException("Grading execution does not have an active round.");
        }

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
