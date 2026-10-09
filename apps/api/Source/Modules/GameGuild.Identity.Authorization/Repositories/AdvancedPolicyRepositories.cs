using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     EF Core repository for ABAC Policies
/// </summary>
public class AbacPolicyRepository(DbContext context) : IAbacPolicyRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<AbacPolicy> DbSet => _context.Set<AbacPolicy>();

    public async Task<AbacPolicy> CreateAsync(
        AbacPolicy policy,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(policy, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return policy;
    }

    public async Task<AbacPolicy?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        AbacPolicy policy,
        CancellationToken cancellationToken = default
    )
    {
        policy.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(policy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var policy = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (policy != null)
        {
            DbSet.Remove(policy);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<AbacPolicy>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(p => p.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(p => p.TenantId == null);

        return await query.OrderBy(p => p.Priority).ToListAsync(cancellationToken);
    }

    public async Task<List<AbacPolicy>> GetActivePoliciesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(p => p.IsEnabled);

        if (tenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == new TenantId(tenantId.Value));
        }

        return await query.OrderBy(p => p.Priority).ToListAsync(cancellationToken);
    }
}

/// <summary>
///     EF Core repository for Conditional Policies
/// </summary>
public class ConditionalPolicyRepository(DbContext context) : IConditionalPolicyRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<ConditionalPolicy> DbSet => _context.Set<ConditionalPolicy>();

    public async Task<ConditionalPolicy> CreateAsync(
        ConditionalPolicy policy,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(policy, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return policy;
    }

    public async Task<ConditionalPolicy?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        ConditionalPolicy policy,
        CancellationToken cancellationToken = default
    )
    {
        policy.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(policy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var policy = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (policy != null)
        {
            DbSet.Remove(policy);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<ConditionalPolicy>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(p => p.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(p => p.TenantId == null);

        return await query.OrderBy(p => p.Priority).ToListAsync(cancellationToken);
    }

    public async Task<List<ConditionalPolicy>> GetActivePoliciesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(p => p.IsEnabled);

        if (tenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == new TenantId(tenantId.Value));
        }

        return await query.OrderBy(p => p.Priority).ToListAsync(cancellationToken);
    }
}

/// <summary>
///     EF Core repository for Data Masking Rules
/// </summary>
public class DataMaskingRuleRepository(DbContext context) : IDataMaskingRuleRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<DataMaskingRule> DbSet => _context.Set<DataMaskingRule>();

    public async Task<DataMaskingRule> CreateAsync(
        DataMaskingRule rule,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule;
    }

    public async Task<DataMaskingRule?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        DataMaskingRule rule,
        CancellationToken cancellationToken = default
    )
    {
        rule.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(rule);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule != null)
        {
            DbSet.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<DataMaskingRule>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(r => r.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(r => r.TenantId == null);

        return await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<DataMaskingRule>> GetActiveRulesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(r => r.IsEnabled);

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<DataMaskingRule>> GetByResourceTypeAsync(
        string resourceType,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(r => r.ResourceType == resourceType && r.IsEnabled);

        query = tenantId.HasValue
            ? query.Where(r => r.TenantId == new TenantId(tenantId.Value))
            : query.Where(r => r.TenantId == null);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for Policy Bundles
/// </summary>
public class PolicyBundleRepository(DbContext context) : IPolicyBundleRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<PolicyBundle> DbSet => _context.Set<PolicyBundle>();

    public async Task<PolicyBundle> CreateAsync(
        PolicyBundle bundle,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(bundle, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return bundle;
    }

    public async Task<PolicyBundle?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        PolicyBundle bundle,
        CancellationToken cancellationToken = default
    )
    {
        bundle.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(bundle);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bundle = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle != null)
        {
            DbSet.Remove(bundle);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<PolicyBundle>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(b => b.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(b => b.TenantId == null);

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<PolicyBundle?> GetPublishedByNameAsync(
        string name,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(b => b.Name == name && b.Status == PolicyBundleStatus.Active);

        if (tenantId.HasValue)
        {
            query = query.Where(b => b.TenantId == new TenantId(tenantId.Value));
        }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for Policy Bundle Deployments
/// </summary>
public class PolicyBundleDeploymentRepository(DbContext context) : IPolicyBundleDeploymentRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<PolicyBundleDeployment> DbSet => _context.Set<PolicyBundleDeployment>();

    public async Task<PolicyBundleDeployment> CreateAsync(
        PolicyBundleDeployment deployment,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(deployment, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return deployment;
    }

    public async Task<PolicyBundleDeployment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        PolicyBundleDeployment deployment,
        CancellationToken cancellationToken = default
    )
    {
        DbSet.Update(deployment);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PolicyBundleDeployment>> GetByBundleAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(d => d.BundleId == bundleId)
        .OrderByDescending(d => d.DeployedAt)
        .ToListAsync(cancellationToken);

    public async Task<List<PolicyBundleDeployment>> GetByEnvironmentAsync(
        string environment,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(d => d.Environment == environment)
        .OrderByDescending(d => d.DeployedAt)
        .ToListAsync(cancellationToken);
}

/// <summary>
///     EF Core repository for Permission Template Versions
/// </summary>
public class PermissionTemplateVersionRepository(DbContext context) : IPermissionTemplateVersionRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<PermissionTemplateVersion> DbSet => _context.Set<PermissionTemplateVersion>();

    public async Task<PermissionTemplateVersion> CreateAsync(
        PermissionTemplateVersion version,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(version, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return version;
    }

    public async Task<PermissionTemplateVersion?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        PermissionTemplateVersion version,
        CancellationToken cancellationToken = default
    )
    {
        DbSet.Update(version);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionTemplateVersion>> GetByTemplateIdAsync(
        Guid templateId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(v => v.TemplateId == templateId)
        .OrderByDescending(v => v.VersionNumber)
        .ToListAsync(cancellationToken);

    public async Task<PermissionTemplateVersion?> GetLatestByTemplateIdAsync(
        Guid templateId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(v => v.TemplateId == templateId)
        .OrderByDescending(v => v.VersionNumber)
        .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
///     EF Core repository for Permission Template Migrations
/// </summary>
public class PermissionTemplateMigrationRepository(DbContext context) : IPermissionTemplateMigrationRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<PermissionTemplateMigration> DbSet => _context.Set<PermissionTemplateMigration>();

    public async Task<PermissionTemplateMigration> CreateAsync(
        PermissionTemplateMigration migration,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(migration, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return migration;
    }

    public async Task<PermissionTemplateMigration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        PermissionTemplateMigration migration,
        CancellationToken cancellationToken = default
    )
    {
        DbSet.Update(migration);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionTemplateMigration>> GetByTemplateIdAsync(
        Guid templateId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(m => m.TemplateId == templateId)
        .OrderByDescending(m => m.StartedAt)
        .ToListAsync(cancellationToken);
}

/// <summary>
///     EF Core repository for Policy Registry Audit Logs
/// </summary>
public class PolicyRegistryAuditLogRepository(DbContext context) : IPolicyRegistryAuditLogRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<PolicyRegistryAuditLog> DbSet => _context.Set<PolicyRegistryAuditLog>();

    public async Task<PolicyRegistryAuditLog> CreateAsync(
        PolicyRegistryAuditLog log,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return log;
    }

    public async Task<PolicyRegistryAuditLog?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task<List<PolicyRegistryAuditLog>> GetByBundleIdAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(l => l.BundleId == bundleId)
        .OrderByDescending(l => l.PerformedAt)
        .ToListAsync(cancellationToken);

    public async Task<List<PolicyRegistryAuditLog>> GetByActorAsync(
        Guid actorId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(l => l.PerformedBy == actorId);

        if (from.HasValue)
        {
            query = query.Where(l => l.PerformedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.PerformedAt <= to.Value);
        }

        return await query.OrderByDescending(l => l.PerformedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<PolicyRegistryAuditLog>> GetByActionAsync(
        PolicyRegistryAction action,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(l => l.Action == action);

        if (from.HasValue)
        {
            query = query.Where(l => l.PerformedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.PerformedAt <= to.Value);
        }

        return await query.OrderByDescending(l => l.PerformedAt).ToListAsync(cancellationToken);
    }
}
