using cms.Common.Entities;

namespace cms.Common.Services;

/// <summary>
/// Interface for the three-layer permission service
/// Provides permission evaluation across Tenant → ContentType → Resource layers
/// Integrates with existing ResourceBase, UserTenant, and ITenantable architecture
/// </summary>
public interface IPermissionService
{
    // ===== LAYER 1: TENANT-WIDE PERMISSIONS =====
    
    /// <summary>
    /// Assign tenant-wide permissions to a user (or global if tenantId is null)
    /// </summary>
    Task AssignUserToTenantAsync(Guid userId, Guid? tenantId, PermissionType permissions, Guid assignedByUserId);

    /// <summary>
    /// Get user's tenant-wide permissions for a specific tenant
    /// </summary>
    Task<PermissionType> GetUserTenantPermissionsAsync(Guid userId, Guid? tenantId);

    /// <summary>
    /// Get all tenants a user has permissions in
    /// </summary>
    Task<IEnumerable<Modules.Tenant.Models.Tenant>> GetUserTenantsAsync(Guid userId);

    /// <summary>
    /// Get user's global permissions (across all tenants)
    /// </summary>
    Task<IEnumerable<UserTenantPermission>> GetUserGlobalPermissionsAsync(Guid userId);

    // ===== LAYER 2: CONTENT-TYPE-WIDE PERMISSIONS =====
    
    /// <summary>
    /// Assign content-type-wide permissions to a user
    /// When tenantId is null, assigns global permissions; when set, assigns tenant-specific permissions
    /// </summary>
    Task AssignContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permissions, Guid assignedByUserId);

    /// <summary>
    /// Check if user has specific content-type permission (checks both global and tenant-specific)
    /// </summary>
    Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permission);

    /// <summary>
    /// Get user's permissions for a specific content type (combines global and tenant-specific)
    /// </summary>
    Task<PermissionType> GetUserContentTypePermissionsAsync(Guid userId, Guid? tenantId, string contentTypeName);

    /// <summary>
    /// Get all content type permissions for a user (both global and tenant-specific)
    /// </summary>
    Task<IEnumerable<ContentTypePermission>> GetUserContentTypePermissionsAsync(Guid userId);

    // ===== LAYER 3: RESOURCE-SPECIFIC PERMISSIONS =====
    
    /// <summary>
    /// Grant permissions to a user for a specific resource
    /// </summary>
    Task GrantResourcePermissionAsync(Guid userId, Guid resourceId, PermissionType permissions, Guid grantedByUserId);

    /// <summary>
    /// Check if user has specific permission for a resource (evaluates all three layers)
    /// </summary>
    Task<bool> HasPermissionAsync(Guid userId, Guid resourceId, PermissionType permission);

    /// <summary>
    /// Get user's complete permissions for a resource (combines all three layers)
    /// </summary>
    Task<PermissionType> GetUserPermissionsAsync(Guid userId, Guid resourceId);

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
