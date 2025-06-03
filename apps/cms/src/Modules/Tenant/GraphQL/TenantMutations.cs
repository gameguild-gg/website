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
    public async Task<UserTenant> AddUserToTenant(
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

    /// <summary>
    /// Create a new tenant role
    /// </summary>
    public async Task<TenantRole> CreateTenantRole(
        [Service] ITenantRoleService tenantRoleService,
        CreateTenantRoleInput input)
    {
        var role = new TenantRole
        {
            TenantId = input.TenantId,
            Name = input.Name,
            Description = input.Description,
            Permissions = input.Permissions,
            IsActive = input.IsActive
        };

        return await tenantRoleService.CreateRoleAsync(role);
    }

    /// <summary>
    /// Update an existing tenant role
    /// </summary>
    public async Task<TenantRole?> UpdateTenantRole(
        [Service] ITenantRoleService tenantRoleService,
        UpdateTenantRoleInput input)
    {
        var role = new TenantRole
        {
            Id = input.Id,
            Name = input.Name ?? string.Empty,
            Description = input.Description,
            Permissions = input.Permissions,
            IsActive = input.IsActive ?? true
        };

        return await tenantRoleService.UpdateRoleAsync(role);
    }

    /// <summary>
    /// Soft delete a tenant role
    /// </summary>
    public async Task<bool> SoftDeleteTenantRole(
        [Service] ITenantRoleService tenantRoleService,
        Guid id)
    {
        return await tenantRoleService.SoftDeleteRoleAsync(id);
    }

    /// <summary>
    /// Assign a role to a user within a tenant
    /// </summary>
    public async Task<UserTenantRole> AssignUserTenantRole(
        [Service] ITenantService tenantService,
        [Service] ITenantRoleService tenantRoleService,
        AssignUserTenantRoleInput input)
    {
        // Find the UserTenantId for the user/tenant pair
        var userTenants = await tenantService.GetUsersInTenantAsync(input.TenantId);
        UserTenant? userTenant = userTenants.FirstOrDefault(ut => ut.UserId == input.UserId);

        if (userTenant == null)
            throw new Exception("User is not a member of the tenant");

        return await tenantRoleService.AssignRoleToUserAsync(userTenant.Id, input.TenantRoleId, input.ExpiresAt);
    }

    /// <summary>
    /// Remove a role assignment from a user within a tenant
    /// </summary>
    public async Task<bool> RemoveUserTenantRole(
        [Service] ITenantService tenantService,
        [Service] ITenantRoleService tenantRoleService,
        Guid userId,
        Guid tenantId,
        Guid tenantRoleId)
    {
        // Find the UserTenantId for the user/tenant pair
        var userTenants = await tenantService.GetUsersInTenantAsync(tenantId);
        UserTenant? userTenant = userTenants.FirstOrDefault(ut => ut.UserId == userId);

        if (userTenant == null)
            throw new Exception("User is not a member of the tenant");

        return await tenantRoleService.RemoveRoleFromUserAsync(userTenant.Id, tenantRoleId);
    }
}
