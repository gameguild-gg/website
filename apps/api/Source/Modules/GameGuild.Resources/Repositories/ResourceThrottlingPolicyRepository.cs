using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for ResourceThrottlingPolicy entity
/// </summary>
public class ResourceThrottlingPolicyRepository(IApplicationDbContext context) : IResourceThrottlingPolicyRepository
{
    private DbSet<ResourceThrottlingPolicy> ResourceThrottlingPolicies { get => context.Set<ResourceThrottlingPolicy>(); }

    public async Task<ResourceThrottlingPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await ResourceThrottlingPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken); }

    public async Task<IEnumerable<ResourceThrottlingPolicy>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ResourceThrottlingPolicies.Where(p => p.TenantId!.Value == tenantId).OrderBy(p => p.ResourceType).ToListAsync(cancellationToken);
    }

    public async Task<ResourceThrottlingPolicy?> GetByTenantAndTypeAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        return await ResourceThrottlingPolicies.FirstOrDefaultAsync(p => p.TenantId!.Value == tenantId && p.ResourceType == type, cancellationToken);
    }

    public async Task<ResourceThrottlingPolicy> CreateAsync(ResourceThrottlingPolicy policy, CancellationToken cancellationToken = default)
    {
        ResourceThrottlingPolicies.Add(policy);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return policy;
    }

    public async Task<ResourceThrottlingPolicy> UpdateAsync(ResourceThrottlingPolicy policy, CancellationToken cancellationToken = default)
    {
        ResourceThrottlingPolicies.Update(policy);
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

        ResourceThrottlingPolicies.Remove(policy);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<ResourceThrottlingPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await ResourceThrottlingPolicies.Where(p => p.IsActive).OrderBy(p => p.ResourceType).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceThrottlingPolicy>> GetActivePoliciesAsync(ResourceUsageType? type, CancellationToken cancellationToken = default)
    {
        var query = ResourceThrottlingPolicies.Where(p => p.IsActive);

        if (type.HasValue) { query = query.Where(p => p.ResourceType == type.Value); }

        return await query.OrderBy(p => p.ResourceType).ToListAsync(cancellationToken);
    }

    public async Task<ResourceThrottlingPolicy> AddAsync(ResourceThrottlingPolicy policy, CancellationToken cancellationToken = default) { return await CreateAsync(policy, cancellationToken).ConfigureAwait(false); }
}
