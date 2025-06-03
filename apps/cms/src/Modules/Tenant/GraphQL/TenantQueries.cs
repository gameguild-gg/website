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
    /// Get roles for a specific tenant
    /// </summary>
    public async Task<IEnumerable<TenantRole>> GetTenantRolesByTenantId([Service] ITenantRoleService tenantRoleService, Guid tenantId)
    {
        return await tenantRoleService.GetRolesByTenantIdAsync(tenantId);
    }

    /// <summary>
    /// Get tenant role by ID
    /// </summary>
    public async Task<TenantRole?> GetTenantRoleById([Service] ITenantRoleService tenantRoleService, Guid id)
    {
        return await tenantRoleService.GetRoleByIdAsync(id);
    }

    /// <summary>
    /// Get users in a tenant
    /// </summary>
    public async Task<IEnumerable<UserTenant>> GetUsersInTenant([Service] ITenantService tenantService, Guid tenantId)
    {
        return await tenantService.GetUsersInTenantAsync(tenantId);
    }

    /// <summary>
    /// Get user's roles in a tenant
    /// </summary>
    public async Task<IEnumerable<UserTenantRole>> GetUserRolesInTenant([Service] ITenantRoleService tenantRoleService, Guid userTenantId)
    {
        return await tenantRoleService.GetUserRolesInTenantAsync(userTenantId);
    }
}
