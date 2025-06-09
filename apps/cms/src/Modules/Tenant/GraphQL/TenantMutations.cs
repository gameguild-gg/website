using cms.Modules.Tenant.Models;
using cms.Modules.Tenant.Services;

namespace cms.Modules.Tenant.GraphQL;

/// <summary>
/// GraphQL mutations for Tenant module
/// </summary>
[ExtendObjectType<cms.Modules.User.GraphQL.Mutation>]
public class TenantMutations
{
    /// <summary>
    /// Create a new tenant
    /// </summary>
    public async Task<Models.Tenant> CreateTenant(
        [Service] ITenantService tenantService,
        CreateTenantInput input)
    {
        var tenant = new Models.Tenant
        {
            Name = input.Name, Description = input.Description, IsActive = input.IsActive
        };

        return await tenantService.CreateTenantAsync(tenant);
    }

    /// <summary>
    /// Update an existing tenant
    /// </summary>
    public async Task<Models.Tenant?> UpdateTenant(
        [Service] ITenantService tenantService,
        UpdateTenantInput input)
    {
        var tenant = new Models.Tenant
        {
            Id = input.Id, Name = input.Name ?? string.Empty, Description = input.Description, IsActive = input.IsActive ?? true
        };

        return await tenantService.UpdateTenantAsync(tenant);
    }

    /// <summary>
    /// Soft delete a tenant
    /// </summary>
    public async Task<bool> SoftDeleteTenant(
        [Service] ITenantService tenantService,
        Guid id)
    {
        return await tenantService.SoftDeleteTenantAsync(id);
    }

    /// <summary>
    /// Restore a soft-deleted tenant
    /// </summary>
    public async Task<bool> RestoreTenant(
        [Service] ITenantService tenantService,
        Guid id)
    {
        return await tenantService.RestoreTenantAsync(id);
    }

    /// <summary>
    /// Add a user to a tenant
    /// </summary>
    public async Task<TenantPermission> AddUserToTenant(
        [Service] ITenantService tenantService,
        AddUserToTenantInput input)
    {
        return await tenantService.AddUserToTenantAsync(input.UserId, input.TenantId);
    }

    /// <summary>
    /// Remove a user from a tenant
    /// </summary>
    public async Task<bool> RemoveUserFromTenant(
        [Service] ITenantService tenantService,
        RemoveUserFromTenantInput input)
    {
        return await tenantService.RemoveUserFromTenantAsync(input.UserId, input.TenantId);
    }
}
