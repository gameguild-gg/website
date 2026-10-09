using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

public interface IScheduledAuditExportRepository
{
    Task AddAsync(ScheduledAuditExport export, CancellationToken cancellationToken);

    Task<ScheduledAuditExport?> GetByIdAsync(Guid exportId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledAuditExport>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledAuditExport>> GetDueAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditExportHistory>> GetHistoryAsync(Guid exportId, Guid tenantId, int limit, CancellationToken cancellationToken);

    Task<AuditExportHistory?> GetHistoryByIdAsync(Guid historyId, Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpiredAuditExportFile>> GetExpiredFilesAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken);

    Task MarkFileExpiredAsync(Guid historyId, CancellationToken cancellationToken);

    Task<bool> TryClaimAsync(
        Guid exportId,
        int expectedVersion,
        DateTime nowUtc,
        DateTime nextRunUtc,
        AuditExportHistory history,
        CancellationToken cancellationToken);

    Task<int> RecoverStaleClaimsAsync(
        DateTime nowUtc,
        TimeSpan staleClaimThreshold,
        CancellationToken cancellationToken);

    Task RecordCompletedAsync(
        Guid exportId,
        Guid historyId,
        int recordCount,
        long fileSizeBytes,
        string storageReference,
        string fileChecksum,
        string fileName,
        TimeSpan duration,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);

    Task RecordFailedAsync(Guid exportId, Guid historyId, TimeSpan duration, DateTime failedAtUtc, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ExpiredAuditExportFile(Guid HistoryId, Guid TenantId, string StorageReference);

public sealed class ScheduledAuditExportRepository(IApplicationDbContext context) : IScheduledAuditExportRepository
{
    public async Task AddAsync(ScheduledAuditExport export, CancellationToken cancellationToken)
    {
        await context.Set<ScheduledAuditExport>().AddAsync(export, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ScheduledAuditExport?> GetByIdAsync(Guid exportId, CancellationToken cancellationToken) =>
        context.Set<ScheduledAuditExport>()
            .FirstOrDefaultAsync(export => export.Id == exportId, cancellationToken);

    public async Task<IReadOnlyList<ScheduledAuditExport>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.Set<ScheduledAuditExport>()
            .AsNoTracking()
            .Where(export => export.TenantId == tenantId)
            .OrderBy(export => export.JobName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ScheduledAuditExport>> GetDueAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken) =>
        await context.Set<ScheduledAuditExport>()
            .AsNoTracking()
            .Where(export => export.IsEnabled && export.NextRunAt.HasValue && export.NextRunAt <= nowUtc)
            .OrderBy(export => export.NextRunAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AuditExportHistory>> GetHistoryAsync(
        Guid exportId,
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken) =>
        await context.Set<AuditExportHistory>()
            .AsNoTracking()
            .Where(history => history.ScheduledExportId == exportId && history.TenantId == tenantId)
            .OrderByDescending(history => history.ExecutedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<AuditExportHistory?> GetHistoryByIdAsync(Guid historyId, Guid tenantId, CancellationToken cancellationToken) =>
        context.Set<AuditExportHistory>()
            .FirstOrDefaultAsync(history => history.Id == historyId && history.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<ExpiredAuditExportFile>> GetExpiredFilesAsync(
        DateTime nowUtc,
        int limit,
        CancellationToken cancellationToken) =>
        await context.Set<AuditExportHistory>()
            .Where(history => history.TenantId.HasValue
                && history.Status == ExportStatus.Completed
                && history.ExportPath != null)
            .Join(
                context.Set<ScheduledAuditExport>(),
                history => history.ScheduledExportId,
                export => export.Id,
                (history, export) => new { History = history, export.RetentionDays })
            .Where(item => item.History.ExecutedAt <= nowUtc.AddDays(-item.RetentionDays))
            .OrderBy(item => item.History.ExecutedAt)
            .Take(limit)
            .Select(item => new ExpiredAuditExportFile(
                item.History.Id,
                item.History.TenantId!.Value,
                item.History.ExportPath!))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task MarkFileExpiredAsync(Guid historyId, CancellationToken cancellationToken)
    {
        var history = await context.Set<AuditExportHistory>()
            .FirstAsync(item => item.Id == historyId, cancellationToken)
            .ConfigureAwait(false);
        history.ExpireFile();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RecoverStaleClaimsAsync(
        DateTime nowUtc,
        TimeSpan staleClaimThreshold,
        CancellationToken cancellationToken)
    {
        var staleBeforeUtc = nowUtc - staleClaimThreshold;
        var staleHistories = await context.Set<AuditExportHistory>()
            .Where(history => history.Status == ExportStatus.InProgress && history.ExecutedAt < staleBeforeUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (staleHistories.Count == 0) { return 0; }

        var exportIds = staleHistories.Select(history => history.ScheduledExportId).Distinct().ToList();
        var exports = await context.Set<ScheduledAuditExport>()
            .Where(export => exportIds.Contains(export.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var busyExportIds = (await context.Set<AuditExportHistory>()
                .Where(history => exportIds.Contains(history.ScheduledExportId)
                    && history.Status == ExportStatus.InProgress
                    && history.ExecutedAt >= staleBeforeUtc)
                .Select(history => history.ScheduledExportId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();

        foreach (var history in staleHistories)
        {
            history.Fail("Stale claim recovered", nowUtc - history.ExecutedAt);
        }

        foreach (var export in exports)
        {
            export.RecordFailure(nowUtc, "Stale claim recovered");
            if (!busyExportIds.Contains(export.Id))
            {
                export.UpdateNextRunTime(nowUtc);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return staleHistories.Count;
    }

    public async Task<bool> TryClaimAsync(
        Guid exportId,
        int expectedVersion,
        DateTime nowUtc,
        DateTime nextRunUtc,
        AuditExportHistory history,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var claimed = await context.Set<ScheduledAuditExport>()
            .Where(export => export.Id == exportId
                && export.IsEnabled
                && export.Version == expectedVersion
                && export.NextRunAt.HasValue
                && export.NextRunAt <= nowUtc)
            .ExecuteUpdateAsync(update => update
                .SetProperty(export => export.NextRunAt, nextRunUtc)
                .SetProperty(export => export.UpdatedAt, nowUtc)
                .SetProperty(export => export.Version, export => export.Version + 1), cancellationToken)
            .ConfigureAwait(false);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await context.Set<AuditExportHistory>().AddAsync(history, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task RecordCompletedAsync(
        Guid exportId,
        Guid historyId,
        int recordCount,
        long fileSizeBytes,
        string storageReference,
        string fileChecksum,
        string fileName,
        TimeSpan duration,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        var export = await context.Set<ScheduledAuditExport>().FirstAsync(item => item.Id == exportId, cancellationToken).ConfigureAwait(false);
        var history = await context.Set<AuditExportHistory>().FirstAsync(item => item.Id == historyId, cancellationToken).ConfigureAwait(false);
        history.Complete(recordCount, fileSizeBytes, storageReference, fileChecksum, duration, fileName);
        export.RecordSuccess(completedAtUtc);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailedAsync(
        Guid exportId,
        Guid historyId,
        TimeSpan duration,
        DateTime failedAtUtc,
        CancellationToken cancellationToken)
    {
        var export = await context.Set<ScheduledAuditExport>().FirstAsync(item => item.Id == exportId, cancellationToken).ConfigureAwait(false);
        var history = await context.Set<AuditExportHistory>().FirstAsync(item => item.Id == historyId, cancellationToken).ConfigureAwait(false);
        history.Fail("Scheduled export failed. Check the service logs for details.", duration);
        export.RecordFailure(failedAtUtc, "Scheduled export failed. Check the service logs for details.");
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
