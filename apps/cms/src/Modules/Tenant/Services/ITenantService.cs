using cms.Modules.Tenant.Models;

namespace cms.Modules.Tenant.Services;

/// <summary>
/// Service interface for managing tenants
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Get all tenants
    /// </summary>
    /// <returns>List of tenants</returns>
    Task<IEnumerable<Models.Tenant>> GetAllTenantsAsync();

    /// <summary>
    /// Get a specific tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>Tenant or null if not found</returns>
    Task<Models.Tenant?> GetTenantByIdAsync(Guid id);

    /// <summary>
    /// Get a tenant by name
    /// </summary>
    /// <param name="name">Tenant name</param>
    /// <returns>Tenant or null if not found</returns>
    Task<Models.Tenant?> GetTenantByNameAsync(string name);

    /// <summary>
    /// Create a new tenant
    /// </summary>
    /// <param name="tenant">Tenant to create</param>
    /// <returns>Created tenant</returns>
    Task<Models.Tenant> CreateTenantAsync(Models.Tenant tenant);

    /// <summary>
    /// Update an existing tenant
    /// </summary>
    /// <param name="tenant">Tenant to update</param>
    /// <returns>Updated tenant</returns>
    Task<Models.Tenant> UpdateTenantAsync(Models.Tenant tenant);

    /// <summary>
    /// Soft delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> SoftDeleteTenantAsync(Guid id);

    /// <summary>
    /// Restore a soft-deleted tenant
    /// </summary>
    /// <param name="id">Tenant ID to restore</param>
    /// <returns>True if restored successfully</returns>
    Task<bool> RestoreTenantAsync(Guid id);

    /// <summary>
    /// Permanently delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> HardDeleteTenantAsync(Guid id);

    /// <summary>
    /// Activate a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>True if activated successfully</returns>
    Task<bool> ActivateTenantAsync(Guid id);

    /// <summary>
    /// Deactivate a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>True if deactivated successfully</returns>
    Task<bool> DeactivateTenantAsync(Guid id);

    /// <summary>
    /// Get soft-deleted tenants
    /// </summary>
    /// <returns>List of soft-deleted tenants</returns>
    Task<IEnumerable<Models.Tenant>> GetDeletedTenantsAsync();

    /// <summary>
    /// Add a user to a tenant
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>Created TenantPermission relationship</returns>
    Task<TenantPermission> AddUserToTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Remove a user from a tenant
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>True if removed successfully</returns>
    Task<bool> RemoveUserFromTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Get users in a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>List of TenantPermission relationships</returns>
    Task<IEnumerable<TenantPermission>> GetUsersInTenantAsync(Guid tenantId);

    /// <summary>
    /// Get tenants for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of TenantPermission relationships</returns>
    Task<IEnumerable<TenantPermission>> GetTenantsForUserAsync(Guid userId);
}
