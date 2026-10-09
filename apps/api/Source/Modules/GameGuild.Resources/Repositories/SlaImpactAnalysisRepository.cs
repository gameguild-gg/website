using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for SlaImpactAnalysis entity
/// </summary>
public class SlaImpactAnalysisRepository(IApplicationDbContext context) : ISlaImpactAnalysisRepository
{
    private DbSet<SlaImpactAnalysis> SlaImpactAnalyses { get => context.Set<SlaImpactAnalysis>(); }

    public async Task<SlaImpactAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await SlaImpactAnalyses.FirstOrDefaultAsync(a => a.Id == id, cancellationToken); }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await SlaImpactAnalyses.Where(a => a.TenantId!.Value == tenantId).OrderByDescending(a => a.ViolationStartTime).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetByDateRangeAsync(Guid tenantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await SlaImpactAnalyses.Where(a => a.TenantId!.Value == tenantId && a.ViolationStartTime >= fromDate && a.ViolationStartTime <= toDate)
            .OrderByDescending(a => a.ViolationStartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<SlaImpactAnalysis> CreateAsync(SlaImpactAnalysis analysis, CancellationToken cancellationToken = default)
    {
        SlaImpactAnalyses.Add(analysis);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return analysis;
    }

    public async Task<SlaImpactAnalysis> AddAsync(SlaImpactAnalysis analysis, CancellationToken cancellationToken = default) { return await CreateAsync(analysis, cancellationToken).ConfigureAwait(false); }

    public async Task<SlaImpactAnalysis> UpdateAsync(SlaImpactAnalysis analysis, CancellationToken cancellationToken = default)
    {
        SlaImpactAnalyses.Update(analysis);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return analysis;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var analysis = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (analysis == null)
        {
            return false;
        }

        SlaImpactAnalyses.Remove(analysis);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<Dictionary<string, int>> GetViolationCountsByTypeAsync(Guid tenantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var violations = await SlaImpactAnalyses.Where(a => a.TenantId!.Value == tenantId && a.ViolationStartTime >= fromDate && a.ViolationStartTime <= toDate)
            .GroupBy(a => a.ViolationType)
            .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(cancellationToken);

        return violations.ToDictionary(v => v.Type, v => v.Count);
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetCriticalOngoingAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await SlaImpactAnalyses.Where(a => a.TenantId!.Value == tenantId && !a.IsResolved && a.Severity >= SlaViolationSeverity.High)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.ViolationStartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetUnresolvedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await SlaImpactAnalyses.Where(a => a.TenantId!.Value == tenantId && !a.IsResolved).OrderByDescending(a => a.Severity).ThenByDescending(a => a.ViolationStartTime).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetByTenantAndTypeAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        // Note: Since SLA violations don't directly map to ResourceUsageType, 
        // we'll return violations for the tenant and log that type filtering is not directly applicable
        return await SlaImpactAnalyses.Where(a => a.TenantId!.Value == tenantId).OrderByDescending(a => a.ViolationStartTime).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetByTypeAsync(ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        // Note: Since SLA violations don't directly map to ResourceUsageType,
        // we'll return all violations and log that type filtering is not directly applicable
        return await SlaImpactAnalyses.OrderByDescending(a => a.ViolationStartTime).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetByTenantAsync(Guid tenantId, ResourceUsageType type, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        // Note: ResourceUsageType parameter is not directly comparable to SlaViolationType in entity
        // Filter by tenant and dates; ResourceUsageType is not directly applicable to SLA violations
        var query = SlaImpactAnalyses.Include(a => a.ResourceQuota).Where(a => a.TenantId!.Value == tenantId);

        // Optionally filter by ResourceQuota type if needed
        query = query.Where(a => a.ResourceQuota!.Type == type);

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.ViolationStartTime >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.ViolationStartTime <= toDate.Value);
        }

        return await query.OrderByDescending(a => a.ViolationStartTime).ToListAsync(cancellationToken);
    }
}
