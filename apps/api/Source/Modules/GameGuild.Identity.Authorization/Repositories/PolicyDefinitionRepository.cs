using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Entity Framework implementation of the policy definition repository.
/// </summary>
public class PolicyDefinitionRepository(IApplicationDbContext context) : IPolicyDefinitionRepository
{
    public async Task<PolicyDefinitionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<PolicyDefinitionEntity>()
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyDefinitionEntity?> GetByNameAsync(string policyName, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        // First try tenant-specific policy
        if (tenantId.HasValue)
        {
            var tenantPolicy = await context.Set<PolicyDefinitionEntity>()
                .FirstOrDefaultAsync(
                    p => p.PolicyName == policyName && p.TenantId == tenantId.Value && p.DeletedAt == null && p.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);

            if (tenantPolicy != null)
            {
                return tenantPolicy;
            }
        }

        // Fall back to global policy
        return await context.Set<PolicyDefinitionEntity>()
            .FirstOrDefaultAsync(
                p => p.PolicyName == policyName && p.TenantId == null && p.DeletedAt == null && p.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PolicyDefinitionEntity>> GetByTenantAsync(Guid tenantId, bool includeGlobal = true, CancellationToken cancellationToken = default)
    {
        var query = context.Set<PolicyDefinitionEntity>()
            .Where(p => p.DeletedAt == null && p.IsActive);

        if (includeGlobal)
        {
            query = query.Where(p => p.TenantId == tenantId || p.TenantId == null);
        }
        else
        {
            query = query.Where(p => p.TenantId == tenantId);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PolicyDefinitionEntity>> GetGlobalPoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<PolicyDefinitionEntity>()
            .Where(p => p.TenantId == null && p.DeletedAt == null && p.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PolicyDefinitionEntity>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<PolicyDefinitionEntity>()
            .Where(p => p.DeletedAt == null && p.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(PolicyDefinitionEntity policy, CancellationToken cancellationToken = default)
    {
        await context.Set<PolicyDefinitionEntity>().AddAsync(policy, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(PolicyDefinitionEntity policy, CancellationToken cancellationToken = default)
    {
        policy.Touch();
        policy.PolicyVersion++;
        context.Set<PolicyDefinitionEntity>().Update(policy);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PolicyDefinitionEntity policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.SoftDelete();
        context.Set<PolicyDefinitionEntity>().Update(policy);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
