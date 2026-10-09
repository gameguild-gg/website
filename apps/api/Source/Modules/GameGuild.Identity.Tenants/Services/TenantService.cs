using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Implementation of tenant management operations.
///     Provides business logic layer over the tenant repository.
/// </summary>
public class TenantService(
    ITenantRepository tenantRepository,
    ILogger<TenantService> logger) : ITenantService
{
    public async Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await tenantRepository.GetActiveTenantsAsync(cancellationToken).ConfigureAwait(false);
        return tenants.ToList().AsReadOnly();
    }

    public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await tenantRepository.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return await tenantRepository.GetBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Tenant?> GetDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await tenantRepository.GetActiveTenantsAsync(cancellationToken).ConfigureAwait(false);
        return tenants.FirstOrDefault(t => t.IsDefault);
    }

    public async Task<Tenant> CreateTenantAsync(
        string name,
        string slug,
        string? description = null,
        string? adminEmail = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        // Validate slug uniqueness
        if (!await tenantRepository.IsSlugUniqueAsync(slug, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"Tenant slug '{slug}' is already in use.");
        }

        var tenant = new Tenant
        {
            Name = name,
            Slug = slug,
            Description = description,
            AdminEmail = adminEmail,
            IsActive = true
        };

        var created = await tenantRepository.CreateAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Created tenant {TenantName} ({TenantId})", created.Name, created.Id);

        return created;
    }

    public async Task<Tenant> UpdateTenantAsync(
        Guid tenantId,
        string name,
        string? description = null,
        string? adminEmail = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");

        tenant.Update(name, description);
        tenant.AdminEmail = adminEmail;

        var updated = await tenantRepository.UpdateAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Updated tenant {TenantName} ({TenantId})", updated.Name, updated.Id);

        return updated;
    }

    public async Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");

        await tenantRepository.DeleteAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Deleted tenant {TenantName} ({TenantId})", tenant.Name, tenant.Id);
    }

    public async Task<Tenant> ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");

        tenant.Activate();
        var updated = await tenantRepository.UpdateAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Activated tenant {TenantName} ({TenantId})", updated.Name, updated.Id);

        return updated;
    }

    public async Task<Tenant> DeactivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");

        tenant.Deactivate();
        var updated = await tenantRepository.UpdateAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Deactivated tenant {TenantName} ({TenantId})", updated.Name, updated.Id);

        return updated;
    }

    public async Task<Tenant> ArchiveTenantAsync(
        Guid tenantId,
        string reason = "",
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");

        tenant.IsArchived = true;
        tenant.ArchivedAt = SystemClock.UtcNow;
        tenant.Deactivate();

        var updated = await tenantRepository.UpdateAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Archived tenant {TenantName} ({TenantId}). Reason: {Reason}", 
            updated.Name, updated.Id, reason);

        return updated;
    }

    public async Task<Tenant> RestoreTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");

        tenant.IsArchived = false;
        tenant.ArchivedAt = null;
        tenant.Activate();

        var updated = await tenantRepository.UpdateAsync(tenant, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Restored tenant {TenantName} ({TenantId})", updated.Name, updated.Id);

        return updated;
    }

    public async Task<bool> IsSlugAvailableAsync(
        string slug,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return await tenantRepository.IsSlugUniqueAsync(slug, excludeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<Tenant> Tenants, int TotalCount)> GetTenantsPagedAsync(
        int page,
        int pageSize,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var (items, totalCount) = await tenantRepository.GetPagedAsync(
            page,
            pageSize,
            isActive: includeArchived ? null : true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (items.ToList().AsReadOnly(), totalCount);
    }
}
