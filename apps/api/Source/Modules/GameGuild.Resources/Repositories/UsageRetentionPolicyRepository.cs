using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for UsageRetentionPolicy entity
/// </summary>
public class UsageRetentionPolicyRepository(IApplicationDbContext context) : IUsageRetentionPolicyRepository
{
    private DbSet<UsageRetentionPolicy> UsageRetentionPolicies { get => context.Set<UsageRetentionPolicy>(); }

    public async Task<UsageRetentionPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await UsageRetentionPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken); }

    public async Task<IEnumerable<UsageRetentionPolicy>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await UsageRetentionPolicies.Where(p => p.TenantId!.Value == tenantId).OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public async Task<UsageRetentionPolicy?> GetByTenantAndTypeAsync(Guid? tenantId, ResourceUsageType? type, CancellationToken cancellationToken = default)
    {
        if (!tenantId.HasValue)
        {
            // Global policy
            var globalQuery = UsageRetentionPolicies.Where(p => p.TenantId == null);

            if (type.HasValue) { globalQuery = globalQuery.Where(p => p.ResourceType == type.Value); }
            else { globalQuery = globalQuery.Where(p => p.ResourceType == null); }

            return await globalQuery.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        var query = UsageRetentionPolicies.Where(p => p.TenantId!.Value == tenantId.Value);

        if (type.HasValue) { query = query.Where(p => p.ResourceType == type.Value); }
        else { query = query.Where(p => p.ResourceType == null); }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<UsageRetentionPolicy> CreateAsync(UsageRetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        UsageRetentionPolicies.Add(policy);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return policy;
    }

    public async Task<UsageRetentionPolicy> AddAsync(UsageRetentionPolicy policy, CancellationToken cancellationToken = default) { return await CreateAsync(policy, cancellationToken).ConfigureAwait(false); }

    public async Task<UsageRetentionPolicy> UpdateAsync(UsageRetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        UsageRetentionPolicies.Update(policy);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return policy;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var policy = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (policy == null)
        {
            return false;
        }

        UsageRetentionPolicies.Remove(policy);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<UsageRetentionPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await UsageRetentionPolicies.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UsageRetentionPolicy>> GetPoliciesDueForExecutionAsync(CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        return await UsageRetentionPolicies.Where(p => p.IsActive && p.NextExecutionAt <= now).OrderBy(p => p.NextExecutionAt).ToListAsync(cancellationToken);
    }
}
