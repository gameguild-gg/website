using Microsoft.EntityFrameworkCore;

namespace GameGuild.Analytics;

public class AnalyticsEventRepository(IApplicationDbContext context) : IAnalyticsEventRepository
{
    public async Task<AnalyticsEvent> AddAsync(AnalyticsEvent analyticsEvent, CancellationToken ct = default)
    {
        var entry = await context.Set<AnalyticsEvent>().AddAsync(analyticsEvent, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return entry.Entity;
    }

    public async Task AddRangeAsync(IEnumerable<AnalyticsEvent> events, CancellationToken ct = default)
    {
        await context.Set<AnalyticsEvent>().AddRangeAsync(events, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<AnalyticsEvent>> GetByEventNameAsync(string eventName, DateTime? startDate, DateTime? endDate, Guid? tenantId, CancellationToken ct = default)
    {
        var query = context.Set<AnalyticsEvent>().Where(e => e.EventName == eventName && e.DeletedAt == null);
        if (startDate.HasValue)
        {
            query = query.Where(e => e.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.Timestamp <= endDate.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(e => e.TenantId == tenantId.Value);
        }

        return await query.OrderByDescending(e => e.Timestamp).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<AnalyticsEvent>> GetByUserIdAsync(Guid userId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
    {
        var query = context.Set<AnalyticsEvent>().Where(e => e.UserId == userId && e.DeletedAt == null);
        if (startDate.HasValue)
        {
            query = query.Where(e => e.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.Timestamp <= endDate.Value);
        }

        return await query.OrderByDescending(e => e.Timestamp).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(string eventName, DateTime? startDate, DateTime? endDate, Guid? tenantId, CancellationToken ct = default)
    {
        var query = context.Set<AnalyticsEvent>().Where(e => e.EventName == eventName && e.DeletedAt == null);
        if (startDate.HasValue)
        {
            query = query.Where(e => e.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.Timestamp <= endDate.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(e => e.TenantId == tenantId.Value);
        }

        return await query.CountAsync(ct).ConfigureAwait(false);
    }
}

public class KpiDefinitionRepository(IApplicationDbContext context) : IKpiDefinitionRepository
{
    public async Task<KpiDefinition?> GetByNameAsync(string name, CancellationToken ct = default)
        => await context.Set<KpiDefinition>().FirstOrDefaultAsync(k => k.Name == name && k.DeletedAt == null, ct).ConfigureAwait(false);

    public async Task<List<KpiDefinition>> GetAllActiveAsync(CancellationToken ct = default)
        => await context.Set<KpiDefinition>().Where(k => k.IsActive && k.DeletedAt == null).ToListAsync(ct).ConfigureAwait(false);

    public async Task<KpiDefinition> AddAsync(KpiDefinition kpi, CancellationToken ct = default)
    {
        var entry = await context.Set<KpiDefinition>().AddAsync(kpi, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return entry.Entity;
    }

    public async Task UpdateAsync(KpiDefinition kpi, CancellationToken ct = default)
    {
        kpi.Touch();
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

public class DashboardRepository(IApplicationDbContext context) : IDashboardRepository
{
    public async Task<Dashboard?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<Dashboard>().Include(d => d.Widgets.OrderBy(w => w.SortOrder))
            .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct).ConfigureAwait(false);

    public async Task<Dashboard?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await context.Set<Dashboard>().Include(d => d.Widgets.OrderBy(w => w.SortOrder))
            .FirstOrDefaultAsync(d => d.Slug == slug && d.DeletedAt == null, ct).ConfigureAwait(false);

    public async Task<List<Dashboard>> GetAllAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var query = context.Set<Dashboard>().Include(d => d.Widgets.OrderBy(w => w.SortOrder))
            .Where(d => d.DeletedAt == null);
        if (tenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
        }

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Dashboard> AddAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        var entry = await context.Set<Dashboard>().AddAsync(dashboard, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return entry.Entity;
    }

    public async Task UpdateAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        dashboard.Touch();
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
