using cms.Common.Entities;

namespace cms.Common.Services;

/// <summary>
/// Interface for the three-layer permission service
/// Provides permission evaluation across Tenant → ContentType → Resource layers
/// Integrates with existing ResourceBase, UserTenant, and ITenantable architecture
/// TODO: Update to work with new DAC permission model after role-based system removal
/// </summary>
public interface IPermissionService
{
    // ===== LAYER 1: TENANT-WIDE PERMISSIONS =====

    // TODO: Implement tenant-wide permissions with new DAC model
    // Task AssignUserToTenantAsync(Guid userId, Guid? tenantId, [NewPermissionType] permissionContext, Guid assignedByUserId);

    // TODO: Implement user tenant permissions with new DAC model
    // Task<[NewPermissionType]> GetUserTenantPermissionsAsync(Guid userId, Guid? tenantId);

    /// <summary>
    /// Get all tenants a user has permissions in
    /// </summary>
    Task<IEnumerable<Modules.Tenant.Models.Tenant>> GetUserTenantsAsync(Guid userId);

    /// <summary>
    /// Get user's global permissions (across all tenants)
    /// Returns ContentTypePermission entities with null TenantId for global permissions
    /// </summary>
    Task<IEnumerable<ContentTypePermission>> GetUserGlobalPermissionsAsync(Guid userId);

    // ===== LAYER 2: CONTENT-TYPE-WIDE PERMISSIONS =====

    // TODO: Implement content type permissions with new DAC model
    // Task AssignContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, [NewPermissionType] permissionContext, Guid assignedByUserId);

    // TODO: Implement content type permission checks with new DAC model
    // Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, [NewPermissionEnum] permission);

    // TODO: Implement user content type permissions with new DAC model
    // Task<[NewPermissionType]> GetUserContentTypePermissionsAsync(Guid userId, Guid? tenantId, string contentTypeName);

    /// <summary>
    /// Get all content type permissions for a user (both global and tenant-specific)
    /// </summary>
    Task<IEnumerable<ContentTypePermission>> GetUserContentTypePermissionsAsync(Guid userId);

    // ===== LAYER 3: RESOURCE-SPECIFIC PERMISSIONS =====

    // TODO: Implement resource permissions with new DAC model
    // Task GrantResourcePermissionAsync(Guid userId, Guid resourceId, [NewPermissionType] permissionContext, Guid grantedByUserId);

    // TODO: Implement resource permission checks with new DAC model
    // Task<bool> HasPermissionAsync(Guid userId, Guid resourceId, [NewPermissionEnum] permission);

    // TODO: Implement user resource permissions with new DAC model
    // Task<[NewPermissionType]> GetUserPermissionsAsync(Guid userId, Guid resourceId);

    // ===== HELPER METHODS =====

    /// <summary>
    /// Get the tenant context for a specific resource
    /// </summary>
    Task<Guid?> GetResourceTenantIdAsync(Guid resourceId);

    /// <summary>
    /// Get the content type name for a specific resource
    /// </summary>
    Task<string?> GetResourceContentTypeAsync(Guid resourceId);

    /// <summary>
    /// Remove a specific permission assignment
    /// </summary>
    Task RemovePermissionAsync(Guid permissionId);

    /// <summary>
    /// Remove all permissions for a user in a specific tenant
    /// </summary>
    Task RemoveUserFromTenantAsync(Guid userId, Guid? tenantId);
}
