using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using GameGuild.AI;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Resources;
using GameGuild.Social.Blog.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Blog.Authoring;

/// <summary>Blog copilot surface: entitlement, conversations, streaming runs, proposals, and apply/discard.</summary>
public interface IBlogAuthoringAiService
{
    /// <summary>Returns the acting author's AI credit wallet snapshot.</summary>
    Task<BlogAiEntitlementDto> GetEntitlement(Guid tenantId, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Lists the author's copilot conversations (with messages) for one post.</summary>
    Task<IReadOnlyList<BlogAiConversationDto>> GetConversations(Guid tenantId, Guid actorId, Guid postId, CancellationToken cancellationToken);

    /// <summary>Creates (or idempotently replays) an AI run against the post's current revision.</summary>
    Task<BlogAiRunDto> CreateRun(Guid tenantId, Guid actorId, Guid postId, BlogAiRunRequest request, CancellationToken cancellationToken);

    /// <summary>Fetches one run owned by the acting author.</summary>
    Task<BlogAiRunDto> GetRun(Guid tenantId, Guid actorId, Guid postId, Guid runId, CancellationToken cancellationToken);

    /// <summary>Cancels a queued, reserved, or running run and releases its reservation.</summary>
    Task<BlogAiRunDto> CancelRun(Guid tenantId, Guid actorId, Guid postId, Guid runId, CancellationToken cancellationToken);

    /// <summary>Streams persisted run events in sequence order until the run reaches a terminal state.</summary>
    IAsyncEnumerable<BlogAiStreamEventDto> StreamRun(Guid tenantId, Guid actorId, Guid postId, Guid runId, long afterSequence, CancellationToken cancellationToken);

    /// <summary>Applies a pending proposal to the post (revision-guarded) and returns the updated post DTO.</summary>
    Task<BlogPostDto> ApplyProposal(Guid tenantId, Guid actorId, Guid postId, Guid proposalId, ApplyBlogAiProposalRequest request, CancellationToken cancellationToken);

    /// <summary>Discards a pending proposal without touching the post.</summary>
    Task<BlogAiProposalDto> DiscardProposal(Guid tenantId, Guid actorId, Guid postId, Guid proposalId, CancellationToken cancellationToken);

    /// <summary>Processes one run: executes the provider stream, persists deltas, settles credits, materializes the proposal.</summary>
    Task ProcessRun(Guid runId, CancellationToken cancellationToken);
}

/// <summary>In-process queue feeding the blog AI background worker.</summary>
internal interface IBlogAuthoringAiRunQueue
{
    /// <summary>Enqueues a run for background processing.</summary>
    ValueTask Enqueue(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Reads enqueued run ids until cancellation.</summary>
    IAsyncEnumerable<Guid> ReadAll(CancellationToken cancellationToken);
}

internal sealed class BlogAuthoringAiRunQueue : IBlogAuthoringAiRunQueue
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

internal sealed class BlogAuthoringAiService(
    IApplicationDbContext db,
    IAiOrchestrator ai,
    IAiCreditWalletService credits,
    IResourceQuotaEnforcer quotaEnforcer,
    IBlogAuthoringAiRunQueue queue,
    TimeProvider timeProvider,
    ILogger<BlogAuthoringAiService> logger) : IBlogAuthoringAiService
{
    private const string ServiceCode = "blog-authoring";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BlogAiEntitlementDto> GetEntitlement(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var balance = await credits.GetBalanceAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        return new BlogAiEntitlementDto(balance.AvailableSoftUnits, balance.ReservedSoftUnits, balance.SettledSoftUnits);
    }

    public async Task<IReadOnlyList<BlogAiConversationDto>> GetConversations(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var conversations = await db.Set<BlogAiConversation>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.AuthorId == actorId && item.BlogPostId == postId && item.DeletedAt == null)
            .OrderByDescending(item => item.LastMessageAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (conversations.Count == 0)
        {
            return [];
        }

        var ids = conversations.Select(item => item.Id).ToArray();
        var messages = await db.Set<BlogAiMessage>()
            .AsNoTracking()
            .Where(item => ids.Contains(item.ConversationId))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return conversations.Select(conversation => new BlogAiConversationDto(
            conversation.Id,
            conversation.BlogPostId,
            conversation.AuthorId,
            conversation.LastMessageAt,
            messages.Where(message => message.ConversationId == conversation.Id).Select(ToDto).ToArray())).ToArray();
    }

    public async Task<BlogAiRunDto> CreateRun(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        BlogAiRunRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);

        var duplicate = await db.Set<BlogAiRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ActorId == actorId && item.IdempotencyKey == request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            if (duplicate.BlogPostId != postId ||
                duplicate.BasePostRevision != request.PostRevision ||
                duplicate.ProposalKind != request.ProposalKind ||
                !string.Equals(duplicate.Instruction, request.Instruction.Trim(), StringComparison.Ordinal) ||
                !string.Equals(duplicate.Selection, NormalizeOptional(request.Selection), StringComparison.Ordinal))
            {
                throw new BlogAiIdempotencyConflictException(
                    "The AI idempotency key is already bound to another authoring request.");
            }

            return await ToDto(duplicate, cancellationToken).ConfigureAwait(false);
        }

        var post = await FindOwnedPost(postId, actorId, cancellationToken).ConfigureAwait(false);
        if (post.Revision != request.PostRevision)
        {
            throw new BlogRevisionConflictException(request.PostRevision, post.Revision);
        }

        EnsureProposalKindAllowed(post.Format, request.ProposalKind);

        var generationRequest = BuildGenerationRequest(
            request.ProposalKind,
            request.Instruction,
            request.Selection,
            post,
            maximumOutputTokens: null);
        var resolved = await ai.DescribeGenerateAsync(new AiExecutionActor(tenantId, actorId), generationRequest, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            throw new BlogAiExecutionException(resolved.Error.Code, resolved.Error.Description);
        }

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
            var conversation = await ResolveConversation(request.ConversationId, tenantId, actorId, postId, now, cancellationToken).ConfigureAwait(false);
            var run = BlogAiRun.Create(
                tenantId,
                actorId,
                postId,
                conversation.Id,
                post.Revision,
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
            db.Set<BlogAiRun>().Add(run);
            conversation.Touch(now);
            db.Set<BlogAiMessage>().Add(BlogAiMessage.Create(conversation.Id, run.Id, "user", request.Instruction, now));
            AddEvent(run.Id, 1, "status", run.Status.ToString(), null, null, now);
            await credits.ReserveAsync(
                run.Id,
                tenantId,
                actorId,
                ServiceCode,
                quote,
                $"blog-authoring:{tenantId:N}:{actorId:N}:{request.IdempotencyKey}",
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
            {
                await ReleaseQuota(tenantId, actorId, maximumQuotaTokens, releaseRequest: true, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    public async Task<BlogAiRunDto> GetRun(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await FindOwnedRun(tenantId, actorId, postId, runId, cancellationToken).ConfigureAwait(false);
        return await ToDto(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BlogAiRunDto> CancelRun(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await FindOwnedRun(tenantId, actorId, postId, runId, cancellationToken).ConfigureAwait(false);
        if (run.Status is BlogAiRunStatus.Completed or BlogAiRunStatus.Failed or BlogAiRunStatus.Cancelled)
        {
            return await ToDto(run, cancellationToken).ConfigureAwait(false);
        }

        if (run.Status == BlogAiRunStatus.Running)
        {
            run.RequestCancellation(timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await ToDto(run, cancellationToken).ConfigureAwait(false);
        }

        await ReleaseAndCancel(
            run,
            await NextSequence(run.Id, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return await ToDto(run, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<BlogAiStreamEventDto> StreamRun(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        Guid runId,
        long afterSequence,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _ = await FindOwnedRun(tenantId, actorId, postId, runId, cancellationToken).ConfigureAwait(false);
        var cursor = Math.Max(0, afterSequence);
        while (!cancellationToken.IsCancellationRequested)
        {
            var events = await db.Set<BlogAiStreamEvent>()
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

            var status = await db.Set<BlogAiRun>()
                .AsNoTracking()
                .Where(item => item.Id == runId)
                .Select(item => item.Status)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            if (status is BlogAiRunStatus.Completed or BlogAiRunStatus.Failed or BlogAiRunStatus.Cancelled)
            {
                yield break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<BlogPostDto> ApplyProposal(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        Guid proposalId,
        ApplyBlogAiProposalRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var proposal = await db.Set<BlogAiProposal>()
            .SingleOrDefaultAsync(item => item.Id == proposalId && item.BlogPostId == postId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI proposal was not found.");
        _ = await FindOwnedRun(tenantId, actorId, postId, proposal.RunId, cancellationToken).ConfigureAwait(false);
        var post = await FindOwnedPost(postId, actorId, cancellationToken).ConfigureAwait(false);
        if (post.Revision != request.PostRevision)
        {
            throw new BlogRevisionConflictException(request.PostRevision, post.Revision);
        }

        proposal.EnsureApplicableTo(post.Revision);

        ApplyProposedContent(post, proposal, request.CursorOffset, actorId);
        proposal.MarkApplied(actorId, timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentRevision = await db.Set<BlogPost>()
                .AsNoTracking()
                .Where(item => item.Id == post.Id && item.DeletedAt == null)
                .Select(item => (int?)item.Revision)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Blog post was not found.");
            if (currentRevision != request.PostRevision)
            {
                throw new BlogRevisionConflictException(request.PostRevision, currentRevision);
            }

            throw await ProposalConflict(proposalId, cancellationToken).ConfigureAwait(false);
        }
        return ToPostDto(post);
    }

    public async Task<BlogAiProposalDto> DiscardProposal(
        Guid tenantId,
        Guid actorId,
        Guid postId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var proposal = await db.Set<BlogAiProposal>()
            .SingleOrDefaultAsync(item => item.Id == proposalId && item.BlogPostId == postId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI proposal was not found.");
        _ = await FindOwnedRun(tenantId, actorId, postId, proposal.RunId, cancellationToken).ConfigureAwait(false);
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
        var run = await db.Set<BlogAiRun>()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null || run.Status is BlogAiRunStatus.Completed or BlogAiRunStatus.Failed or BlogAiRunStatus.Cancelled)
        {
            return;
        }

        var nextSequence = await NextSequence(runId, cancellationToken).ConfigureAwait(false);
        if (run.Status == BlogAiRunStatus.Running)
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

        var post = await db.Set<BlogPost>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == run.BlogPostId && item.DeletedAt == null, cancellationToken)
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
            if (await IsCancellationRequested(run.Id, cancellationToken).ConfigureAwait(false))
            {
                throw new OperationCanceledException("AI generation was cancelled by the author.");
            }

            var completion = await ai.GenerateForActorStreamingWithReservedQuotaAsync(
                new AiExecutionActor(run.TenantId!.Value, run.ActorId),
                BuildGenerationRequest(run, post),
                async (delta, streamCancellationToken) =>
                {
                    if (string.IsNullOrEmpty(delta))
                    {
                        return;
                    }

                    if (await IsCancellationRequested(run.Id, streamCancellationToken).ConfigureAwait(false))
                    {
                        throw new OperationCanceledException("AI generation was cancelled by the author.");
                    }

                    AddEvent(run.Id, nextSequence++, "delta", BlogAiRunStatus.Running.ToString(), delta, null, timeProvider.GetUtcNow());
                    await db.SaveChangesAsync(streamCancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            if (completion.IsFailure)
            {
                throw new BlogAiExecutionException(completion.Error.Code, completion.Error.Description);
            }

            if (await IsCancellationRequested(run.Id, cancellationToken).ConfigureAwait(false))
            {
                throw new OperationCanceledException("AI generation was cancelled by the author.");
            }

            var inputTokens = completion.Value.Usage.InputTokens ?? 0;
            var outputTokens = completion.Value.Usage.OutputTokens ?? 0;
            var actualQuotaTokens = checked(inputTokens + outputTokens);
            var reservedQuotaTokens = checked(run.MaximumInputTokens + run.MaximumOutputTokens);
            if (actualQuotaTokens > reservedQuotaTokens)
            {
                throw new BlogAiExecutionException("AI_USAGE_EXCEEDED_RESERVATION", "Provider usage exceeded the reserved token envelope.");
            }

            var originalContent = OriginalContent(post, run.ProposalKind);
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
                var proposal = BlogAiProposal.Create(
                    run.Id,
                    run.BlogPostId,
                    run.BasePostRevision,
                    run.ProposalKind,
                    originalContent,
                    proposedContent,
                    timeProvider.GetUtcNow());
                proposal.TenantId = run.TenantId;
                db.Set<BlogAiProposal>().Add(proposal);
                db.Set<BlogAiMessage>().Add(BlogAiMessage.Create(
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
                var completedEvent = new BlogAiStreamEventDto(
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
                {
                    await finalizationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                if (finalizationTransaction is not null)
                {
                    await finalizationTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    relationalContext!.ChangeTracker.Clear();
                    run = await db.Set<BlogAiRun>()
                        .SingleAsync(item => item.Id == runId, CancellationToken.None)
                        .ConfigureAwait(false);
                    nextSequence = await NextSequence(runId, CancellationToken.None).ConfigureAwait(false);
                }
                throw;
            }
            await ReleaseQuota(run.TenantId!.Value, run.ActorId, reservedQuotaTokens - actualQuotaTokens, releaseRequest: false, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await ReleaseAndCancel(run, nextSequence, CancellationToken.None).ConfigureAwait(false);
        }
        catch (BlogAiExecutionException exception)
        {
            var code = IsQuotaError(exception.Code) ? "AI_QUOTA_EXCEEDED" : exception.Code;
            await ReleaseAndFail(run, nextSequence, code, exception.Message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ReleaseAndFail(run, nextSequence, "AI_EXECUTION_FAILED", exception.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReleaseAndFail(BlogAiRun run, long sequence, string code, string message, CancellationToken cancellationToken)
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
        catch (KeyNotFoundException)
        {
            // A run that never reached the wallet has nothing to release.
        }
        run.Fail(code, message, released, timeProvider.GetUtcNow());
        var streamEvent = new BlogAiStreamEventDto(sequence, "error", null, run.Id, run.Status.ToString(), ErrorCode: code);
        AddEvent(run.Id, sequence, "error", run.Status.ToString(), null, JsonSerializer.Serialize(streamEvent, JsonOptions), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseQuota(run.TenantId!.Value, run.ActorId, checked(run.MaximumInputTokens + run.MaximumOutputTokens), releaseRequest: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleaseAndCancel(BlogAiRun run, long sequence, CancellationToken cancellationToken)
    {
        long released = 0;
        try
        {
            var reservation = await credits.ReleaseAsync(
                run.Id,
                "AI_CANCELLED",
                $"release:{run.Id:N}",
                cancellationToken).ConfigureAwait(false);
            released = reservation.ReleasedSoftUnits;
        }
        catch (InvalidOperationException)
        {
            // An already settled reservation is terminal and cannot be released.
        }
        catch (KeyNotFoundException)
        {
            // A run that never reached the wallet has nothing to release.
        }
        run.Cancel(released, timeProvider.GetUtcNow());
        var streamEvent = new BlogAiStreamEventDto(
            sequence,
            "cancelled",
            null,
            run.Id,
            run.Status.ToString(),
            ErrorCode: "AI_CANCELLED");
        AddEvent(
            run.Id,
            sequence,
            "cancelled",
            run.Status.ToString(),
            null,
            JsonSerializer.Serialize(streamEvent, JsonOptions),
            timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseQuota(
            run.TenantId!.Value,
            run.ActorId,
            checked(run.MaximumInputTokens + run.MaximumOutputTokens),
            releaseRequest: false,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> IsCancellationRequested(Guid runId, CancellationToken cancellationToken) =>
        db.Set<BlogAiRun>()
            .AsNoTracking()
            .Where(item => item.Id == runId)
            .Select(item => item.ErrorCode == "AI_CANCEL_REQUESTED")
            .SingleAsync(cancellationToken);

    private async Task ReserveQuota(Guid tenantId, Guid actorId, long maximumTokens, CancellationToken cancellationToken)
    {
        var request = await quotaEnforcer.TryAtomicConsumeAsync(
            tenantId,
            ResourceUsageType.AiRequests,
            1,
            cancellationToken).ConfigureAwait(false);
        if (!request.Success)
        {
            throw new BlogAiExecutionException("AI_QUOTA_EXCEEDED", "The AI request quota has been exceeded.");
        }

        var tokens = await quotaEnforcer.TryAtomicConsumeAsync(
            tenantId,
            ResourceUsageType.AiTokens,
            maximumTokens,
            cancellationToken).ConfigureAwait(false);
        if (tokens.Success)
        {
            return;
        }

        await ReleaseQuota(tenantId, actorId, 0, releaseRequest: true, CancellationToken.None).ConfigureAwait(false);
        throw new BlogAiExecutionException("AI_QUOTA_EXCEEDED", "The AI token quota has been exceeded.");
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
            {
                _ = await quotaEnforcer.DecrementUsageAsync(
                    tenantId,
                    ResourceUsageType.AiTokens,
                    tokens,
                    actorId,
                    ServiceCode,
                    cancellationToken).ConfigureAwait(false);
            }

            if (releaseRequest)
            {
                _ = await quotaEnforcer.DecrementUsageAsync(
                    tenantId,
                    ResourceUsageType.AiRequests,
                    1,
                    actorId,
                    ServiceCode,
                    cancellationToken).ConfigureAwait(false);
            }
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

    private async Task<BlogAiConversation> ResolveConversation(
        Guid? requestedId,
        Guid tenantId,
        Guid actorId,
        Guid postId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = db.Set<BlogAiConversation>()
            .Where(item => item.TenantId == tenantId && item.AuthorId == actorId && item.BlogPostId == postId && item.DeletedAt == null);
        BlogAiConversation? conversation;
        if (requestedId.HasValue)
        {
            conversation = await query.SingleOrDefaultAsync(item => item.Id == requestedId.Value, cancellationToken).ConfigureAwait(false);
            if (conversation is null)
            {
                throw new KeyNotFoundException("AI conversation was not found.");
            }
        }
        else
        {
            conversation = await query.OrderByDescending(item => item.LastMessageAt).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (conversation is null)
            {
                conversation = BlogAiConversation.Create(tenantId, postId, actorId, now);
                db.Set<BlogAiConversation>().Add(conversation);
            }
        }
        return conversation;
    }

    /// <summary>Loads a live post and asserts the actor is a primary or co-author (edit rights).</summary>
    private async Task<BlogPost> FindOwnedPost(Guid postId, Guid actorId, CancellationToken cancellationToken)
    {
        var post = await db.Set<BlogPost>()
            .SingleOrDefaultAsync(item => item.Id == postId && item.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Blog post was not found.");
        var isAuthor = post.PrimaryAuthorId == actorId ||
                       await db.Set<BlogPostAuthor>().AnyAsync(author => author.BlogPostId == postId && author.UserId == actorId, cancellationToken).ConfigureAwait(false);
        if (!isAuthor)
        {
            throw new KeyNotFoundException("Blog post was not found.");
        }

        return post;
    }

    private async Task<BlogAiRun> FindOwnedRun(Guid tenantId, Guid actorId, Guid postId, Guid runId, CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        return await db.Set<BlogAiRun>()
            .SingleOrDefaultAsync(item => item.Id == runId && item.TenantId == tenantId && item.ActorId == actorId && item.BlogPostId == postId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI authoring run was not found.");
    }

    private async Task<BlogAiRunDto> ToDto(BlogAiRun run, CancellationToken cancellationToken)
    {
        var proposal = await db.Set<BlogAiProposal>().AsNoTracking().SingleOrDefaultAsync(item => item.RunId == run.Id, cancellationToken).ConfigureAwait(false);
        AiCreditBalance balance;
        try
        {
            balance = await credits.GetBalanceAsync(run.TenantId!.Value, run.ActorId, cancellationToken).ConfigureAwait(false);
        }
        catch (InsufficientAiCreditsException)
        {
            balance = new AiCreditBalance(0, 0, run.SettledCost);
        }
        return new BlogAiRunDto(
            run.Id,
            run.ConversationId,
            run.BlogPostId,
            run.BasePostRevision,
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
        db.Set<BlogAiStreamEvent>().Add(BlogAiStreamEvent.Create(runId, sequence, type, status, delta, payload, now));

    private async Task<long> NextSequence(Guid runId, CancellationToken cancellationToken)
    {
        var last = await db.Set<BlogAiStreamEvent>()
            .Where(item => item.RunId == runId)
            .Select(item => (long?)item.Sequence)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);
        return (last ?? 0) + 1;
    }

    private async Task<BlogAiProposalStateConflictException> ProposalConflict(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        var currentStatus = await db.Set<BlogAiProposal>()
            .AsNoTracking()
            .Where(item => item.Id == proposalId)
            .Select(item => (BlogAiProposalStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("AI proposal was not found.");
        return new BlogAiProposalStateConflictException(currentStatus);
    }

    private static BlogAiStreamEventDto DeserializeEvent(BlogAiStreamEvent item)
    {
        if (!string.IsNullOrWhiteSpace(item.PayloadJson))
        {
            return JsonSerializer.Deserialize<BlogAiStreamEventDto>(item.PayloadJson, JsonOptions)
                   ?? throw new InvalidOperationException("Persisted AI stream event is invalid.");
        }

        return new BlogAiStreamEventDto(item.Sequence, item.Type, item.Delta, item.RunId, item.Status);
    }

    private static AiGenerateRequest BuildGenerationRequest(BlogAiRun run, BlogPost post) =>
        BuildGenerationRequest(
            run.ProposalKind,
            run.Instruction,
            run.Selection,
            post,
            run.MaximumOutputTokens > 0 ? run.MaximumOutputTokens : null);

    private static AiGenerateRequest BuildGenerationRequest(
        BlogAiProposalKind proposalKind,
        string instruction,
        string? selection,
        BlogPost post,
        int? maximumOutputTokens)
    {
        var outputContract = proposalKind switch
        {
            BlogAiProposalKind.LexicalPatch => """
                Return a JSON object with an operations array. Each operation uses JSON Pointer and is one of:
                {"op":"replace","path":"/root/children/0/children/0/text","value":"new text"},
                {"op":"add","path":"/root/children/-","value":{...}}, or
                {"op":"remove","path":"/root/children/2"}.
                Never change type, version, or key properties and never remove unknown or interactive nodes.
                """,
            BlogAiProposalKind.MetadataPatch => """
                Return a complete JSON metadata object with only these fields: title, excerpt, tags,
                metaTitle, metaDescription, ogImageUrl, twitterCard. Tags must be an array of lowercase strings.
                """,
            BlogAiProposalKind.InsertAtCursor => "Return only the text to insert at the selected cursor position.",
            _ => "Return only the complete replacement body.",
        };
        var prompt = $"""
                      Current blog post (title: {post.Title}):
                      {post.Content}

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
            "You are the GameGuild blog authoring copilot. Preserve the post format and structure. Never claim to have published or directly changed the post.",
            prompt,
            0.4,
            maximumOutputTokens);
    }

    private static string MaterializeProposedContent(BlogAiProposalKind kind, string originalContent, string providerOutput) =>
        kind switch
        {
            BlogAiProposalKind.LexicalPatch => BlogStructuredPatch.Apply(originalContent, providerOutput),
            BlogAiProposalKind.MetadataPatch => JsonSerializer.Serialize(
                ApplyMetadataProposal(DeserializeMetadata(originalContent), providerOutput),
                JsonOptions),
            _ => providerOutput,
        };

    private static int EstimateInputTokens(AiGenerateRequest request)
    {
        // One token cannot encode less than one byte of the UTF-8 provider input.
        // Reserving by byte count is intentionally conservative and prevents a
        // tokenizer-specific input count from exceeding the financial envelope.
        // BuildGenerationRequest always supplies the authoring system prompt.
        // Keeping that invariant explicit avoids silently under-reserving input
        // credits if the private request builder is changed later.
        var byteCount = Encoding.UTF8.GetByteCount(request.Prompt) +
                        Encoding.UTF8.GetByteCount(request.SystemPrompt!);
        return Math.Max(1, byteCount);
    }

    private static string OriginalContent(BlogPost post, BlogAiProposalKind kind) =>
        kind switch
        {
            BlogAiProposalKind.MetadataPatch => JsonSerializer.Serialize(
                BlogMetadataSnapshot.From(post),
                JsonOptions),
            BlogAiProposalKind.LexicalPatch => post.JsonBody ?? "{}",
            _ => post.Content,
        };

    /// <summary>Applies the proposal onto the tracked post via a revision-guarded <see cref="BlogPost.ApplyDraftEdit"/>.</summary>
    private static void ApplyProposedContent(BlogPost post, BlogAiProposal proposal, int? cursorOffset, Guid actorId)
    {
        switch (proposal.Kind)
        {
            case BlogAiProposalKind.ReplaceDocument:
                post.ApplyDraftEdit(
                    proposal.BasePostRevision,
                    title: null,
                    content: proposal.ProposedContent,
                    jsonBody: null,
                    excerpt: null,
                    tags: null,
                    metaTitle: null,
                    metaDescription: null,
                    ogImageUrl: null,
                    canonicalUrlOverride: null,
                    twitterCard: null,
                    structuredDataOverride: null,
                    allowComments: null,
                    readTimeMinutes: post.Format == BlogContentFormat.Markdown
                        ? BlogReadTimeEstimator.Estimate(post.Format, proposal.ProposedContent, null)
                        : null);
                break;
            case BlogAiProposalKind.InsertAtCursor:
                var merged = Insert(post.Content, proposal.ProposedContent, cursorOffset);
                post.ApplyDraftEdit(
                    proposal.BasePostRevision,
                    title: null,
                    content: merged,
                    jsonBody: null,
                    excerpt: null,
                    tags: null,
                    metaTitle: null,
                    metaDescription: null,
                    ogImageUrl: null,
                    canonicalUrlOverride: null,
                    twitterCard: null,
                    structuredDataOverride: null,
                    allowComments: null,
                    readTimeMinutes: post.Format == BlogContentFormat.Markdown
                        ? BlogReadTimeEstimator.Estimate(post.Format, merged, null)
                        : null);
                break;
            case BlogAiProposalKind.LexicalPatch:
                post.ApplyDraftEdit(
                    proposal.BasePostRevision,
                    title: null,
                    content: null,
                    jsonBody: proposal.ProposedContent,
                    excerpt: null,
                    tags: null,
                    metaTitle: null,
                    metaDescription: null,
                    ogImageUrl: null,
                    canonicalUrlOverride: null,
                    twitterCard: null,
                    structuredDataOverride: null,
                    allowComments: null,
                    readTimeMinutes: BlogReadTimeEstimator.Estimate(BlogContentFormat.Lexical, null, proposal.ProposedContent));
                break;
            case BlogAiProposalKind.MetadataPatch:
                var proposed = ApplyMetadataProposal(BlogMetadataSnapshot.From(post), proposal.ProposedContent);
                post.ApplyDraftEdit(
                    proposal.BasePostRevision,
                    title: proposed.Title,
                    content: null,
                    jsonBody: null,
                    excerpt: proposed.Excerpt,
                    tags: proposed.Tags,
                    metaTitle: proposed.MetaTitle,
                    metaDescription: proposed.MetaDescription,
                    ogImageUrl: proposed.OgImageUrl,
                    canonicalUrlOverride: null,
                    twitterCard: proposed.TwitterCard,
                    structuredDataOverride: null,
                    allowComments: null,
                    readTimeMinutes: null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(proposal.Kind));
        }
    }

    private static BlogMetadataSnapshot ApplyMetadataProposal(BlogMetadataSnapshot current, string providerOutput)
    {
        var proposed = JsonSerializer.Deserialize<BlogMetadataProposal>(providerOutput, JsonOptions)
                       ?? throw new ArgumentException("AI metadata proposal is invalid.");

        // Metadata proposals may change presentation and SEO metadata only. The
        // persisted body, format, slug, canonical override, and structured data
        // always come from the current post, never from provider output.
        return current with
        {
            Title = proposed.Title ?? current.Title,
            Excerpt = proposed.Excerpt ?? current.Excerpt,
            Tags = proposed.Tags is { Count: > 0 } ? BlogPost.NormalizeTags(proposed.Tags) : current.Tags,
            MetaTitle = proposed.MetaTitle ?? current.MetaTitle,
            MetaDescription = proposed.MetaDescription ?? current.MetaDescription,
            OgImageUrl = proposed.OgImageUrl ?? current.OgImageUrl,
            TwitterCard = proposed.TwitterCard ?? current.TwitterCard,
        };
    }

    private static BlogMetadataSnapshot DeserializeMetadata(string json) =>
        JsonSerializer.Deserialize<BlogMetadataSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("Blog metadata snapshot is invalid.");

    private static void EnsureProposalKindAllowed(BlogContentFormat format, BlogAiProposalKind kind)
    {
        var allowed = format switch
        {
            BlogContentFormat.Lexical => kind is BlogAiProposalKind.LexicalPatch or BlogAiProposalKind.MetadataPatch,
            _ => kind is BlogAiProposalKind.ReplaceDocument or BlogAiProposalKind.InsertAtCursor or BlogAiProposalKind.MetadataPatch,
        };

        if (!allowed)
        {
            throw new BlogAiProposalKindNotAllowedException(
                kind,
                "The requested AI proposal kind cannot safely modify this post format.");
        }
    }

    private static string Insert(string source, string value, int? cursorOffset)
    {
        var offset = Math.Clamp(cursorOffset ?? source.Length, 0, source.Length);
        return string.Concat(source.AsSpan(0, offset), value, source.AsSpan(offset));
    }

    private static BlogPostDto ToPostDto(BlogPost post) => new(
        post.Id,
        post.Title,
        post.Slug,
        post.Excerpt,
        post.Content,
        post.JsonBody,
        post.Format,
        post.Tags,
        post.MetaTitle,
        post.MetaDescription,
        post.OgImageUrl,
        post.TwitterCard,
        post.Status,
        post.Revision,
        post.UpdatedAt);

    private static BlogAiMessageDto ToDto(BlogAiMessage message) =>
        new(message.Id, message.Role, message.Content, message.RunId, message.CreatedAt);

    private static BlogAiProposalDto ToDto(BlogAiProposal proposal) => new(
        proposal.Id,
        proposal.RunId,
        proposal.BasePostRevision,
        proposal.Kind,
        proposal.Status,
        proposal.OriginalContent,
        proposal.ProposedContent,
        proposal.ProposedAt);

    private static BlogAiCreditUsageDto Usage(BlogAiRun run, AiCreditBalance balance) => new(
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
        {
            throw new UnauthorizedAccessException("AI authoring requires a tenant-scoped user actor.");
        }
    }
}

/// <summary>Provider-facing metadata payload mirrored into <see cref="BlogPost"/> SEO fields on apply.</summary>
/// <param name="Title">Post title.</param>
/// <param name="Excerpt">Listing excerpt.</param>
/// <param name="Tags">Lowercase tags.</param>
/// <param name="MetaTitle">SEO title override.</param>
/// <param name="MetaDescription">SEO description override.</param>
/// <param name="OgImageUrl">Open Graph image.</param>
/// <param name="TwitterCard">Twitter card type.</param>
public sealed record BlogMetadataSnapshot(
    string Title,
    string? Excerpt,
    IReadOnlyList<string> Tags,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImageUrl,
    string TwitterCard)
{
    /// <summary>Builds the snapshot from a live post.</summary>
    public static BlogMetadataSnapshot From(BlogPost post) => new(
        post.Title,
        post.Excerpt,
        post.Tags,
        post.MetaTitle,
        post.MetaDescription,
        post.OgImageUrl,
        post.TwitterCard);
}

/// <summary>AI provider output shape for MetadataPatch runs.</summary>
/// <param name="Title">Proposed title.</param>
/// <param name="Excerpt">Proposed excerpt.</param>
/// <param name="Tags">Proposed tags.</param>
/// <param name="MetaTitle">Proposed SEO title.</param>
/// <param name="MetaDescription">Proposed SEO description.</param>
/// <param name="OgImageUrl">Proposed Open Graph image.</param>
/// <param name="TwitterCard">Proposed Twitter card type.</param>
public sealed record BlogMetadataProposal(
    string? Title,
    string? Excerpt,
    IReadOnlyList<string>? Tags,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImageUrl,
    string? TwitterCard);

/// <summary>Post view returned after applying an AI proposal (presentation fields + revision).</summary>
public sealed record BlogPostDto(
    Guid Id,
    string Title,
    string Slug,
    string? Excerpt,
    string Content,
    string? JsonBody,
    BlogContentFormat Format,
    IReadOnlyList<string> Tags,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImageUrl,
    string TwitterCard,
    BlogPostStatus Status,
    int Revision,
    DateTime UpdatedAt);

/// <summary>A blog AI run failed to execute against the provider or quota boundary.</summary>
public sealed class BlogAiExecutionException(string code, string message) : InvalidOperationException(message)
{
    /// <summary>Machine-readable failure code.</summary>
    public string Code { get; } = code;
}

/// <summary>The idempotency key is already bound to a different request payload.</summary>
public sealed class BlogAiIdempotencyConflictException(string message) : InvalidOperationException(message);

/// <summary>The requested proposal kind cannot safely modify the post's format.</summary>
public sealed class BlogAiProposalKindNotAllowedException(BlogAiProposalKind kind, string message)
    : InvalidOperationException(message)
{
    /// <summary>The rejected kind.</summary>
    public BlogAiProposalKind Kind { get; } = kind;
}

/// <summary>
/// Applies the deliberately small JSON patch dialect accepted from the blog copilot
/// (Lexical body edits). Operations mutate only the addressed value, so Lexical nodes
/// unknown to this service survive unchanged.
/// </summary>
public static class BlogStructuredPatch
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>Applies the patch operations to the source JSON document and returns the result.</summary>
    public static string Apply(string sourceJson, string patchJson)
    {
        var document = JsonNode.Parse(sourceJson) ?? throw new ArgumentException("The source document is invalid JSON.", nameof(sourceJson));
        var patch = JsonNode.Parse(patchJson) as JsonObject
                    ?? throw new ArgumentException("The AI patch must be a JSON object.", nameof(patchJson));
        var operations = patch["operations"] as JsonArray
                         ?? throw new ArgumentException("The AI patch must contain an operations array.", nameof(patchJson));
        if (operations.Count is 0 or > 100)
        {
            throw new ArgumentException("The AI patch must contain between 1 and 100 operations.", nameof(patchJson));
        }

        foreach (var node in operations)
        {
            var operation = node as JsonObject
                            ?? throw new ArgumentException("Every AI patch operation must be an object.", nameof(patchJson));
            ApplyOperation(document, operation);
        }

        return document.ToJsonString(JsonOptions);
    }

    private static void ApplyOperation(JsonNode document, JsonObject operation)
    {
        var opNode = operation["op"]
                     ?? throw new ArgumentException("Every AI patch operation requires an op value.", nameof(operation));
        var op = opNode.GetValue<string>().Trim().ToLowerInvariant();
        var pointer = operation["path"]?.GetValue<string>()
                      ?? throw new ArgumentException("Every AI patch operation requires a path value.", nameof(operation));
        var segments = ParsePointer(pointer);
        if (segments.Count == 0)
        {
            throw new ArgumentException("Replacing the structured document root is not allowed.", nameof(operation));
        }

        if (segments.Any(IsProtectedLexicalProperty))
        {
            throw new ArgumentException("Lexical node identity and type metadata cannot be changed by AI.", nameof(operation));
        }

        var (parent, leaf) = ResolveParent(document, segments);
        switch (op)
        {
            case "replace":
                Replace(parent, leaf, operation["value"]?.DeepClone());
                break;
            case "add":
                Add(parent, leaf, operation["value"]?.DeepClone());
                break;
            case "remove":
                Remove(parent, leaf);
                break;
            default:
                throw new ArgumentException($"Unsupported AI patch operation '{op}'.", nameof(operation));
        }
    }

    private static (JsonNode Parent, string Leaf) ResolveParent(JsonNode document, IReadOnlyList<string> segments)
    {
        var current = document;
        for (var index = 0; index < segments.Count - 1; index++)
        {
            current = ResolveChild(current, segments[index])
                      ?? throw new ArgumentException($"AI patch path segment '{segments[index]}' does not exist.");
        }
        return (current, segments[^1]);
    }

    private static JsonNode? ResolveChild(JsonNode node, string segment) => node switch
    {
        JsonObject obj => obj[segment],
        JsonArray array => array[ParseArrayIndex(segment, array.Count, allowAppend: false)],
        _ => throw new ArgumentException("AI patch paths can only traverse objects and arrays."),
    };

    private static void Replace(JsonNode parent, string leaf, JsonNode? value)
    {
        switch (parent)
        {
            case JsonObject obj when obj.ContainsKey(leaf):
                obj[leaf] = value;
                return;
            case JsonArray array:
                array[ParseArrayIndex(leaf, array.Count, allowAppend: false)] = value;
                return;
            default:
                throw new ArgumentException("AI replace operations must address an existing value.");
        }
    }

    private static void Add(JsonNode parent, string leaf, JsonNode? value)
    {
        switch (parent)
        {
            case JsonObject obj:
                obj[leaf] = value;
                return;
            case JsonArray array when leaf == "-":
                array.Add(value);
                return;
            case JsonArray array:
                array.Insert(ParseArrayIndex(leaf, array.Count, allowAppend: true), value);
                return;
            default:
                throw new ArgumentException("AI add operations require an object or array parent.");
        }
    }

    private static void Remove(JsonNode parent, string leaf)
    {
        switch (parent)
        {
            case JsonObject obj:
                var target = obj[leaf] ?? throw new ArgumentException("AI remove operations must address an existing value.");
                EnsureRemovalAllowed(target);
                obj.Remove(leaf);
                return;
            case JsonArray array:
                var index = ParseArrayIndex(leaf, array.Count, allowAppend: false);
                var arrayTarget = array[index];
                EnsureRemovalAllowed(arrayTarget);
                array.RemoveAt(index);
                return;
            default:
                throw new ArgumentException("AI remove operations require an object or array parent.");
        }
    }

    private static void EnsureRemovalAllowed(JsonNode? target)
    {
        if (target is not JsonObject obj)
        {
            return;
        }

        var type = obj["type"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(type) && type is not ("text" or "paragraph" or "heading" or "quote" or "listitem"))
        {
            throw new ArgumentException($"AI cannot remove the interactive or unknown Lexical node type '{type}'.");
        }
    }

    private static IReadOnlyList<string> ParsePointer(string pointer)
    {
        if (pointer.Length == 0)
        {
            return [];
        }

        if (!pointer.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("AI patch paths must use JSON Pointer syntax.", nameof(pointer));
        }

        return pointer.Split('/').Skip(1)
            .Select(static segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();
    }

    private static int ParseArrayIndex(string value, int count, bool allowAppend)
    {
        if (!int.TryParse(value, out var index) || index < 0 || index > count || (!allowAppend && index == count))
        {
            throw new ArgumentException($"'{value}' is not a valid array index.");
        }

        return index;
    }

    private static bool IsProtectedLexicalProperty(string value) =>
        value.Equals("type", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("version", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("key", StringComparison.OrdinalIgnoreCase);
}

internal sealed class BlogAuthoringAiBackgroundService(
    IBlogAuthoringAiRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BlogAuthoringAiBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverIncompleteRuns(stoppingToken).ConfigureAwait(false);
        await foreach (var runId in queue.ReadAll(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IBlogAuthoringAiService>();
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
        var ids = await db.Set<BlogAiRun>()
            .AsNoTracking()
            .Where(item => item.Status == BlogAiRunStatus.Reserved ||
                           item.Status == BlogAiRunStatus.Running)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var id in ids)
        {
            await queue.Enqueue(id, cancellationToken).ConfigureAwait(false);
        }
    }
}
