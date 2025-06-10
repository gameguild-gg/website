using cms.Common.Entities;
using cms.Common.Enums;
using cms.Modules.Tenant.Models;

namespace cms.Common.Services;

/// <summary>
/// Interface for the three-layer permission service
/// Layer 1: Tenant-wide permissions with default support
/// Layer 2: Content-Type permissions (to be implemented)
/// Layer 3: Resource-specific permissions (to be implemented)
/// </summary>
public interface IPermissionService
{
    // ===== LAYER 1: TENANT-WIDE PERMISSIONS =====

    /// <summary>
    /// Grant permissions to a user in a tenant, or set default permissions
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID (null for global defaults)</param>
    /// <param name="permissions">Permissions to grant</param>
    /// <returns>The tenant permission entity</returns>
    Task<TenantPermission> GrantTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions);

    /// <summary>
    /// Check if user has a specific tenant permission
    /// Resolves through hierarchy: user -> tenant default -> global default
    /// </summary>
    Task<bool> HasTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType permission);

    /// <summary>
    /// Get all permissions for a user in a tenant
    /// </summary>
    Task<IEnumerable<PermissionType>> GetTenantPermissionsAsync(Guid? userId, Guid? tenantId);

    /// <summary>
    /// Revoke specific permissions from a user in a tenant
    /// </summary>
    Task RevokeTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions);

    // === DEFAULT PERMISSION MANAGEMENT ===

    /// <summary>
    /// Set default permissions for a tenant
    /// </summary>
    Task<TenantPermission> SetTenantDefaultPermissionsAsync(Guid? tenantId, PermissionType[] permissions);

    /// <summary>
    /// Set global default permissions
    /// </summary>
    Task<TenantPermission> SetGlobalDefaultPermissionsAsync(PermissionType[] permissions);

    /// <summary>
    /// Get tenant default permissions
    /// </summary>
    Task<IEnumerable<PermissionType>> GetTenantDefaultPermissionsAsync(Guid? tenantId);

    /// <summary>
    /// Get global default permissions
    /// </summary>
    Task<IEnumerable<PermissionType>> GetGlobalDefaultPermissionsAsync();

    /// <summary>
    /// Get effective permissions through hierarchy resolution
    /// </summary>
    Task<IEnumerable<PermissionType>> GetEffectiveTenantPermissionsAsync(Guid userId, Guid? tenantId);

    // === USER-TENANT MEMBERSHIP FUNCTIONALITY ===

    /// <summary>
    /// Get all tenants a user is a member of
    /// </summary>
    Task<IEnumerable<TenantPermission>> GetUserTenantsAsync(Guid userId);

    /// <summary>
    /// Add user to a tenant
    /// </summary>
    Task<TenantPermission> JoinTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Remove user from a tenant
    /// </summary>
    Task LeaveTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Check if user is a member of a tenant
    /// </summary>
    Task<bool> IsUserInTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Update user's membership expiration in a tenant
    /// </summary>
    Task<TenantPermission> UpdateTenantMembershipExpirationAsync(Guid userId, Guid tenantId, DateTime? expiresAt);

    // ===== LAYER 2: CONTENT-TYPE-WIDE PERMISSIONS =====

    /// <summary>
    /// Grant content-type permissions to a user in a tenant, or set default permissions
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID (null for global defaults)</param>
    /// <param name="contentTypeName">Name of the content type (e.g., "Article", "Video")</param>
    /// <param name="permissions">Permissions to grant</param>
    Task GrantContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions);

    /// <summary>
    /// Check if user has a specific content-type permission
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="contentTypeName">Name of the content type</param>
    /// <param name="permission">Permission to check</param>
    Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permission);

    /// <summary>
    /// Get all content-type permissions for a user in a tenant
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="contentTypeName">Name of the content type</param>
    Task<IEnumerable<PermissionType>> GetContentTypePermissionsAsync(Guid? userId, Guid? tenantId, string contentTypeName);

    /// <summary>
    /// Revoke specific content-type permissions from a user in a tenant
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="contentTypeName">Name of the content type</param>
    /// <param name="permissions">Permissions to revoke</param>
    Task RevokeContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions);
    
    // ===== LAYER 3: RESOURCE-SPECIFIC PERMISSIONS (Future Implementation) =====
    // TODO: Implement resource permissions in final phase
    
    // ===== HELPER METHODS =====

    /// <summary>
    /// Get the tenant context for a specific resource
    /// </summary>
    Task<Guid?> GetResourceTenantIdAsync(Guid resourceId);

    /// <summary>
    /// Get the content type name for a specific resource
    /// </summary>
    Task<string?> GetResourceContentTypeAsync(Guid resourceId);
}
