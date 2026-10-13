using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository for revenue events
/// </summary>
public class RevenueEventRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<RevenueEvent>(context), IRevenueEventRepository
{
    private sealed record GroupRow(int Key, string Currency, int Count, decimal Total);

    public new async Task<RevenueEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await Entities.FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false); }

    public async Task<List<RevenueEvent>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await Entities.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate).OrderByDescending(e => e.Timestamp).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<RevenueEvent>> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        return await Entities.Where(e => e.ReferenceId == referenceId).OrderByDescending(e => e.Timestamp).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(RevenueEvent revenueEvent, CancellationToken cancellationToken = default) { await Entities.AddAsync(revenueEvent, cancellationToken).ConfigureAwait(false); }

    public new async Task UpdateAsync(RevenueEvent revenueEvent, CancellationToken cancellationToken = default)
    {
        Entities.Update(revenueEvent);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) { await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }

    public async Task<List<RevenueEvent>> GetInPeriodAsync(DateTime startUtc, DateTime endUtc, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var query = Query.Where(e => e.Timestamp >= startUtc && e.Timestamp <= endUtc);
        if (tenantId is { } scopedTenantId)
        {
            query = query.Where(e => e.TenantId == scopedTenantId);
        }

        return await query
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.ReferenceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<RevenueEventGroupTotal>> GetGroupedTotalsAsync(DateTime startUtc, DateTime endUtc, Guid? tenantId, RevenueEventTotalGrouping grouping, CancellationToken cancellationToken = default)
    {
        var query = Query.Where(e => e.Timestamp >= startUtc && e.Timestamp <= endUtc);
        if (tenantId is { } scopedTenantId)
        {
            query = query.Where(e => e.TenantId == scopedTenantId);
        }

        List<GroupRow> rows = grouping switch
        {
            RevenueEventTotalGrouping.EventType => await query
                .GroupBy(e => new { e.EventType, e.Currency })
                .Select(g => new GroupRow((int)g.Key.EventType, g.Key.Currency, g.Count(), g.Sum(e => e.Amount)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            RevenueEventTotalGrouping.Source => await query
                .GroupBy(e => new { e.Source, e.Currency })
                .Select(g => new GroupRow((int)g.Key.Source, g.Key.Currency, g.Count(), g.Sum(e => e.Amount)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            RevenueEventTotalGrouping.Status => await query
                .GroupBy(e => new { e.Status, e.Currency })
                .Select(g => new GroupRow((int)g.Key.Status, g.Key.Currency, g.Count(), g.Sum(e => e.Amount)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "Unknown revenue total grouping."),
        };

        return rows
            .Select(row => new RevenueEventGroupTotal(ToKeyName(grouping, row.Key), row.Currency, row.Count, row.Total))
            .OrderBy(total => total.Key, StringComparer.Ordinal)
            .ThenBy(total => total.Currency, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<List<RevenueDailyTotal>> GetDailyTotalsAsync(DateTime startUtc, DateTime endUtc, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var query = Query.Where(e => e.Timestamp >= startUtc && e.Timestamp <= endUtc);
        if (tenantId is { } scopedTenantId)
        {
            query = query.Where(e => e.TenantId == scopedTenantId);
        }

        var rows = await query
            .Select(e => new { e.Timestamp, e.EventType, e.Amount, e.Currency })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => new { row.Timestamp.Date, row.Currency })
            .Select(group => new RevenueDailyTotal(
                group.Key.Date,
                group.Key.Currency,
                group.Sum(row => RevenueAuditingSigns.IsCredit(row.EventType) ? row.Amount : 0m),
                group.Sum(row => RevenueAuditingSigns.IsDebit(row.EventType) ? row.Amount : 0m),
                group.Sum(row =>
                    RevenueAuditingSigns.IsCredit(row.EventType) ? row.Amount
                    : RevenueAuditingSigns.IsDebit(row.EventType) ? -row.Amount
                    : 0m),
                group.Count()))
            .OrderBy(total => total.DateUtc)
            .ThenBy(total => total.Currency, StringComparer.Ordinal)
            .ToList();
    }

    private static string ToKeyName(RevenueEventTotalGrouping grouping, int key) => grouping switch
    {
        RevenueEventTotalGrouping.EventType => ((RevenueEventType)key).ToString(),
        RevenueEventTotalGrouping.Source => ((RevenueSource)key).ToString(),
        RevenueEventTotalGrouping.Status => ((RevenueEventStatus)key).ToString(),
        _ => key.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
