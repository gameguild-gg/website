using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for ResourceQuota entity
/// </summary>
public class ResourceQuotaRepository(IApplicationDbContext context) : IResourceQuotaRepository
{
    private DbSet<ResourceQuota> ResourceQuotas { get => context.Set<ResourceQuota>(); }

    public async Task<ResourceQuota?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await ResourceQuotas.FirstOrDefaultAsync(q => q.Id == id, cancellationToken); }

    public async Task<IEnumerable<ResourceQuota>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ResourceQuotas.Where(q => q.TenantId!.Value == tenantId).ToListAsync(cancellationToken);
    }

    public async Task<ResourceQuota?> GetByTenantAndTypeAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        return await ResourceQuotas.FirstOrDefaultAsync(q => q.TenantId!.Value == tenantId && q.Type == type, cancellationToken);
    }

    public async Task<ResourceQuota> CreateAsync(ResourceQuota quota, CancellationToken cancellationToken = default)
    {
        ResourceQuotas.Add(quota);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return quota;
    }

    public async Task<ResourceQuota> UpdateAsync(ResourceQuota quota, CancellationToken cancellationToken = default)
    {
        ResourceQuotas.Update(quota);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return quota;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quota = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return false;
        }

        ResourceQuotas.Remove(quota);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<ResourceQuota>> GetActiveQuotasAsync(CancellationToken cancellationToken = default)
    {
        return await ResourceQuotas.Where(q => q.IsActive).OrderBy(q => q.TenantId!.Value).ThenBy(q => q.Type).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Guid>> GetTenantsExceedingLimitsAsync(CancellationToken cancellationToken = default)
    {
        var quotas = await ResourceQuotas.Where(q => q.IsActive).ToListAsync(cancellationToken);

        // Filter in memory using entity business logic, get unique tenant IDs
        return quotas.Where(q => q.IsHardLimitExceeded()).Select(q => q.TenantId!.Value).Distinct();
    }

    public async Task<IEnumerable<ResourceQuota>> GetQuotasDueForResetAsync(CancellationToken cancellationToken = default)
    {
        var quotas = await ResourceQuotas.Where(q => q.IsActive).ToListAsync(cancellationToken);

        // Use entity business logic to determine which quotas should reset
        return quotas.Where(q => q.ShouldReset());
    }

    public async Task<IEnumerable<ResourceQuota>> GetQuotasExceedingLimitsAsync(ResourceUsageType? type = null, bool softLimitOnly = false, CancellationToken cancellationToken = default)
    {
        var query = ResourceQuotas.Where(q => q.IsActive);

        if (type.HasValue) { query = query.Where(q => q.Type == type.Value); }

        var quotas = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Filter in memory using entity business logic
        return quotas.Where(q => softLimitOnly ? q.IsSoftLimitExceeded() : q.IsHardLimitExceeded());
    }

    public async Task<IEnumerable<ResourceQuota>> GetQuotasExceedingLimitsAsync(CancellationToken cancellationToken = default)
    {
        var quotas = await ResourceQuotas.Where(q => q.IsActive).ToListAsync(cancellationToken);

        // Filter in memory using entity business logic
        return quotas.Where(q => q.IsHardLimitExceeded());
    }

    /// <inheritdoc/>
    public async Task<Dictionary<ResourceUsageType, ResourceQuota>> GetByTenantAndTypesAsync(
        Guid tenantId,
        IEnumerable<ResourceUsageType> types,
        CancellationToken cancellationToken = default)
    {
        var typesList = types.ToList();
        var quotas = await ResourceQuotas
            .Where(q => q.TenantId!.Value == tenantId && typesList.Contains(q.Type))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return quotas.ToDictionary(q => q.Type);
    }

    // User-level quota methods
    public async Task<ResourceQuota?> GetByUserAndTypeAsync(Guid userId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        return await ResourceQuotas.FirstOrDefaultAsync(q => q.UserId == userId && q.Type == type, cancellationToken);
    }

    public async Task<IEnumerable<ResourceQuota>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await ResourceQuotas.Where(q => q.UserId == userId).ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteByUserAndTypeAsync(Guid userId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        var quota = await GetByUserAndTypeAsync(userId, type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return false;
        }

        ResourceQuotas.Remove(quota);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async Task<(bool Success, ResourceQuota? Quota)> TryIncrementUsageAsync(
        Guid tenantId,
        ResourceUsageType type,
        long amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Usage amount must be greater than zero.");
        }

        if (context is not DbContext dbContext || !dbContext.Database.IsRelational())
        {
            return await TryIncrementTrackedAsync(tenantId, type, amount, cancellationToken).ConfigureAwait(false);
        }

        var maximumStartingUsage = long.MaxValue - amount;

        // Two attempts cover the only expected state transition between read and write:
        // another request resetting an expired quota before this request updates it.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var quota = await ResourceQuotas
                .AsNoTracking()
                .SingleOrDefaultAsync(q => q.TenantId!.Value == tenantId && q.Type == type, cancellationToken)
                .ConfigureAwait(false);

            if (quota == null)
            {
                return (true, null);
            }

            if (!quota.IsActive)
            {
                return (true, quota);
            }

            var observedLastReset = quota.LastReset;
            var observedVersion = quota.Version;
            var observedPeriod = quota.Period;
            var observedResetTime = quota.ResetTime;
            var observedResetDayOfWeek = quota.ResetDayOfWeek;
            var observedResetDayOfMonth = quota.ResetDayOfMonth;
            var updatedAt = SystemClock.UtcNow;
            int affectedRows;

            if (quota.ShouldReset())
            {
                if (quota.HardLimit.HasValue && amount > quota.HardLimit.Value)
                {
                    return (false, quota);
                }

                affectedRows = await ResourceQuotas
                    .Where(candidate =>
                        candidate.Id == quota.Id &&
                        candidate.IsActive &&
                        candidate.Version == observedVersion &&
                        candidate.LastReset == observedLastReset &&
                        candidate.Period == observedPeriod &&
                        candidate.ResetTime == observedResetTime &&
                        candidate.ResetDayOfWeek == observedResetDayOfWeek &&
                        candidate.ResetDayOfMonth == observedResetDayOfMonth &&
                        (!candidate.HardLimit.HasValue || candidate.HardLimit.Value >= amount))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(candidate => candidate.CurrentUsage, amount)
                            .SetProperty(candidate => candidate.LastReset, updatedAt)
                            .SetProperty(candidate => candidate.UpdatedAt, updatedAt)
                            .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (quota.CurrentUsage > maximumStartingUsage)
                {
                    throw new OverflowException("Resource quota usage cannot exceed Int64.MaxValue.");
                }

                if (quota.HardLimit.HasValue &&
                    (amount > quota.HardLimit.Value || quota.CurrentUsage > quota.HardLimit.Value - amount))
                {
                    return (false, quota);
                }

                // Do not compare Version here: every successful consumer increments it.
                // PostgreSQL serializes writers and re-evaluates these live limit and
                // reset predicates after waiting, so all remaining capacity is usable.
                affectedRows = await ResourceQuotas
                    .Where(candidate =>
                        candidate.Id == quota.Id &&
                        candidate.IsActive &&
                        candidate.LastReset == observedLastReset &&
                        candidate.Period == observedPeriod &&
                        candidate.ResetTime == observedResetTime &&
                        candidate.ResetDayOfWeek == observedResetDayOfWeek &&
                        candidate.ResetDayOfMonth == observedResetDayOfMonth &&
                        candidate.CurrentUsage <= maximumStartingUsage &&
                        (!candidate.HardLimit.HasValue || candidate.CurrentUsage <= candidate.HardLimit.Value - amount))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(candidate => candidate.CurrentUsage, candidate => candidate.CurrentUsage + amount)
                            .SetProperty(candidate => candidate.UpdatedAt, updatedAt)
                            .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (affectedRows == 1)
            {
                var updatedQuota = await ResourceQuotas
                    .AsNoTracking()
                    .SingleAsync(candidate => candidate.Id == quota.Id, cancellationToken)
                    .ConfigureAwait(false);

                return (true, updatedQuota);
            }
        }

        var latestQuota = await ResourceQuotas
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.TenantId!.Value == tenantId && q.Type == type, cancellationToken)
            .ConfigureAwait(false);

        if (latestQuota == null)
        {
            return (true, null);
        }

        if (!latestQuota.IsActive)
        {
            return (true, latestQuota);
        }

        if (!latestQuota.ShouldReset() && latestQuota.HardLimit.HasValue &&
            (amount > latestQuota.HardLimit.Value || latestQuota.CurrentUsage > latestQuota.HardLimit.Value - amount))
        {
            return (false, latestQuota);
        }

        if (!latestQuota.ShouldReset() &&
            !latestQuota.HardLimit.HasValue &&
            latestQuota.CurrentUsage > maximumStartingUsage)
        {
            throw new OverflowException("Resource quota usage cannot exceed Int64.MaxValue.");
        }

        throw new DbUpdateConcurrencyException(
            $"Resource quota changed repeatedly while consuming usage. Tenant: {tenantId}, Type: {type}.");
    }

    private async Task<(bool Success, ResourceQuota? Quota)> TryIncrementTrackedAsync(
        Guid tenantId,
        ResourceUsageType type,
        long amount,
        CancellationToken cancellationToken)
    {
        var quota = await ResourceQuotas
            .SingleOrDefaultAsync(q => q.TenantId!.Value == tenantId && q.Type == type, cancellationToken)
            .ConfigureAwait(false);

        if (quota == null)
        {
            return (true, null);
        }

        if (!quota.IsActive)
        {
            return (true, quota);
        }

        if (quota.ShouldReset())
        {
            quota.ResetUsage();
        }

        var projectedUsage = checked(quota.CurrentUsage + amount);
        if (quota.HardLimit.HasValue && projectedUsage > quota.HardLimit.Value)
        {
            return (false, quota);
        }

        quota.CurrentUsage = projectedUsage;
        quota.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (true, quota);
    }

    /// <inheritdoc/>
    public async Task<bool> DecrementUsageAsync(
        Guid tenantId,
        ResourceUsageType type,
        long amount,
        CancellationToken cancellationToken = default)
    {
        var quota = await ResourceQuotas
            .FirstOrDefaultAsync(q => q.TenantId!.Value == tenantId && q.Type == type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return false;
        }

        // Use entity method to ensure usage never goes negative
        quota.RemoveUsage(amount);
        quota.Touch();

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
