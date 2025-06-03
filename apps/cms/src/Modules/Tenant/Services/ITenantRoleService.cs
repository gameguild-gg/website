using cms.Modules.Tenant.Models;

namespace cms.Modules.Tenant.Services;

/// <summary>
/// Service interface for managing tenant roles
/// </summary>
public interface ITenantRoleService
{
    /// <summary>
    /// Get all roles for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>List of tenant roles</returns>
    Task<IEnumerable<TenantRole>> GetRolesByTenantIdAsync(Guid tenantId);

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>Tenant role or null if not found</returns>
    Task<TenantRole?> GetRoleByIdAsync(Guid id);

    /// <summary>
    /// Get a role by tenant ID and name
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="name">Role name</param>
    /// <returns>Tenant role or null if not found</returns>
    Task<TenantRole?> GetRoleByTenantIdAndNameAsync(Guid tenantId, string name);

    /// <summary>
    /// Create a new tenant role
    /// </summary>
    /// <param name="role">Role to create</param>
    /// <returns>Created role</returns>
    Task<TenantRole> CreateRoleAsync(TenantRole role);

    /// <summary>
    /// Update an existing tenant role
    /// </summary>
    /// <param name="role">Role to update</param>
    /// <returns>Updated role</returns>
    Task<TenantRole> UpdateRoleAsync(TenantRole role);

    /// <summary>
    /// Soft delete a tenant role
    /// </summary>
    /// <param name="id">Role ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> SoftDeleteRoleAsync(Guid id);

    /// <summary>
    /// Restore a soft-deleted tenant role
    /// </summary>
    /// <param name="id">Role ID to restore</param>
    /// <returns>True if restored successfully</returns>
    Task<bool> RestoreRoleAsync(Guid id);

    /// <summary>
    /// Permanently delete a tenant role
    /// </summary>
    /// <param name="id">Role ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> HardDeleteRoleAsync(Guid id);

    /// <summary>
    /// Assign a role to a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <param name="roleId">Role ID</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <returns>Created UserTenantRole assignment</returns>
    Task<UserTenantRole> AssignRoleToUserAsync(Guid userTenantId, Guid roleId, DateTime? expiresAt = null);

    /// <summary>
    /// Remove a role from a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <param name="roleId">Role ID</param>
    /// <returns>True if removed successfully</returns>
    Task<bool> RemoveRoleFromUserAsync(Guid userTenantId, Guid roleId);

    /// <summary>
    /// Get roles for a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <returns>List of UserTenantRole assignments</returns>
    Task<IEnumerable<UserTenantRole>> GetUserRolesInTenantAsync(Guid userTenantId);

    /// <summary>
    /// Get users with a specific role in a tenant
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>List of UserTenantRole assignments</returns>
    Task<IEnumerable<UserTenantRole>> GetUsersWithRoleAsync(Guid roleId);
}
