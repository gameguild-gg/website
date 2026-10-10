using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for ResourceUsageTrend entity
/// </summary>
public class ResourceUsageTrendRepository(IApplicationDbContext context) : IResourceUsageTrendRepository
{
    private DbSet<ResourceUsageTrend> ResourceUsageTrends { get => context.Set<ResourceUsageTrend>(); }

    public async Task<ResourceUsageTrend?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await ResourceUsageTrends.FirstOrDefaultAsync(t => t.Id == id, cancellationToken); }

    public async Task<IEnumerable<ResourceUsageTrend>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ResourceUsageTrends.Where(t => t.TenantId!.Value == tenantId).OrderByDescending(t => t.PeriodEnd).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceUsageTrend>> GetByTenantAsync(Guid tenantId, ResourceUsageType? type, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = ResourceUsageTrends.Where(t => t.TenantId!.Value == tenantId);

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.PeriodStart >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.PeriodEnd <= toDate.Value);
        }

        return await query.OrderByDescending(t => t.PeriodEnd).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceUsageTrend>> GetByTenantAndTypeAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        return await ResourceUsageTrends.Where(t => t.TenantId!.Value == tenantId && t.Type == type).OrderByDescending(t => t.PeriodEnd).ToListAsync(cancellationToken);
    }

    public async Task<ResourceUsageTrend> CreateAsync(ResourceUsageTrend trend, CancellationToken cancellationToken = default)
    {
        ResourceUsageTrends.Add(trend);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return trend;
    }

    public async Task<ResourceUsageTrend> AddAsync(ResourceUsageTrend trend, CancellationToken cancellationToken = default) { return await CreateAsync(trend, cancellationToken).ConfigureAwait(false); }

    public async Task<ResourceUsageTrend> UpdateAsync(ResourceUsageTrend trend, CancellationToken cancellationToken = default)
    {
        ResourceUsageTrends.Update(trend);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return trend;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trend = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (trend == null)
        {
            return false;
        }

        ResourceUsageTrends.Remove(trend);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<ResourceUsageTrend>> GetByDateRangeAsync(Guid tenantId, ResourceUsageType type, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await ResourceUsageTrends.Where(t => t.TenantId!.Value == tenantId && t.Type == type && t.PeriodStart >= fromDate && t.PeriodEnd <= toDate)
            .OrderByDescending(t => t.PeriodEnd)
            .ToListAsync(cancellationToken);
    }

    public async Task<ResourceUsageTrend?> GetLatestTrendAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        return await ResourceUsageTrends.Where(t => t.TenantId!.Value == tenantId && t.Type == type).OrderByDescending(t => t.PeriodEnd).FirstOrDefaultAsync(cancellationToken);
    }
}
