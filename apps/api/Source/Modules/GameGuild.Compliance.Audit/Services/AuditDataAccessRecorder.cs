using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record AuditAccessedRecord(Guid? TenantId, DateTime RecordDateUtc);

public interface IAuditDataAccessRecorder
{
    Task RecordAsync(IEnumerable<AuditAccessedRecord> records);
    Task RecordAsync(IEnumerable<AuditAccessedRecord> records, CancellationToken cancellationToken);
}

/// <summary>Records successful row reads in a separate database scope, including partially consumed streams.</summary>
public sealed class AuditDataAccessRecorder(
    IServiceScopeFactory scopeFactory,
    IActorContextAccessor actors,
    TimeProvider timeProvider,
    ILogger<AuditDataAccessRecorder> logger) : IAuditDataAccessRecorder
{
    public Task RecordAsync(IEnumerable<AuditAccessedRecord> records) => RecordAsync(records, CancellationToken.None);

    public async Task RecordAsync(IEnumerable<AuditAccessedRecord> records, CancellationToken cancellationToken)
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is null || actor.TenantId == Guid.Empty) { return; }
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tenant = actor.TenantId.Value;
        var observations = records.Where(record => record.TenantId == tenant && record.RecordDateUtc <= now)
            .GroupBy(record => DateOnly.FromDateTime(record.RecordDateUtc))
            .Select(group => new AuditDataAccessObservation
            {
                TenantId = tenant, RecordDateUtc = group.Key, ReadCount = group.LongCount(), CreatedAt = now, UpdatedAt = now
            }).ToArray();
        if (observations.Length == 0) { return; }
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            context.Set<AuditDataAccessObservation>().AddRange(observations);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // A telemetry outage must not prevent authorized audit reads. The model marks missing observations explicitly.
            logger.LogWarning(exception, "Audit read observations could not be saved for tenant {TenantId}", tenant);
        }
    }
}
