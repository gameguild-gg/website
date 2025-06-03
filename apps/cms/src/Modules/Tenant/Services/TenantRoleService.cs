using Microsoft.EntityFrameworkCore;
using cms.Data;
using cms.Modules.Tenant.Models;

namespace cms.Modules.Tenant.Services;

/// <summary>
/// Service implementation for managing tenant roles
/// </summary>
public class TenantRoleService : ITenantRoleService
{
    private readonly ApplicationDbContext _context;

    public TenantRoleService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all roles for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>List of tenant roles</returns>
    public async Task<IEnumerable<TenantRole>> GetRolesByTenantIdAsync(Guid tenantId)
    {
        return await _context.TenantRoles
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Tenant)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>Tenant role or null if not found</returns>
    public async Task<TenantRole?> GetRoleByIdAsync(Guid id)
    {
        return await _context.TenantRoles
            .Include(r => r.Tenant)
            .Include(r => r.UserTenantRoles)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// Get a role by tenant ID and name
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="name">Role name</param>
    /// <returns>Tenant role or null if not found</returns>
    public async Task<TenantRole?> GetRoleByTenantIdAndNameAsync(Guid tenantId, string name)
    {
        return await _context.TenantRoles
            .Include(r => r.Tenant)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name);
    }

    /// <summary>
    /// Create a new tenant role
    /// </summary>
    /// <param name="role">Role to create</param>
    /// <returns>Created role</returns>
    public async Task<TenantRole> CreateRoleAsync(TenantRole role)
    {
        _context.TenantRoles.Add(role);
        await _context.SaveChangesAsync();

        // Load the related Tenant for the response
        await _context.Entry(role)
            .Reference(r => r.Tenant)
            .LoadAsync();

        return role;
    }

    /// <summary>
    /// Update an existing tenant role
    /// </summary>
    /// <param name="role">Role to update</param>
    /// <returns>Updated role</returns>
    public async Task<TenantRole> UpdateRoleAsync(TenantRole role)
    {
        TenantRole? existingRole = await _context.TenantRoles
            .FirstOrDefaultAsync(r => r.Id == role.Id);

        if (existingRole == null)
        {
            throw new InvalidOperationException($"Tenant role with ID {role.Id} not found");
        }

        // Update properties
        existingRole.Name = role.Name;
        existingRole.Description = role.Description;
        existingRole.Permissions = role.Permissions;
        existingRole.IsActive = role.IsActive;
        existingRole.Touch(); // Update timestamp

        await _context.SaveChangesAsync();

        // Load the related Tenant for the response
        await _context.Entry(existingRole)
            .Reference(r => r.Tenant)
            .LoadAsync();

        return existingRole;
    }

    /// <summary>
    /// Soft delete a tenant role
    /// </summary>
    /// <param name="id">Role ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    public async Task<bool> SoftDeleteRoleAsync(Guid id)
    {
        TenantRole? role = await _context.TenantRoles
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
        {
            return false;
        }

        role.SoftDelete();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Restore a soft-deleted tenant role
    /// </summary>
    /// <param name="id">Role ID to restore</param>
    /// <returns>True if restored successfully</returns>
    public async Task<bool> RestoreRoleAsync(Guid id)
    {
        TenantRole? role = await _context.TenantRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt != null);

        if (role == null)
        {
            return false;
        }

        role.Restore();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Permanently delete a tenant role
    /// </summary>
    /// <param name="id">Role ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    public async Task<bool> HardDeleteRoleAsync(Guid id)
    {
        TenantRole? role = await _context.TenantRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
        {
            return false;
        }

        _context.TenantRoles.Remove(role);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Assign a role to a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <param name="roleId">Role ID</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <returns>Created UserTenantRole assignment</returns>
    public async Task<UserTenantRole> AssignRoleToUserAsync(Guid userTenantId, Guid roleId, DateTime? expiresAt = null)
    {
        // Check if assignment already exists
        UserTenantRole? existingAssignment = await _context.UserTenantRoles
            .FirstOrDefaultAsync(utr => utr.UserTenantId == userTenantId && utr.TenantRoleId == roleId);

        if (existingAssignment != null)
        {
            // If it exists but is inactive, activate it
            if (!existingAssignment.IsActive)
            {
                existingAssignment.Activate();
                existingAssignment.ExpiresAt = expiresAt;
                await _context.SaveChangesAsync();
            }

            return existingAssignment;
        }

        // Create new assignment
        var userTenantRole = new UserTenantRole(
            new
            {
                UserTenantId = userTenantId, TenantRoleId = roleId, AssignedAt = DateTime.UtcNow, ExpiresAt = expiresAt
            }
        );

        _context.UserTenantRoles.Add(userTenantRole);
        await _context.SaveChangesAsync();

        // Load related entities
        await _context.Entry(userTenantRole)
            .Reference(utr => utr.UserTenant)
            .LoadAsync();
        await _context.Entry(userTenantRole)
            .Reference(utr => utr.TenantRole)
            .LoadAsync();

        return userTenantRole;
    }

    /// <summary>
    /// Remove a role from a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <param name="roleId">Role ID</param>
    /// <returns>True if removed successfully</returns>
    public async Task<bool> RemoveRoleFromUserAsync(Guid userTenantId, Guid roleId)
    {
        UserTenantRole? userTenantRole = await _context.UserTenantRoles
            .FirstOrDefaultAsync(utr => utr.UserTenantId == userTenantId && utr.TenantRoleId == roleId);

        if (userTenantRole == null)
        {
            return false;
        }

        userTenantRole.SoftDelete();
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Get roles for a user in a tenant
    /// </summary>
    /// <param name="userTenantId">UserTenant ID</param>
    /// <returns>List of UserTenantRole assignments</returns>
    public async Task<IEnumerable<UserTenantRole>> GetUserRolesInTenantAsync(Guid userTenantId)
    {
        return await _context.UserTenantRoles
            .Where(utr => utr.UserTenantId == userTenantId)
            .Include(utr => utr.UserTenant)
            .ThenInclude(ut => ut.User)
            .Include(utr => utr.TenantRole)
            .ThenInclude(tr => tr.Tenant)
            .ToListAsync();
    }

    /// <summary>
    /// Get users with a specific role in a tenant
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>List of UserTenantRole assignments</returns>
    public async Task<IEnumerable<UserTenantRole>> GetUsersWithRoleAsync(Guid roleId)
    {
        return await _context.UserTenantRoles
            .Where(utr => utr.TenantRoleId == roleId)
            .Include(utr => utr.UserTenant)
            .ThenInclude(ut => ut.User)
            .Include(utr => utr.TenantRole)
            .ThenInclude(tr => tr.Tenant)
            .ToListAsync();
    }
}
