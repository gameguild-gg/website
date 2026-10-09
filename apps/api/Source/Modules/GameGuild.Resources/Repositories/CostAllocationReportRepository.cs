using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for CostAllocationReport entity
/// </summary>
public class CostAllocationReportRepository(IApplicationDbContext context) : ICostAllocationReportRepository
{
    private DbSet<CostAllocationReport> CostAllocationReports { get => context.Set<CostAllocationReport>(); }

    public async Task<CostAllocationReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await CostAllocationReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken); }

    public async Task<IEnumerable<CostAllocationReport>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await CostAllocationReports.Where(r => r.TenantId!.Value == tenantId).OrderByDescending(r => r.PeriodEnd).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CostAllocationReport>> GetByTenantAsync(Guid tenantId, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default)
    {
        var query = CostAllocationReports.Where(r => r.TenantId!.Value == tenantId);

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.PeriodStart >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.PeriodEnd <= toDate.Value);
        }

        return await query.OrderByDescending(r => r.PeriodEnd).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CostAllocationReport>> GetByDateRangeAsync(Guid tenantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await CostAllocationReports.Where(r => r.TenantId!.Value == tenantId && r.PeriodStart >= fromDate && r.PeriodEnd <= toDate).OrderByDescending(r => r.PeriodEnd).ToListAsync(cancellationToken);
    }

    public async Task<CostAllocationReport> CreateAsync(CostAllocationReport report, CancellationToken cancellationToken = default)
    {
        CostAllocationReports.Add(report);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return report;
    }

    public async Task<CostAllocationReport> AddAsync(CostAllocationReport report, CancellationToken cancellationToken = default) { return await CreateAsync(report, cancellationToken).ConfigureAwait(false); }

    public async Task<CostAllocationReport> UpdateAsync(CostAllocationReport report, CancellationToken cancellationToken = default)
    {
        CostAllocationReports.Update(report);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return report;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (report == null)
        {
            return false;
        }

        CostAllocationReports.Remove(report);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<CostAllocationReport>> GetUnexportedReportsAsync(CancellationToken cancellationToken = default)
    {
        return await CostAllocationReports.Where(r => !r.IsExported).OrderBy(r => r.PeriodEnd).ToListAsync(cancellationToken);
    }
}
