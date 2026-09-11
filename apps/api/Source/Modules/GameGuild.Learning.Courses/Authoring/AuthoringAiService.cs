using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using GameGuild.AI;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

public interface IAuthoringAiService
{
    Task<AiEntitlementDto> GetEntitlement(Guid tenantId, Guid actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiAuthoringConversationDto>> GetConversations(Guid tenantId, Guid actorId, Guid programId, Guid contentId, CancellationToken cancellationToken);
    Task<AiAuthoringRunDto> CreateRun(Guid tenantId, Guid actorId, Guid programId, Guid contentId, AiAuthoringRunRequest request, CancellationToken cancellationToken);
    Task<AiAuthoringRunDto> GetRun(Guid tenantId, Guid actorId, Guid programId, Guid contentId, Guid runId, CancellationToken cancellationToken);
    IAsyncEnumerable<AiStreamEvent> StreamRun(Guid tenantId, Guid actorId, Guid programId, Guid contentId, Guid runId, long afterSequence, CancellationToken cancellationToken);
    Task<AuthoringDraftDto> ApplyProposal(Guid tenantId, Guid actorId, Guid programId, Guid contentId, Guid proposalId, ApplyAiProposalRequest request, CancellationToken cancellationToken);
    Task<AiProposalDto> DiscardProposal(Guid tenantId, Guid actorId, Guid programId, Guid contentId, Guid proposalId, CancellationToken cancellationToken);
    Task ProcessRun(Guid runId, CancellationToken cancellationToken);
}

internal interface IAuthoringAiRunQueue
{
    ValueTask Enqueue(Guid runId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAll(CancellationToken cancellationToken);
}

internal sealed class AuthoringAiRunQueue : IAuthoringAiRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask Enqueue(Guid runId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class AuthoringAiService(
    IApplicationDbContext db,
    IAiOrchestrator ai,
    IAiCreditWalletService credits,
    IResourceQuotaEnforcer quotaEnforcer,
    IAuthoringAiRunQueue queue,
    TimeProvider timeProvider,
    ILogger<AuthoringAiService> logger) : IAuthoringAiService
{
    private const string ServiceCode = "lesson-authoring";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiEntitlementDto> GetEntitlement(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var balance = await credits.GetBalanceAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        return new AiEntitlementDto(balance.AvailableSoftUnits, balance.ReservedSoftUnits, balance.SettledSoftUnits);
    }

    public async Task<IReadOnlyList<AiAuthoringConversationDto>> GetConversations(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var conversations = await db.Set<AiAuthoringConversation>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.AuthorId == actorId && item.ProgramId == programId && item.ContentId == contentId && item.DeletedAt == null)
            .OrderByDescending(item => item.LastMessageAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (conversations.Count == 0)
            return [];

        var ids = conversations.Select(item => item.Id).ToArray();
        var messages = await db.Set<AiAuthoringMessage>()
            .AsNoTracking()
            .Where(item => ids.Contains(item.ConversationId))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return conversations.Select(conversation => new AiAuthoringConversationDto(
            conversation.Id,
            conversation.ContentId,
            conversation.AuthorId,
            conversation.LastMessageAt,
            messages.Where(message => message.ConversationId == conversation.Id).Select(ToDto).ToArray())).ToArray();
    }

    public async Task<AiAuthoringRunDto> CreateRun(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        AiAuthoringRunRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);

        var duplicate = await db.Set<AiAuthoringRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ActorId == actorId && item.IdempotencyKey == request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            if (duplicate.ProgramId != programId ||
                duplicate.ContentId != contentId ||
                duplicate.BaseDraftRevision != request.DraftRevision ||
                duplicate.ProposalKind != request.ProposalKind ||
                !string.Equals(duplicate.Instruction, request.Instruction.Trim(), StringComparison.Ordinal) ||
                !string.Equals(duplicate.Selection, NormalizeOptional(request.Selection), StringComparison.Ordinal))
                throw new AiAuthoringIdempotencyConflictException(
                    "The AI idempotency key is already bound to another authoring request.");
            return await ToDto(duplicate, cancellationToken).ConfigureAwait(false);
        }

        var draft = await FindDraft(programId, contentId, tenantId, cancellationToken).ConfigureAwait(false);
        if (draft.Revision != request.DraftRevision)
            throw new AuthoringRevisionConflictException(request.DraftRevision, draft.Revision);
        EnsureProposalKindAllowed(DeserializePayload(draft.PayloadJson), request.ProposalKind);

        var generationRequest = BuildGenerationRequest(
            request.ProposalKind,
            request.Instruction,
            request.Selection,
            draft.PayloadJson,
            maximumOutputTokens: null);
        var resolved = await ai.DescribeGenerateAsync(new AiExecutionActor(tenantId, actorId), generationRequest, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
            throw new AiAuthoringExecutionException(resolved.Error.Code, resolved.Error.Description);

        var maximumInputTokens = EstimateInputTokens(generationRequest);
        var quote = await credits.QuoteAsync(
            ServiceCode,
            resolved.Value.Provider,
            resolved.Value.Model,
            maximumInputTokens,
            resolved.Value.MaximumOutputTokens,
            cancellationToken).ConfigureAwait(false);

        var maximumQuotaTokens = checked(maximumInputTokens + resolved.Value.MaximumOutputTokens);
        await ReserveQuota(tenantId, actorId, maximumQuotaTokens, cancellationToken).ConfigureAwait(false);

        // Resolve/create the conversation only after pricing succeeds. The credit
        // service may persist a newly resolved rate card, and no authoring state
        // should leak into that save before a wallet reservation is accepted.
        var creditsReserved = false;
        try
        {
            var now = timeProvider.GetUtcNow();
            var conversation = await ResolveConversation(request.ConversationId, tenantId, actorId, programId, contentId, now, cancellationToken).ConfigureAwait(false);
            var run = AiAuthoringRun.Create(
                tenantId,
                actorId,
                programId,
                contentId,
                draft.Id,
                conversation.Id,
                draft.Revision,
                request.ProposalKind,
                request.Instruction,
                request.Selection,
                request.IdempotencyKey,
                now);
            run.Reserve(
                resolved.Value.Provider,
                resolved.Value.Model,
                maximumInputTokens,
                resolved.Value.MaximumOutputTokens,
                quote.MaximumSoftUnits,
                now);

            // AiCreditWalletService owns the relational transaction. Tracking the
            // authoring records before ReserveAsync makes run, conversation, message,
            // initial SSE event, and reservation one atomic SaveChanges operation.
            db.Set<AiAuthoringRun>().Add(run);
            conversation.Touch(now);
            db.Set<AiAuthoringMessage>().Add(AiAuthoringMessage.Create(conversation.Id, run.Id, "user", request.Instruction, now));
            AddEvent(run.Id, 1, "status", run.Status.ToString(), null, null, now);
            await credits.ReserveAsync(
                run.Id,
                tenantId,
                actorId,
                ServiceCode,
                quote,
                $"authoring:{tenantId:N}:{actorId:N}:{request.IdempotencyKey}",
                cancellationToken).ConfigureAwait(false);
            creditsReserved = true;
            // Keep the orchestration contract correct even when the wallet adapter
            // persists through a separate unit of work. The production adapter has
            // already saved these tracked records in its reservation transaction;
            // this second call is therefore a no-op there.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await queue.Enqueue(run.Id, cancellationToken).ConfigureAwait(false);
            return await ToDto(run, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!creditsReserved)
                await ReleaseQuota(tenantId, actorId, maximumQuotaTokens, releaseRequest: true, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<AiAuthoringRunDto> GetRun(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await FindOwnedRun(tenantId, actorId, programId, contentId, runId, cancellationToken).ConfigureAwait(false);
        return await ToDto(run, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<AiStreamEvent> StreamRun(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        Guid runId,
        long afterSequence,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _ = await FindOwnedRun(tenantId, actorId, programId, contentId, runId, cancellationToken).ConfigureAwait(false);
        var cursor = Math.Max(0, afterSequence);
        while (!cancellationToken.IsCancellationRequested)
        {
            var events = await db.Set<AiAuthoringStreamEvent>()
                .AsNoTracking()
                .Where(item => item.RunId == runId && item.Sequence > cursor)
                .OrderBy(item => item.Sequence)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var item in events)
            {
                cursor = item.Sequence;
                yield return DeserializeEvent(item);
            }

            var status = await db.Set<AiAuthoringRun>()
                .AsNoTracking()
                .Where(item => item.Id == runId)
                .Select(item => item.Status)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            if (status is AiAuthoringRunStatus.Completed or AiAuthoringRunStatus.Failed or AiAuthoringRunStatus.Cancelled)
                yield break;
            await Task.Delay(TimeSpan.FromMilliseconds(300), timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<AuthoringDraftDto> ApplyProposal(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        Guid proposalId,
        ApplyAiProposalRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var proposal = await db.Set<AiAuthoringProposal>()
            .SingleOrDefaultAsync(item => item.Id == proposalId && item.ContentId == contentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI proposal was not found.");
        _ = await FindOwnedRun(tenantId, actorId, programId, contentId, proposal.RunId, cancellationToken).ConfigureAwait(false);
        var draft = await FindDraft(programId, contentId, tenantId, cancellationToken).ConfigureAwait(false);
        if (draft.Revision != request.DraftRevision)
            throw new AuthoringRevisionConflictException(request.DraftRevision, draft.Revision);
        proposal.EnsureApplicableTo(draft.Revision);

        var payload = DeserializePayload(draft.PayloadJson);
        payload = ApplyProposedContent(payload, proposal, request.CursorOffset);
        draft.Update(request.DraftRevision, JsonSerializer.Serialize(payload, JsonOptions), actorId, timeProvider.GetUtcNow());
        proposal.MarkApplied(actorId, timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentRevision = await db.Set<ProgramContentDraft>()
                .AsNoTracking()
                .Where(item => item.Id == draft.Id)
                .Select(item => (int?)item.Revision)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Authoring draft was not found.");
            if (currentRevision != request.DraftRevision)
                throw new AuthoringRevisionConflictException(request.DraftRevision, currentRevision);
            throw await ProposalConflict(proposalId, cancellationToken).ConfigureAwait(false);
        }
        return ToDraftDto(draft);
    }

    public async Task<AiProposalDto> DiscardProposal(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var proposal = await db.Set<AiAuthoringProposal>()
            .SingleOrDefaultAsync(item => item.Id == proposalId && item.ContentId == contentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI proposal was not found.");
        _ = await FindOwnedRun(tenantId, actorId, programId, contentId, proposal.RunId, cancellationToken).ConfigureAwait(false);
        proposal.Discard(actorId, timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw await ProposalConflict(proposalId, cancellationToken).ConfigureAwait(false);
        }
        return ToDto(proposal);
    }

    public async Task ProcessRun(Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.Set<AiAuthoringRun>()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null || run.Status is AiAuthoringRunStatus.Completed or AiAuthoringRunStatus.Failed or AiAuthoringRunStatus.Cancelled)
            return;

        var nextSequence = await NextSequence(runId, cancellationToken).ConfigureAwait(false);
        if (run.Status == AiAuthoringRunStatus.Running)
        {
            // A running row can only be observed here after the process that owned
            // the provider stream disappeared. Replaying it could submit and charge
            // the same prompt twice, so fail closed and release the reservation.
            await ReleaseAndFail(
                run,
                nextSequence,
                "AI_RUN_INTERRUPTED",
                "The AI run was interrupted before it completed. Start a new run to retry safely.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var draft = await db.Set<ProgramContentDraft>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == run.DraftId, cancellationToken)
            .ConfigureAwait(false);
        run.Start(timeProvider.GetUtcNow());
        AddEvent(run.Id, nextSequence++, "status", run.Status.ToString(), null, null, timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker atomically claimed this run first.
            return;
        }

        try
        {
            var completion = await ai.GenerateForActorStreamingAsync(
                new AiExecutionActor(run.TenantId!.Value, run.ActorId),
                BuildGenerationRequest(run, draft.PayloadJson),
                async (delta, streamCancellationToken) =>
                {
                    if (string.IsNullOrEmpty(delta))
                        return;
                    AddEvent(run.Id, nextSequence++, "delta", AiAuthoringRunStatus.Running.ToString(), delta, null, timeProvider.GetUtcNow());
                    await db.SaveChangesAsync(streamCancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            if (completion.IsFailure)
                throw new AiAuthoringExecutionException(completion.Error.Code, completion.Error.Description);

            var inputTokens = completion.Value.Usage.InputTokens ?? 0;
            var outputTokens = completion.Value.Usage.OutputTokens ?? 0;
            var actualQuotaTokens = checked(inputTokens + outputTokens);
            var reservedQuotaTokens = checked(run.MaximumInputTokens + run.MaximumOutputTokens);
            if (actualQuotaTokens > reservedQuotaTokens)
                throw new AiAuthoringExecutionException("AI_USAGE_EXCEEDED_RESERVATION", "Provider usage exceeded the reserved token envelope.");
            var originalContent = OriginalContent(draft.PayloadJson, run.ProposalKind);
            var proposedContent = MaterializeProposedContent(run.ProposalKind, originalContent, completion.Value.Text);
            var relationalContext = db as DbContext;
            await using var finalizationTransaction = relationalContext?.Database.IsRelational() == true
                ? await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                : null;
            try
            {
                // Settlement and the durable result are one unit: a charged run
                // must always have a recoverable proposal, message, terminal
                // event, and usage record in the same commit.
                var settlement = await credits.SettleAsync(
                    run.Id,
                    inputTokens,
                    outputTokens,
                    $"{completion.Value.Provider}:{run.Id:N}",
                    $"settle:{run.Id:N}",
                    cancellationToken).ConfigureAwait(false);
                var proposal = AiAuthoringProposal.Create(
                    run.Id,
                    run.ContentId,
                    run.BaseDraftRevision,
                    run.ProposalKind,
                    originalContent,
                    proposedContent,
                    timeProvider.GetUtcNow());
                proposal.TenantId = run.TenantId;
                db.Set<AiAuthoringProposal>().Add(proposal);
                db.Set<AiAuthoringMessage>().Add(AiAuthoringMessage.Create(
                    run.ConversationId,
                    run.Id,
                    "assistant",
                    completion.Value.Text,
                    timeProvider.GetUtcNow()));

                run.Complete(
                    completion.Value.Text,
                    inputTokens,
                    outputTokens,
                    settlement.SettledSoftUnits,
                    settlement.ReleasedSoftUnits,
                    timeProvider.GetUtcNow());
                var usage = Usage(run, await credits.GetBalanceAsync(run.TenantId!.Value, run.ActorId, cancellationToken).ConfigureAwait(false));
                var completedEvent = new AiStreamEvent(
                    nextSequence,
                    "completed",
                    null,
                    run.Id,
                    run.Status.ToString(),
                    usage,
                    ToDto(proposal));
                AddEvent(run.Id, nextSequence, "completed", run.Status.ToString(), null, JsonSerializer.Serialize(completedEvent, JsonOptions), timeProvider.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                if (finalizationTransaction is not null)
                    await finalizationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (finalizationTransaction is not null)
                {
                    await finalizationTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    relationalContext!.ChangeTracker.Clear();
                    run = await db.Set<AiAuthoringRun>()
                        .SingleAsync(item => item.Id == runId, CancellationToken.None)
                        .ConfigureAwait(false);
                    nextSequence = await NextSequence(runId, CancellationToken.None).ConfigureAwait(false);
                }
                throw;
            }
            await ReleaseQuota(run.TenantId!.Value, run.ActorId, reservedQuotaTokens - actualQuotaTokens, releaseRequest: false, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseAndFail(run, nextSequence, "AI_CANCELLED", "AI generation was cancelled.", CancellationToken.None).ConfigureAwait(false);
        }
        catch (AiAuthoringExecutionException exception)
        {
            var code = IsQuotaError(exception.Code) ? "AI_QUOTA_EXCEEDED" : exception.Code;
            await ReleaseAndFail(run, nextSequence, code, exception.Message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ReleaseAndFail(run, nextSequence, "AI_EXECUTION_FAILED", exception.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReleaseAndFail(AiAuthoringRun run, long sequence, string code, string message, CancellationToken cancellationToken)
    {
        long released = 0;
        try
        {
            var reservation = await credits.ReleaseAsync(run.Id, code, $"release:{run.Id:N}", cancellationToken).ConfigureAwait(false);
            released = reservation.ReleasedSoftUnits;
        }
        catch (InvalidOperationException)
        {
            // A completed settlement is already final and must never be released twice.
        }
        run.Fail(code, message, released, timeProvider.GetUtcNow());
        var streamEvent = new AiStreamEvent(sequence, "error", null, run.Id, run.Status.ToString(), null, null, code);
        AddEvent(run.Id, sequence, "error", run.Status.ToString(), null, JsonSerializer.Serialize(streamEvent, JsonOptions), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseQuota(run.TenantId!.Value, run.ActorId, checked(run.MaximumInputTokens + run.MaximumOutputTokens), releaseRequest: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReserveQuota(Guid tenantId, Guid actorId, long maximumTokens, CancellationToken cancellationToken)
    {
        var request = await quotaEnforcer.TryAtomicConsumeAsync(
            tenantId,
            ResourceUsageType.AiRequests,
            1,
            cancellationToken).ConfigureAwait(false);
        if (!request.Success)
            throw new AiAuthoringExecutionException("AI_QUOTA_EXCEEDED", "The AI request quota has been exceeded.");

        var tokens = await quotaEnforcer.TryAtomicConsumeAsync(
            tenantId,
            ResourceUsageType.AiTokens,
            maximumTokens,
            cancellationToken).ConfigureAwait(false);
        if (tokens.Success)
            return;

        await ReleaseQuota(tenantId, actorId, 0, releaseRequest: true, CancellationToken.None).ConfigureAwait(false);
        throw new AiAuthoringExecutionException("AI_QUOTA_EXCEEDED", "The AI token quota has been exceeded.");
    }

    private async Task ReleaseQuota(
        Guid tenantId,
        Guid actorId,
        long tokens,
        bool releaseRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            if (tokens > 0)
                _ = await quotaEnforcer.DecrementUsageAsync(
                    tenantId,
                    ResourceUsageType.AiTokens,
                    tokens,
                    actorId,
                    ServiceCode,
                    cancellationToken).ConfigureAwait(false);
            if (releaseRequest)
                _ = await quotaEnforcer.DecrementUsageAsync(
                    tenantId,
                    ResourceUsageType.AiRequests,
                    1,
                    actorId,
                    ServiceCode,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to reconcile AI quota for tenant {TenantId}, actor {ActorId}, tokens {Tokens}, releaseRequest {ReleaseRequest}",
                tenantId,
                actorId,
                tokens,
                releaseRequest);
        }
    }

    private async Task<AiAuthoringConversation> ResolveConversation(
        Guid? requestedId,
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = db.Set<AiAuthoringConversation>()
            .Where(item => item.TenantId == tenantId && item.AuthorId == actorId && item.ProgramId == programId && item.ContentId == contentId && item.DeletedAt == null);
        AiAuthoringConversation? conversation;
        if (requestedId.HasValue)
        {
            conversation = await query.SingleOrDefaultAsync(item => item.Id == requestedId.Value, cancellationToken).ConfigureAwait(false);
            if (conversation is null)
                throw new KeyNotFoundException("AI conversation was not found.");
        }
        else
        {
            conversation = await query.OrderByDescending(item => item.LastMessageAt).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (conversation is null)
            {
                conversation = AiAuthoringConversation.Create(tenantId, programId, contentId, actorId, now);
                db.Set<AiAuthoringConversation>().Add(conversation);
            }
        }
        return conversation;
    }

    private async Task<ProgramContentDraft> FindDraft(Guid programId, Guid contentId, Guid tenantId, CancellationToken cancellationToken) =>
        await db.Set<ProgramContentDraft>().SingleOrDefaultAsync(item =>
            item.ProgramId == programId && item.ContentId == contentId && item.TenantId == tenantId && item.DeletedAt == null,
            cancellationToken).ConfigureAwait(false)
        ?? throw new KeyNotFoundException("Authoring draft was not found.");

    private async Task<AiAuthoringRun> FindOwnedRun(Guid tenantId, Guid actorId, Guid programId, Guid contentId, Guid runId, CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        return await db.Set<AiAuthoringRun>()
            .SingleOrDefaultAsync(item => item.Id == runId && item.TenantId == tenantId && item.ActorId == actorId && item.ProgramId == programId && item.ContentId == contentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI authoring run was not found.");
    }

    private async Task<AiAuthoringRunDto> ToDto(AiAuthoringRun run, CancellationToken cancellationToken)
    {
        var proposal = await db.Set<AiAuthoringProposal>().AsNoTracking().SingleOrDefaultAsync(item => item.RunId == run.Id, cancellationToken).ConfigureAwait(false);
        AiCreditBalance balance;
        try
        {
            balance = await credits.GetBalanceAsync(run.TenantId!.Value, run.ActorId, cancellationToken).ConfigureAwait(false);
        }
        catch (InsufficientAiCreditsException)
        {
            balance = new AiCreditBalance(0, 0, run.SettledCost);
        }
        return new AiAuthoringRunDto(
            run.Id,
            run.ConversationId,
            run.ContentId,
            run.BaseDraftRevision,
            run.ProposalKind,
            run.Status,
            run.Instruction,
            run.Provider,
            run.Model,
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            Usage(run, balance),
            proposal is null ? null : ToDto(proposal),
            run.ErrorCode,
            run.ErrorMessage);
    }

    private void AddEvent(Guid runId, long sequence, string type, string status, string? delta, string? payload, DateTimeOffset now) =>
        db.Set<AiAuthoringStreamEvent>().Add(AiAuthoringStreamEvent.Create(runId, sequence, type, status, delta, payload, now));

    private async Task<long> NextSequence(Guid runId, CancellationToken cancellationToken)
    {
        var last = await db.Set<AiAuthoringStreamEvent>()
            .Where(item => item.RunId == runId)
            .Select(item => (long?)item.Sequence)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);
        return (last ?? 0) + 1;
    }

    private async Task<AiProposalStateConflictException> ProposalConflict(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        var currentStatus = await db.Set<AiAuthoringProposal>()
            .AsNoTracking()
            .Where(item => item.Id == proposalId)
            .Select(item => (AiProposalStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI proposal was not found.");
        return new AiProposalStateConflictException(currentStatus);
    }

    private static AiStreamEvent DeserializeEvent(AiAuthoringStreamEvent item)
    {
        if (!string.IsNullOrWhiteSpace(item.PayloadJson))
            return JsonSerializer.Deserialize<AiStreamEvent>(item.PayloadJson, JsonOptions)
                   ?? throw new InvalidOperationException("Persisted AI stream event is invalid.");
        return new AiStreamEvent(item.Sequence, item.Type, item.Delta, item.RunId, item.Status);
    }

    private static AiGenerateRequest BuildGenerationRequest(AiAuthoringRun run, string payloadJson) =>
        BuildGenerationRequest(
            run.ProposalKind,
            run.Instruction,
            run.Selection,
            payloadJson,
            run.MaximumOutputTokens > 0 ? run.MaximumOutputTokens : null);

    private static AiGenerateRequest BuildGenerationRequest(
        AiProposalKind proposalKind,
        string instruction,
        string? selection,
        string payloadJson,
        int? maximumOutputTokens)
    {
        var outputContract = proposalKind switch
        {
            AiProposalKind.LexicalPatch => """
                Return a JSON object with an operations array. Each operation uses JSON Pointer and is one of:
                {"op":"replace","path":"/root/children/0/children/0/text","value":"new text"},
                {"op":"add","path":"/root/children/-","value":{...}}, or
                {"op":"remove","path":"/root/children/2"}.
                Never change type, version, or key properties and never remove unknown or interactive nodes.
                """,
            AiProposalKind.QuizPatch => """
                Return a JSON object with an operations array. Each operation uses JSON Pointer and is one of
                replace, add, or remove. Change only the quiz fields needed to satisfy the request.
                """,
            AiProposalKind.MetadataPatch => "Return the complete AuthoringContentPayload JSON object with the requested metadata changes only.",
            AiProposalKind.InsertAtCursor => "Return only the text to insert at the selected cursor position.",
            _ => "Return only the complete replacement body.",
        };
        var prompt = $"""
                      Current lesson draft (JSON):
                      {payloadJson}

                      Author request:
                      {instruction}

                      Selected content, when present:
                      {selection ?? "(none)"}

                      Output contract:
                      {outputContract}

                      Do not include commentary or Markdown fences.
                      """;
        return new AiGenerateRequest(
            null,
            null,
            "You are the GameGuild lesson authoring copilot. Preserve the lesson format and interactive structures. Never claim to have published or directly changed the lesson.",
            prompt,
            0.4,
            maximumOutputTokens);
    }

    private static string MaterializeProposedContent(AiProposalKind kind, string originalContent, string providerOutput) =>
        kind switch
        {
            AiProposalKind.LexicalPatch or AiProposalKind.QuizPatch =>
                AuthoringStructuredPatch.Apply(originalContent, providerOutput, kind),
            AiProposalKind.MetadataPatch => JsonSerializer.Serialize(
                ApplyMetadataProposal(DeserializePayload(originalContent), providerOutput),
                JsonOptions),
            _ => providerOutput,
        };

    private static int EstimateInputTokens(AiGenerateRequest request)
    {
        // One token cannot encode less than one byte of the UTF-8 provider input.
        // Reserving by byte count is intentionally conservative and prevents a
        // tokenizer-specific input count from exceeding the financial envelope.
        var byteCount = Encoding.UTF8.GetByteCount(request.Prompt) +
                        (request.SystemPrompt is null ? 0 : Encoding.UTF8.GetByteCount(request.SystemPrompt));
        return Math.Max(1, byteCount);
    }

    private static string OriginalContent(string payloadJson, AiProposalKind kind)
    {
        var payload = DeserializePayload(payloadJson);
        return kind switch
        {
            AiProposalKind.MetadataPatch => payloadJson,
            AiProposalKind.LexicalPatch or AiProposalKind.QuizPatch => payload.JsonBody?.GetRawText() ?? "{}",
            _ => payload.Body ?? string.Empty,
        };
    }

    private static AuthoringContentPayload ApplyProposedContent(AuthoringContentPayload payload, AiAuthoringProposal proposal, int? cursorOffset)
    {
        return proposal.Kind switch
        {
            AiProposalKind.ReplaceDocument => payload with { Body = proposal.ProposedContent },
            AiProposalKind.InsertAtCursor => payload with { Body = Insert(payload.Body ?? string.Empty, proposal.ProposedContent, cursorOffset) },
            AiProposalKind.LexicalPatch or AiProposalKind.QuizPatch => payload with { JsonBody = ParseJson(proposal.ProposedContent) },
            AiProposalKind.MetadataPatch => ApplyMetadataProposal(payload, proposal.ProposedContent),
            _ => throw new ArgumentOutOfRangeException(nameof(proposal.Kind)),
        };
    }

    private static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static AuthoringContentPayload ApplyMetadataProposal(
        AuthoringContentPayload current,
        string providerOutput)
    {
        var proposed = JsonSerializer.Deserialize<AuthoringContentPayload>(providerOutput, JsonOptions)
                       ?? throw new ArgumentException("AI metadata proposal is invalid.");

        // Metadata proposals may change presentation and completion metadata only.
        // The persisted content kind, structured state, activity contract, and media
        // reference always come from the current draft, never from provider output.
        return current with
        {
            Title = proposed.Title,
            Slug = proposed.Slug,
            Description = proposed.Description,
            IsRequired = proposed.IsRequired,
            EstimatedMinutes = proposed.EstimatedMinutes,
            EstimatedMinutesSource = proposed.EstimatedMinutesSource,
            Visibility = proposed.Visibility,
        };
    }

    private static void EnsureProposalKindAllowed(AuthoringContentPayload payload, AiProposalKind kind)
    {
        var allowed = payload switch
        {
            { Type: ProgramContentType.Lesson, LessonFormat: LessonContentFormat.Video } =>
                kind == AiProposalKind.MetadataPatch,
            { Type: ProgramContentType.Lesson, LessonFormat: LessonContentFormat.Lexical } =>
                kind is AiProposalKind.LexicalPatch or AiProposalKind.MetadataPatch,
            { Type: ProgramContentType.Questionnaire } =>
                kind is AiProposalKind.QuizPatch or AiProposalKind.MetadataPatch,
            _ => kind is AiProposalKind.ReplaceDocument or AiProposalKind.InsertAtCursor or AiProposalKind.MetadataPatch,
        };

        if (!allowed)
            throw new AiProposalKindNotAllowedException(
                kind,
                "The requested AI proposal kind cannot safely modify this lesson format.");
    }

    private static string Insert(string source, string value, int? cursorOffset)
    {
        var offset = Math.Clamp(cursorOffset ?? source.Length, 0, source.Length);
        return string.Concat(source.AsSpan(0, offset), value, source.AsSpan(offset));
    }

    private static AuthoringContentPayload DeserializePayload(string json) =>
        JsonSerializer.Deserialize<AuthoringContentPayload>(json, JsonOptions)
        ?? throw new InvalidOperationException("Authoring draft payload is invalid.");

    private static AuthoringDraftDto ToDraftDto(ProgramContentDraft draft) => new(
        draft.Id,
        draft.ProgramId,
        draft.ContentId,
        DeserializePayload(draft.PayloadJson),
        draft.BasePublishedVersion,
        draft.Revision,
        draft.ETag,
        draft.LastEditedBy,
        draft.LastEditedAt);

    private static AiAuthoringMessageDto ToDto(AiAuthoringMessage message) =>
        new(message.Id, message.Role, message.Content, message.RunId, message.CreatedAt);

    private static AiProposalDto ToDto(AiAuthoringProposal proposal) => new(
        proposal.Id,
        proposal.RunId,
        proposal.BaseDraftRevision,
        proposal.Kind,
        proposal.Status,
        proposal.OriginalContent,
        proposal.ProposedContent,
        proposal.ProposedAt);

    private static AiCreditUsageDto Usage(AiAuthoringRun run, AiCreditBalance balance) => new(
        balance.AvailableSoftUnits,
        run.MaximumEstimatedCost,
        run.InputTokens,
        run.OutputTokens,
        run.SettledCost,
        run.ReleasedAmount);

    private static bool IsQuotaError(string code) =>
        code.Contains("Quota", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static void ValidateActor(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("AI authoring requires a tenant-scoped user actor.");
    }
}

public sealed class AiAuthoringExecutionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class AiAuthoringIdempotencyConflictException(string message) : InvalidOperationException(message);

public sealed class AiProposalKindNotAllowedException(AiProposalKind kind, string message)
    : InvalidOperationException(message)
{
    public AiProposalKind Kind { get; } = kind;
}

internal sealed class AuthoringAiBackgroundService(
    IAuthoringAiRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AuthoringAiBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverIncompleteRuns(stoppingToken).ConfigureAwait(false);
        await foreach (var runId in queue.ReadAll(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuthoringAiService>();
                await service.ProcessRun(runId, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process AI authoring run {RunId}", runId);
            }
        }
    }

    private async Task RecoverIncompleteRuns(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var ids = await db.Set<AiAuthoringRun>()
            .AsNoTracking()
            .Where(item => item.Status == AiAuthoringRunStatus.Reserved ||
                           item.Status == AiAuthoringRunStatus.Running)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var id in ids)
            await queue.Enqueue(id, cancellationToken).ConfigureAwait(false);
    }
}
