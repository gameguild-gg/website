using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Core.Security;

/// <summary>Stores audit evidence atomically with token changes; pure denials use a separate scope.</summary>
internal sealed class RefreshTokenLifecycleRecorder(
    ApplicationDbContext context,
    RefreshTokenLifecycleMetricBuffer metrics,
    IServiceScopeFactory scopeFactory,
    IActorContextAccessor actorContextAccessor,
    IDurableEventProducer events,
    IUseCaseOperationContextAccessor operations) : IRefreshTokenLifecycleRecorder
{
    public async Task RecordMutationAsync(RefreshTokenLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        if (lifecycleEvent.Operation == RefreshTokenLifecycleOperation.Rejected)
        {
            throw new ArgumentException("A rejection requires independent audit persistence.", nameof(lifecycleEvent));
        }
        var isReplay = lifecycleEvent.Operation == RefreshTokenLifecycleOperation.ReplayContained;
        if (isReplay && (lifecycleEvent.UserId is null || lifecycleEvent.UserId == Guid.Empty ||
                         lifecycleEvent.TokenId is null || lifecycleEvent.TokenId == Guid.Empty))
        {
            throw new ArgumentException("A replay alert requires persisted token ownership.", nameof(lifecycleEvent));
        }
        if (isReplay && context.Database.IsRelational() && context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Replay containment and its alert must share a database transaction.");
        }

        await PersistAsync(context, metrics, lifecycleEvent, cancellationToken).ConfigureAwait(false);
        if (isReplay)
        {
            var actor = actorContextAccessor.ActorContext;
            var tenantId = lifecycleEvent.TenantId ??
                (actor.IsAuthenticated && actor.SubjectIdAsGuid == lifecycleEvent.UserId ? actor.TenantId : null);
            var operation = operations.Current;
            await events.RecordAsync(new RefreshTokenReplayContainedV1(lifecycleEvent.UserId!.Value,
                lifecycleEvent.TokenId!.Value, lifecycleEvent.SessionId)
            {
                TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
                ActorId = DurableIntegrationEventActors.System,
                AggregateType = "RefreshToken",
                AggregateId = lifecycleEvent.TokenId.Value.ToString(),
                CorrelationId = operation?.CorrelationId ?? Guid.NewGuid(),
                CausationId = operation?.CausationId
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RecordRejectionAsync(RefreshTokenLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        if (lifecycleEvent.Operation != RefreshTokenLifecycleOperation.Rejected)
        {
            throw new ArgumentException("A mutation cannot be recorded outside its owning transaction.", nameof(lifecycleEvent));
        }
        await using var scope = scopeFactory.CreateAsyncScope();
        // This audit is independent of the failed command. Do not inherit its ambient operation
        // accessor and fabricate a successful business-mutation outbox event for an audit row.
        await using var independentContext = new ApplicationDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        await PersistAsync(independentContext,
            scope.ServiceProvider.GetRequiredService<RefreshTokenLifecycleMetricBuffer>(), lifecycleEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task PersistAsync(ApplicationDbContext db, RefreshTokenLifecycleMetricBuffer buffer,
        RefreshTokenLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(lifecycleEvent.Operation) || !Enum.IsDefined(lifecycleEvent.Reason))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleEvent));
        }
        var denied = lifecycleEvent.Operation is RefreshTokenLifecycleOperation.Rejected or RefreshTokenLifecycleOperation.ReplayContained;
        var actor = actorContextAccessor.ActorContext;
        // Stored ownership does not authenticate the requester. Only an already authenticated owner supplies tenant context.
        var tenantId = lifecycleEvent.TenantId ??
            (actor.IsAuthenticated && actor.SubjectIdAsGuid == lifecycleEvent.UserId ? actor.TenantId : null);
        db.Set<AuditLog>().Add(new AuditLog
        {
            ActionType = "Authentication.RefreshToken" + lifecycleEvent.Operation,
            ResourceType = "RefreshToken",
            ResourceId = lifecycleEvent.TokenId?.ToString() ?? lifecycleEvent.UserId?.ToString(),
            UserId = lifecycleEvent.UserId,
            SessionId = lifecycleEvent.SessionId,
            TenantId = tenantId,
            Category = AuditCategory.Authentication,
            Success = !denied,
            RiskLevel = lifecycleEvent.Operation == RefreshTokenLifecycleOperation.ReplayContained ? AuditRiskLevel.Critical :
                denied ? AuditRiskLevel.High : AuditRiskLevel.Low,
            ErrorMessage = denied ? lifecycleEvent.Reason.ToString() : null,
            Description = "Refresh token lifecycle: " + lifecycleEvent.Operation,
            Metadata = JsonSerializer.Serialize(new
            {
                Operation = lifecycleEvent.Operation.ToString(),
                Reason = lifecycleEvent.Reason.ToString(),
                lifecycleEvent.TokenId,
                lifecycleEvent.ParentTokenId
            })
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (db.Database.CurrentTransaction is { } transaction)
        {
            buffer.Enlist(transaction.TransactionId, lifecycleEvent);
        }
        else
        {
            // SaveChanges itself has committed; this also covers non-relational test hosts.
            RefreshTokenLifecycleMetrics.RecordPersisted(lifecycleEvent);
        }
    }
}
