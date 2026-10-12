namespace GameGuild.Commerce.Payments;

/// <summary>
///     Persistence for revenue reconciliation runs and their discrepancies (issue #404).
/// </summary>
public interface IRevenueReconciliationRepository
{
    /// <summary>Add a new reconciliation run.</summary>
    Task AddRunAsync(RevenueReconciliationRun run, CancellationToken cancellationToken = default);

    /// <summary>Update an existing reconciliation run.</summary>
    Task UpdateRunAsync(RevenueReconciliationRun run, CancellationToken cancellationToken = default);

    /// <summary>Get a reconciliation run by id.</summary>
    Task<RevenueReconciliationRun?> GetRunByIdAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Get reconciliation runs, newest first, optionally tenant-scoped, paged.</summary>
    Task<PagedResult<RevenueReconciliationRun>> GetRunsAsync(
        Guid? tenantId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Get reconciliation runs overlapping an inclusive period, optionally tenant-scoped.</summary>
    Task<List<RevenueReconciliationRun>> GetRunsOverlappingPeriodAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Add a discrepancy to a run.</summary>
    Task AddDiscrepancyAsync(RevenueReconciliationDiscrepancy discrepancy, CancellationToken cancellationToken = default);

    /// <summary>Get discrepancies of a run, paged, optionally filtered by kind.</summary>
    Task<PagedResult<RevenueReconciliationDiscrepancy>> GetDiscrepanciesAsync(
        Guid runId,
        RevenueDiscrepancyKind? kind,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Save changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Persistence for revenue anomaly alerts (issue #404).
/// </summary>
public interface IRevenueAnomalyAlertRepository
{
    /// <summary>Add a new anomaly alert.</summary>
    Task AddAsync(RevenueAnomalyAlert alert, CancellationToken cancellationToken = default);

    /// <summary>Update an existing anomaly alert.</summary>
    Task UpdateAsync(RevenueAnomalyAlert alert, CancellationToken cancellationToken = default);

    /// <summary>Get an anomaly alert by id.</summary>
    Task<RevenueAnomalyAlert?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>Get anomaly alerts, newest detection first, paged with optional filters.</summary>
    Task<PagedResult<RevenueAnomalyAlert>> GetAlertsAsync(
        Guid? tenantId,
        RevenueAnomalyStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Check whether an alert already exists for a (kind, day, currency) pair, in any state.</summary>
    Task<bool> ExistsForDayAsync(
        RevenueAnomalyKind kind,
        DateTime detectedForDateUtc,
        string currency,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Save changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
