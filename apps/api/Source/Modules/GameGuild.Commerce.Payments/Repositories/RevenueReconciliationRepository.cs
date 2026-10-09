using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     EF Core implementation of <see cref="IRevenueReconciliationRepository" /> and
///     <see cref="IRevenueAnomalyAlertRepository" /> backed by the shared application
///     <see cref="IApplicationDbContext" />.
/// </summary>
public class RevenueReconciliationRepository(IApplicationDbContext context)
    : IRevenueReconciliationRepository, IRevenueAnomalyAlertRepository
{
    // ── IRevenueReconciliationRepository ───────────────────────────────

    /// <inheritdoc />
    public async Task AddRunAsync(RevenueReconciliationRun run, CancellationToken cancellationToken = default)
    {
        await context.Set<RevenueReconciliationRun>().AddAsync(run, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateRunAsync(RevenueReconciliationRun run, CancellationToken cancellationToken = default)
    {
        context.Set<RevenueReconciliationRun>().Update(run);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<RevenueReconciliationRun?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await context.Set<RevenueReconciliationRun>()
            .AsNoTracking()
            .FirstOrDefaultAsync(run => run.Id == runId && run.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<RevenueReconciliationRun>> GetRunsAsync(Guid? tenantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<RevenueReconciliationRun>().AsNoTracking().Where(run => run.DeletedAt == null);
        if (tenantId is { } scopedTenantId)
        {
            query = query.Where(run => run.TenantId == scopedTenantId);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(run => run.StartedAtUtc)
            .ThenBy(run => run.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<RevenueReconciliationRun>(items, totalCount, skip, take);
    }

    /// <inheritdoc />
    public async Task<List<RevenueReconciliationRun>> GetRunsOverlappingPeriodAsync(DateTime periodStartUtc, DateTime periodEndUtc, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var query = context.Set<RevenueReconciliationRun>().AsNoTracking()
            .Where(run => run.DeletedAt == null
                && run.Status == RevenueReconciliationStatus.Completed
                && run.PeriodStartUtc <= periodEndUtc
                && run.PeriodEndUtc >= periodStartUtc);
        if (tenantId is { } scopedTenantId)
        {
            query = query.Where(run => run.TenantId == scopedTenantId);
        }

        return await query
            .OrderByDescending(run => run.CompletedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddDiscrepancyAsync(RevenueReconciliationDiscrepancy discrepancy, CancellationToken cancellationToken = default)
    {
        await context.Set<RevenueReconciliationDiscrepancy>().AddAsync(discrepancy, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<RevenueReconciliationDiscrepancy>> GetDiscrepanciesAsync(Guid runId, RevenueDiscrepancyKind? kind, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<RevenueReconciliationDiscrepancy>().AsNoTracking()
            .Where(discrepancy => discrepancy.RunId == runId && discrepancy.DeletedAt == null);
        if (kind is { } kindFilter)
        {
            query = query.Where(discrepancy => discrepancy.Kind == kindFilter);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(discrepancy => discrepancy.Kind)
            .ThenBy(discrepancy => discrepancy.ExternalReference)
            .ThenBy(discrepancy => discrepancy.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<RevenueReconciliationDiscrepancy>(items, totalCount, skip, take);
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    // ── IRevenueAnomalyAlertRepository ─────────────────────────────────

    /// <inheritdoc />
    public async Task AddAsync(RevenueAnomalyAlert alert, CancellationToken cancellationToken = default)
    {
        await context.Set<RevenueAnomalyAlert>().AddAsync(alert, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateAsync(RevenueAnomalyAlert alert, CancellationToken cancellationToken = default)
    {
        context.Set<RevenueAnomalyAlert>().Update(alert);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<RevenueAnomalyAlert?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await context.Set<RevenueAnomalyAlert>()
            .FirstOrDefaultAsync(alert => alert.Id == alertId && alert.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<RevenueAnomalyAlert>> GetAlertsAsync(Guid? tenantId, RevenueAnomalyStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<RevenueAnomalyAlert>().AsNoTracking().Where(alert => alert.DeletedAt == null);
        if (tenantId is { } scopedTenantId)
        {
            query = query.Where(alert => alert.TenantId == scopedTenantId);
        }

        if (status is { } statusFilter)
        {
            query = query.Where(alert => alert.Status == statusFilter);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(alert => alert.DetectedAtUtc)
            .ThenBy(alert => alert.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<RevenueAnomalyAlert>(items, totalCount, skip, take);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsForDayAsync(RevenueAnomalyKind kind, DateTime detectedForDateUtc, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        // Exact tenant match: a null tenantId is the global aggregate scope and must not
        // be suppressed by (nor suppress) tenant-scoped alerts for the same day.
        var query = context.Set<RevenueAnomalyAlert>().AsNoTracking()
            .Where(alert => alert.DeletedAt == null
                && alert.Kind == kind
                && alert.DetectedForDateUtc == detectedForDateUtc
                && alert.TenantId == tenantId);

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }
}
