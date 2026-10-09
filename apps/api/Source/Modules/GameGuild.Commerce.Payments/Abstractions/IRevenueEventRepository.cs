namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository for revenue events
/// </summary>
public interface IRevenueEventRepository
{
    /// <summary>Get revenue event by ID</summary>
    Task<RevenueEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get revenue events by date range</summary>
    Task<List<RevenueEvent>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Get revenue events by reference ID</summary>
    Task<List<RevenueEvent>> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default);

    /// <summary>Add new revenue event</summary>
    Task AddAsync(RevenueEvent revenueEvent, CancellationToken cancellationToken = default);

    /// <summary>Update revenue event</summary>
    Task UpdateAsync(RevenueEvent revenueEvent, CancellationToken cancellationToken = default);

    /// <summary>Save changes to database</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Get revenue events in an inclusive period, optionally tenant-scoped.</summary>
    Task<List<RevenueEvent>> GetInPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Group revenue event totals in an inclusive period by the requested dimension.</summary>
    Task<List<RevenueEventGroupTotal>> GetGroupedTotalsAsync(
        DateTime startUtc,
        DateTime endUtc,
        Guid? tenantId,
        RevenueEventTotalGrouping grouping,
        CancellationToken cancellationToken = default);

    /// <summary>Get per-day net revenue totals in an inclusive period.</summary>
    Task<List<RevenueDailyTotal>> GetDailyTotalsAsync(
        DateTime startUtc,
        DateTime endUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
