using System.Data.Common;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.API.Core.Security;

/// <summary>Shares the scoped DbContext's transaction boundary; rollback and retry discard staged counts.</summary>
internal sealed class RefreshTokenLifecycleMetricBuffer : DbTransactionInterceptor
{
    private readonly Dictionary<Guid, List<RefreshTokenLifecycleEvent>> _pending = [];

    public void Enlist(Guid transactionId, RefreshTokenLifecycleEvent lifecycleEvent)
    {
        if (!_pending.TryGetValue(transactionId, out var events))
        {
            events = [];
            _pending.Add(transactionId, events);
        }
        events.Add(lifecycleEvent);
    }

    public void Commit(Guid transactionId)
    {
        if (!_pending.Remove(transactionId, out var events)) { return; }
        foreach (var lifecycleEvent in events) { RefreshTokenLifecycleMetrics.RecordPersisted(lifecycleEvent); }
    }

    public void Discard(Guid transactionId) => _pending.Remove(transactionId);

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Commit(eventData.TransactionId);

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Commit(eventData.TransactionId);
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Discard(eventData.TransactionId);

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Discard(eventData.TransactionId);
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Discard(eventData.TransactionId);

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Discard(eventData.TransactionId);
        return Task.CompletedTask;
    }
}
