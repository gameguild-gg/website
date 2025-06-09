using cms.Modules.Tenant.Models;
using cms.Modules.Tenant.Services;

namespace cms.Modules.Tenant.GraphQL;

/// <summary>
/// GraphQL queries for Tenant module
/// </summary>
[ExtendObjectType<cms.Modules.User.GraphQL.Query>]
public class TenantQueries
{
    /// <summary>
    /// Get all tenants (non-deleted only)
    /// </summary>
    public async Task<IEnumerable<Models.Tenant>> GetTenants([Service] ITenantService tenantService)
    {
        return await tenantService.GetAllTenantsAsync();
    }

    /// <summary>
    /// Get a tenant by ID
    /// </summary>
    public async Task<Models.Tenant?> GetTenantById([Service] ITenantService tenantService, Guid id)
    {
        return await tenantService.GetTenantByIdAsync(id);
    }

    /// <summary>
    /// Get soft-deleted tenants
    /// </summary>
    public async Task<IEnumerable<Models.Tenant>> GetDeletedTenants([Service] ITenantService tenantService)
    {
        return await tenantService.GetDeletedTenantsAsync();
    }

    /// <summary>
    /// Get users in a tenant
    /// </summary>
    public async Task<IEnumerable<TenantPermission>> GetUsersInTenant([Service] ITenantService tenantService, Guid tenantId)
    {
        return await tenantService.GetUsersInTenantAsync(tenantId);
    }
}
