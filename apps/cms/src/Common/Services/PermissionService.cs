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

    public async Task AssignUserToTenantAsync(Guid userId, Guid? tenantId, PermissionType permissions, Guid assignedByUserId)
    {
        if (tenantId == null)
        {
            // For global tenant-wide permissions, use ContentTypePermission with null TenantId
            await AssignContentTypePermissionAsync(userId, null, TENANT_WIDE_CONTENT_TYPE, permissions, assignedByUserId);

            return;
        }

        // First ensure the UserTenant relationship exists
        UserTenant? userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted);

        if (userTenant == null)
        {
            // Create the UserTenant relationship if it doesn't exist
            userTenant = new Modules.Tenant.Models.UserTenant
            {
                UserId = userId, TenantId = tenantId.Value, IsActive = true, JoinedAt = DateTime.UtcNow
            };
            _context.UserTenants.Add(userTenant);
            await _context.SaveChangesAsync();
        }

        // Use ContentTypePermission with special content type name for tenant-wide permissions
        await AssignContentTypePermissionAsync(userId, tenantId, TENANT_WIDE_CONTENT_TYPE, permissions, assignedByUserId);
    }

    public async Task<PermissionType> GetUserTenantPermissionsAsync(Guid userId, Guid? tenantId)
    {
        // Get tenant-wide permissions using ContentTypePermission with special content type
        return await GetUserContentTypePermissionsAsync(userId, tenantId, TENANT_WIDE_CONTENT_TYPE);
    }

    public async Task<IEnumerable<Modules.Tenant.Models.Tenant>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.UserTenants
            .Where(ut => ut.UserId == userId && ut.IsActive && !ut.IsDeleted)
            .Include(ut => ut.Tenant)
            .Select(ut => ut.Tenant)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<ContentTypePermission>> GetUserGlobalPermissionsAsync(Guid userId)
    {
        // Get global permissions using ContentTypePermission with null TenantId
        return await _context.ContentTypePermissions
            .Where(p => p.UserId == userId && EF.Property<Guid?>(p, "TenantId") == null && p.IsValid)
            .Include(p => p.User)
            .Include(p => p.AssignedByUser)
            .ToListAsync();
    }

    // ===== LAYER 2: CONTENT-TYPE-WIDE PERMISSIONS =====

    public async Task AssignContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permissions, Guid assignedByUserId)
    {
        // Check if permission already exists
        ContentTypePermission? existingPermission = await _context.ContentTypePermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ContentTypeName == contentTypeName &&
                                      EF.Property<Guid?>(p, "TenantId") == tenantId && !p.IsDeleted
            );

        if (existingPermission != null)
        {
            // Update existing permission
            existingPermission.Permissions = permissions;
            existingPermission.AssignedAt = DateTime.UtcNow;
            existingPermission.AssignedByUserId = assignedByUserId;
            existingPermission.IsActive = true;
            existingPermission.Touch();
        }
        else
        {
            // Get user and assignedByUser entities
            User? user = await _context.Users.FindAsync(userId);
            User? assignedByUser = await _context.Users.FindAsync(assignedByUserId);

            if (user == null || assignedByUser == null)
                throw new ArgumentException("User not found");

            // Get tenant if specified
            Modules.Tenant.Models.Tenant? tenant = null;
            if (tenantId.HasValue)
            {
                tenant = await _context.Tenants.FindAsync(tenantId.Value);

                if (tenant == null)
                    throw new ArgumentException("Tenant not found");
            }

            // Create new permission
            var permission = new ContentTypePermission
            {
                User = user,
                Tenant = tenant,
                ContentTypeName = contentTypeName,
                Permissions = permissions,
                AssignedByUser = assignedByUser,
                IsActive = true
            };

            _context.ContentTypePermissions.Add(permission);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permission)
    {
        PermissionType userPermissions = await GetUserContentTypePermissionsAsync(userId, tenantId, contentTypeName);

        return userPermissions.HasFlag(permission);
    }

    public async Task<PermissionType> GetUserContentTypePermissionsAsync(Guid userId, Guid? tenantId, string contentTypeName)
    {
        // Get all content type permissions for this user and content type
        var permissions = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId && p.ContentTypeName == contentTypeName && p.IsValid &&
                        EF.Property<Guid?>(p, "TenantId") == tenantId
            )
            .Select(p => p.Permissions)
            .ToListAsync();

        // Combine all permissions using bitwise OR
        return permissions.Aggregate(PermissionType.None, (current, perm) => current | perm);
    }

    public async Task<IEnumerable<ContentTypePermission>> GetUserContentTypePermissionsAsync(Guid userId)
    {
        return await _context.ContentTypePermissions
            .Where(p => p.UserId == userId && p.IsValid)
            .Include(p => p.User)
            .Include(p => p.Tenant)
            .Include(p => p.AssignedByUser)
            .ToListAsync();
    }

    // ===== LAYER 3: RESOURCE-SPECIFIC PERMISSIONS =====

    public async Task GrantResourcePermissionAsync(Guid userId, Guid resourceId, PermissionType permissions, Guid grantedByUserId)
    {
        // Get resource info for proper typing
        ResourceInfo? resourceInfo = await GetResourceInfoAsync(resourceId);

        if (resourceInfo == null)
            throw new ArgumentException("Resource not found", nameof(resourceId));

        // Check if permission already exists
        ResourcePermission? existingPermission = await _context.ResourcePermissions
            .FirstOrDefaultAsync(p => p.User!.Id == userId && p.Resource.Id == resourceId && !p.IsDeleted);

        if (existingPermission != null)
        {
            // Update existing permission
            existingPermission.Permissions = permissions;
            existingPermission.GrantedAt = DateTime.UtcNow;
            existingPermission.GrantedByUserId = grantedByUserId;
            existingPermission.IsActive = true;
            existingPermission.Touch();
        }
        else
        {
            // Get user and grantedByUser entities
            User? user = await _context.Users.FindAsync(userId);
            User? grantedByUser = await _context.Users.FindAsync(grantedByUserId);

            if (user == null || grantedByUser == null)
                throw new ArgumentException("User not found");

            // Create new permission
            var permission = new ResourcePermission
            {
                User = user,
                ResourceType = resourceInfo.ContentType,
                Permissions = permissions,
                GrantedByUser = grantedByUser,
                IsActive = true
            };

            _context.ResourcePermissions.Add(permission);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Guid resourceId, PermissionType permission)
    {
        PermissionType userPermissions = await GetUserPermissionsAsync(userId, resourceId);

        return userPermissions.HasFlag(permission);
    }

    public async Task<PermissionType> GetUserPermissionsAsync(Guid userId, Guid resourceId)
    {
        ResourceInfo? resourceInfo = await GetResourceInfoAsync(resourceId);

        if (resourceInfo == null)
            return PermissionType.None; // Default to no permissions

        var combinedPermissions = PermissionType.None;

        // Layer 1: Get tenant-wide permissions
        PermissionType tenantPermissions = await GetUserTenantPermissionsAsync(userId, resourceInfo.TenantId);
        combinedPermissions |= tenantPermissions;

        // Also check global permissions (tenant-wide with null tenant)
        PermissionType globalPermissions = await GetUserTenantPermissionsAsync(userId, null);
        combinedPermissions |= globalPermissions;

        // Layer 2: Get content-type-wide permissions
        PermissionType contentTypePermissions = await GetUserContentTypePermissionsAsync(userId, resourceInfo.TenantId, resourceInfo.ContentType);
        combinedPermissions |= contentTypePermissions;

        // Also check global content type permissions
        PermissionType globalContentTypePermissions = await GetUserContentTypePermissionsAsync(userId, null, resourceInfo.ContentType);
        combinedPermissions |= globalContentTypePermissions;

        // Layer 3: Get resource-specific permissions
        var resourcePermissions = await _context.ResourcePermissions
            .Where(p => p.User!.Id == userId && p.Resource.Id == resourceId && p.IsValid)
            .Select(p => p.Permissions)
            .ToListAsync();

        foreach (PermissionType perm in resourcePermissions)
        {
            combinedPermissions |= perm;
        }

        return combinedPermissions;
    }

    // ===== HELPER METHODS =====

    public async Task<Guid?> GetResourceTenantIdAsync(Guid resourceId)
    {
        ResourceInfo? resourceInfo = await GetResourceInfoAsync(resourceId);

        return resourceInfo?.TenantId;
    }

    public async Task<string?> GetResourceContentTypeAsync(Guid resourceId)
    {
        ResourceInfo? resourceInfo = await GetResourceInfoAsync(resourceId);

        return resourceInfo?.ContentType;
    }

    public async Task RemovePermissionAsync(Guid permissionId)
    {
        // Try to find the permission in each permission table
        ResourcePermission? resourcePermission = await _context.ResourcePermissions.FindAsync(permissionId);
        if (resourcePermission != null)
        {
            resourcePermission.SoftDelete();
            await _context.SaveChangesAsync();

            return;
        }

        ContentTypePermission? contentTypePermission = await _context.ContentTypePermissions.FindAsync(permissionId);
        if (contentTypePermission != null)
        {
            contentTypePermission.SoftDelete();
            await _context.SaveChangesAsync();

            return;
        }
    }

    public async Task RemoveUserFromTenantAsync(Guid userId, Guid? tenantId)
    {
        if (tenantId == null)
        {
            // For global permissions, remove ContentTypePermission with null TenantId
            var globalContentTypePermissions = await _context.ContentTypePermissions
                .Where(p => p.UserId == userId && EF.Property<Guid?>(p, "TenantId") == null && !p.IsDeleted)
                .ToListAsync();

            foreach (ContentTypePermission permission in globalContentTypePermissions)
            {
                permission.SoftDelete();
            }

            await _context.SaveChangesAsync();

            return;
        }

        // Find the UserTenant relationship
        UserTenant? userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted);

        if (userTenant == null)
            return;

        // Remove content type permissions for this tenant
        var contentTypePermissions = await _context.ContentTypePermissions
            .Where(p => p.UserId == userId && EF.Property<Guid?>(p, "TenantId") == tenantId && !p.IsDeleted)
            .ToListAsync();

        foreach (ContentTypePermission permission in contentTypePermissions)
        {
            permission.SoftDelete();
        }

        await _context.SaveChangesAsync();
    }

    private Task<ResourceInfo?> GetResourceInfoAsync(Guid resourceId)
    {
        // Check UserProfile table

        // Add more resource types as needed...
        // For now, we'll return null if resource not found
        return Task.FromResult<ResourceInfo?>(null);
    }

    private class ResourceInfo
    {
        public string ContentType
        {
            get;
            set;
        } = string.Empty;

        public Guid? TenantId
        {
            get;
            set;
        }
    }
}
