using Microsoft.EntityFrameworkCore;
using GameGuild.Common.Services;
using GameGuild.Data;
using GameGuild.Modules.Tenant.Models;

namespace GameGuild.Modules.Tenant.Services;

/// <summary>
/// Service implementation for managing tenants
/// </summary>
public class TenantService : ITenantService
{
    private readonly ApplicationDbContext _context;
    private readonly IPermissionService _permissionService;

    public TenantService(ApplicationDbContext context, IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Get all tenants
    /// </summary>
    /// <returns>List of tenants</returns>
    public async Task<IEnumerable<Models.Tenant>> GetAllTenantsAsync()
    {
        return await _context.Tenants
            .Include(t => t.TenantPermissions)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>Tenant or null if not found</returns>
    public async Task<Models.Tenant?> GetTenantByIdAsync(Guid id)
    {
        return await _context.Tenants
            .Include(t => t.TenantPermissions)
            .ThenInclude(tp => tp.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Get a tenant by name
    /// </summary>
    /// <param name="name">Tenant name</param>
    /// <returns>Tenant or null if not found</returns>
    public async Task<Models.Tenant?> GetTenantByNameAsync(string name)
    {
        return await _context.Tenants
            .Include(t => t.TenantPermissions)
            .FirstOrDefaultAsync(t => t.Name == name);
    }

    /// <summary>
    /// Create a new tenant
    /// </summary>
    /// <param name="tenant">Tenant to create</param>
    /// <returns>Created tenant</returns>
    public async Task<Models.Tenant> CreateTenantAsync(Models.Tenant tenant)
    {
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        return tenant;
    }

    /// <summary>
    /// Update an existing tenant
    /// </summary>
    /// <param name="tenant">Tenant to update</param>
    /// <returns>Updated tenant</returns>
    public async Task<Models.Tenant> UpdateTenantAsync(Models.Tenant tenant)
    {
        Models.Tenant? existingTenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        if (existingTenant == null)
        {
            throw new InvalidOperationException($"Tenant with ID {tenant.Id} not found");
        }

        // Update properties
        existingTenant.Name = tenant.Name;
        existingTenant.Description = tenant.Description;
        existingTenant.IsActive = tenant.IsActive;
        existingTenant.Touch(); // Update timestamp

        await _context.SaveChangesAsync();

        return existingTenant;
    }

    /// <summary>
    /// Soft delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    public async Task<bool> SoftDeleteTenantAsync(Guid id)
    {
        Models.Tenant? tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return false;
        }

        tenant.SoftDelete();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Restore a soft-deleted tenant
    /// </summary>
    /// <param name="id">Tenant ID to restore</param>
    /// <returns>True if restored successfully</returns>
    public async Task<bool> RestoreTenantAsync(Guid id)
    {
        Models.Tenant? tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt != null);

        if (tenant == null)
        {
            return false;
        }

        tenant.Restore();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Permanently delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    public async Task<bool> HardDeleteTenantAsync(Guid id)
    {
        Models.Tenant? tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return false;
        }

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Activate a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>True if activated successfully</returns>
    public async Task<bool> ActivateTenantAsync(Guid id)
    {
        Models.Tenant? tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return false;
        }

        tenant.Activate();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Deactivate a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>True if deactivated successfully</returns>
    public async Task<bool> DeactivateTenantAsync(Guid id)
    {
        Models.Tenant? tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return false;
        }

        tenant.Deactivate();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Get soft-deleted tenants
    /// </summary>
    /// <returns>List of soft-deleted tenants</returns>
    public async Task<IEnumerable<Models.Tenant>> GetDeletedTenantsAsync()
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt != null)
            .Include(t => t.TenantPermissions)
            .ToListAsync();
    }

    /// <summary>
    /// Add a user to a tenant
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>Created TenantPermission relationship</returns>
    public async Task<TenantPermission> AddUserToTenantAsync(Guid userId, Guid tenantId)
    {
        // Use the permission service to handle tenant membership
        return await _permissionService.JoinTenantAsync(userId, tenantId);
    }

    /// <summary>
    /// Remove a user from a tenant
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>True if removed successfully</returns>
    public async Task<bool> RemoveUserFromTenantAsync(Guid userId, Guid tenantId)
    {
        await _permissionService.LeaveTenantAsync(userId, tenantId);
        return true;
    }

    /// <summary>
    /// Get users in a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>List of TenantPermission relationships</returns>
    public async Task<IEnumerable<TenantPermission>> GetUsersInTenantAsync(Guid tenantId)
    {
        return await _context.TenantPermissions
            .Where(tp => tp.TenantId == tenantId && tp.UserId != null)
            .Include(tp => tp.User)
            .Include(tp => tp.Tenant)
            .ToListAsync();
    }

    /// <summary>
    /// Get tenants for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of TenantPermission relationships</returns>
    public async Task<IEnumerable<TenantPermission>> GetTenantsForUserAsync(Guid userId)
    {
        return await _permissionService.GetUserTenantsAsync(userId);
    }
}
