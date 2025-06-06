using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using cms.Data;
using cms.Modules.Tenant.Models;
using cms.Modules.User.Models;

namespace cms.Common.Services;

/// <summary>
/// Implementation of the three-layer permission service
/// Integrates with existing ResourceBase, UserTenant, and ITenantable architecture
/// Provides efficient permission evaluation across Tenant → ContentType → Resource layers
/// 
/// Permission Architecture:
/// - Global permissions: ContentTypePermission with TenantId = null
/// - Tenant-wide permissions: ContentTypePermission with ContentTypeName = "*" (all content types)
/// - Content type permissions: ContentTypePermission with specific ContentTypeName
/// - Resource permissions: ResourcePermission
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    private const string TENANT_WIDE_CONTENT_TYPE = "*"; // Special content type name for tenant-wide permissions

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ===== LAYER 1: TENANT-WIDE PERMISSIONS =====

    public async Task AssignUserToTenantAsync(Guid userId, Guid? tenantId, UnifiedPermissionContext permissionContext, Guid assignedByUserId)
    {
        if (tenantId == null)
        {
            // For global tenant-wide permissions, use ContentTypePermission with null TenantId
            await AssignContentTypePermissionAsync(userId, null, TENANT_WIDE_CONTENT_TYPE, permissionContext, assignedByUserId);
            return;
        }

        // Ensure the tenant exists
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            throw new ArgumentException($"Tenant with ID {tenantId} not found.");
        }

        // Check if the user already has a permission for this tenant
        var existingUserTenant = await _context.UserTenants
            .Where(ut => ut.UserId == userId && ut.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (existingUserTenant != null)
        {
            // Update existing user tenant permission
            await AssignContentTypePermissionAsync(userId, tenantId, TENANT_WIDE_CONTENT_TYPE, permissionContext, assignedByUserId);
            return;
        }

        // Create new user tenant and assign permission
        var user = await _context.Users.FindAsync(userId);
        var assignedByUser = await _context.Users.FindAsync(assignedByUserId);

        if (user == null || assignedByUser == null)
        {
            throw new ArgumentException("User or assigned by user not found.");
        }

        var userTenant = new UserTenant
        {
            User = user,
            Tenant = tenant,
            IsActive = true
        };

        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        // After creating the UserTenant record, assign the actual permissions
        await AssignContentTypePermissionAsync(userId, tenantId, TENANT_WIDE_CONTENT_TYPE, permissionContext, assignedByUserId);
    }

    public async Task<UnifiedPermissionContext> GetUserTenantPermissionsAsync(Guid userId, Guid? tenantId)
    {
        // For tenant-wide permissions, look for ContentTypePermission with the special TENANT_WIDE_CONTENT_TYPE
        var permission = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId &&
                   p.TenantId == tenantId &&
                   p.ContentTypeName == TENANT_WIDE_CONTENT_TYPE)
            .FirstOrDefaultAsync();

        return permission?.PermissionContext ?? new UnifiedPermissionContext();
    }

    public async Task<IEnumerable<Tenant>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.UserTenants
            .Where(ut => ut.UserId == userId && ut.IsActive)
            .Select(ut => ut.Tenant)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContentTypePermission>> GetUserGlobalPermissionsAsync(Guid userId)
    {
        return await _context.ContentTypePermissions
            .Where(p => p.UserId == userId && p.TenantId == null)
            .ToListAsync();
    }

    // ===== LAYER 2: CONTENT-TYPE-WIDE PERMISSIONS =====

    public async Task AssignContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, UnifiedPermissionContext permissionContext, Guid assignedByUserId)
    {
        var existingPermission = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId &&
                   p.TenantId == tenantId &&
                   p.ContentTypeName == contentTypeName)
            .FirstOrDefaultAsync();

        if (existingPermission != null)
        {
            // Update existing permission
            existingPermission.PermissionContext = permissionContext;
            existingPermission.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new permission
            var user = await _context.Users.FindAsync(userId);
            var assignedByUser = await _context.Users.FindAsync(assignedByUserId);

            if (user == null || assignedByUser == null)
            {
                throw new ArgumentException("User or assigned by user not found.");
            }

            var newPermission = new ContentTypePermission
            {
                UserId = userId,
                TenantId = tenantId,
                ContentTypeName = contentTypeName,
                PermissionContext = permissionContext,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = assignedByUserId
            };

            _context.ContentTypePermissions.Add(newPermission);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, ContentInteractionPermission permission)
    {
        var userPermissions = await GetUserContentTypePermissionsAsync(userId, tenantId, contentTypeName);
        return userPermissions.InteractionPermissions.HasFlag(permission);
    }

    public async Task<UnifiedPermissionContext> GetUserContentTypePermissionsAsync(Guid userId, Guid? tenantId, string contentTypeName)
    {
        // Get permissions specific to this content type and tenant
        var tenantSpecificPermission = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId &&
                   p.TenantId == tenantId &&
                   p.ContentTypeName == contentTypeName)
            .FirstOrDefaultAsync();

        // Get global permissions for this content type (null tenant)
        var globalContentTypePermission = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId &&
                   p.TenantId == null &&
                   p.ContentTypeName == contentTypeName)
            .FirstOrDefaultAsync();

        var mergedPermissions = new UnifiedPermissionContext();
        
        // Merge all permissions, starting with the most specific
        if (tenantSpecificPermission != null)
        {
            mergedPermissions = tenantSpecificPermission.PermissionContext;
        }
        
        if (globalContentTypePermission != null)
        {
            // Merge global content type permissions
            // In a real implementation, you would implement merging logic here
        }

        return mergedPermissions;
    }

    public async Task<IEnumerable<ContentTypePermission>> GetUserContentTypePermissionsAsync(Guid userId)
    {
        return await _context.ContentTypePermissions
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    // ===== LAYER 3: RESOURCE-SPECIFIC PERMISSIONS =====

    public async Task GrantResourcePermissionAsync(Guid userId, Guid resourceId, UnifiedPermissionContext permissionContext, Guid grantedByUserId)
    {
        var resource = await _context.Resources.FindAsync(resourceId);
        if (resource == null)
        {
            throw new ArgumentException($"Resource with ID {resourceId} not found.");
        }

        var user = await _context.Users.FindAsync(userId);
        var grantedByUser = await _context.Users.FindAsync(grantedByUserId);

        if (user == null || grantedByUser == null)
        {
            throw new ArgumentException("User or granted by user not found.");
        }

        // Check if permission already exists
        var existingPermission = await _context.ResourcePermissions
            .Where(p => p.User.Id == userId && p.Resource.Id == resourceId)
            .FirstOrDefaultAsync();

        if (existingPermission != null)
        {
            // Update existing permission
            existingPermission.PermissionContext = permissionContext;
            existingPermission.GrantedByUser = grantedByUser;
            existingPermission.GrantedAt = DateTime.UtcNow;
        }
        else
        {
            // Grant new permission using the IPermissionable interface
            var permission = resource.GrantPermission(user, permissionContext, grantedByUser);
            _context.ResourcePermissions.Add(permission);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Guid resourceId, ContentInteractionPermission permission)
    {
        var userPermissions = await GetUserPermissionsAsync(userId, resourceId);
        return userPermissions.HasPermission(permission);
    }

    public async Task<UnifiedPermissionContext> GetUserPermissionsAsync(Guid userId, Guid resourceId)
    {
        var resourceInfo = await GetResourceInfoAsync(resourceId);
        if (resourceInfo == null)
        {
            return new UnifiedPermissionContext(); // Default to no permissions
        }

        var mergedPermissions = new UnifiedPermissionContext();

        // Get resource-specific permissions
        var resourcePermissions = await _context.ResourcePermissions
            .Where(p => p.User.Id == userId && p.Resource.Id == resourceId && p.IsValid)
            .Select(p => p.PermissionContext)
            .ToListAsync();

        // Merge resource-specific permissions
        foreach (var permission in resourcePermissions)
        {
            // In a real implementation, you would implement merging logic here
        }

        return mergedPermissions;
    }

    // ===== HELPER METHODS =====

    public async Task<Guid?> GetResourceTenantIdAsync(Guid resourceId)
    {
        var resource = await _context.Resources
            .Include(r => r.Tenant)
            .FirstOrDefaultAsync(r => r.Id == resourceId);
        
        return resource?.Tenant?.Id;
    }

    private async Task<ResourceInfo> GetResourceInfoAsync(Guid resourceId)
    {
        return await _context.Resources
            .Include(r => r.Tenant)
            .Where(r => r.Id == resourceId)
            .Select(r => new ResourceInfo
            {
                Id = r.Id,
                TenantId = r.Tenant != null ? r.Tenant.Id : null,
                ContentType = r.GetType().Name
            })
            .FirstOrDefaultAsync();
    }

    private class ResourceInfo
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }

    // Additional methods required by the interface

    /// <summary>
    /// Get the content type name for a specific resource
    /// </summary>
    public async Task<string?> GetResourceContentTypeAsync(Guid resourceId)
    {
        var resourceInfo = await GetResourceInfoAsync(resourceId);
        return resourceInfo?.ContentType;
    }

    /// <summary>
    /// Remove a specific permission assignment
    /// </summary>
    public async Task RemovePermissionAsync(Guid permissionId)
    {
        var permission = await _context.ResourcePermissions.FindAsync(permissionId);
        if (permission != null)
        {
            _context.ResourcePermissions.Remove(permission);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Remove all permissions for a user in a specific tenant
    /// </summary>
    public async Task RemoveUserFromTenantAsync(Guid userId, Guid? tenantId)
    {
        // First remove tenant assignments
        var userTenantAssignments = await _context.UserTenants
            .Where(ut => ut.UserId == userId && ut.TenantId == tenantId)
            .ToListAsync();

        if (userTenantAssignments.Any())
        {
            _context.UserTenants.RemoveRange(userTenantAssignments);
        }

        // Then remove content type permissions
        var contentTypePermissions = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .ToListAsync();

        if (contentTypePermissions.Any())
        {
            _context.ContentTypePermissions.RemoveRange(contentTypePermissions);
        }

        await _context.SaveChangesAsync();
    }
}
