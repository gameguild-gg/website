using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using cms.Common.Enums;
using cms.Data;
using cms.Modules.Tenant.Models;

namespace cms.Common.Services;

/// <summary>
/// Implementation of the three-layer permission service
/// Layer 1: Tenant-wide permissions with default support
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ===== LAYER 1: TENANT-WIDE PERMISSIONS =====

    public async Task<TenantPermission> GrantTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions)
    {
        if (permissions?.Length == 0)
            throw new ArgumentException("At least one permission must be specified", nameof(permissions));

        if (permissions?.Length > TenantPermissionConstants.MaxPermissionsPerGrant)
            throw new ArgumentException($"Cannot grant more than {TenantPermissionConstants.MaxPermissionsPerGrant} permissions at once", nameof(permissions));

        // Find existing permission record or create new one
        var existingPermission = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && !tp.IsDeleted);

        TenantPermission tenantPermission;

        if (existingPermission != null)
        {
            // Update existing permission
            tenantPermission = existingPermission;
            foreach (var permission in permissions!)
            {
                tenantPermission.AddPermission(permission);
            }
            tenantPermission.Touch();
        }
        else
        {
            // Create new permission record
            tenantPermission = new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                Status = userId.HasValue ? UserTenantStatus.Active : UserTenantStatus.Active // Default status
            };

            foreach (var permission in permissions!)
            {
                tenantPermission.AddPermission(permission);
            }

            _context.TenantPermissions.Add(tenantPermission);
        }

        await _context.SaveChangesAsync();
        return tenantPermission;
    }

    public async Task<bool> HasTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType permission)
    {
        // For null user, only check default permissions
        if (!userId.HasValue)
        {
            return await CheckDefaultPermissionAsync(tenantId, permission);
        }

        // 1. Check user-specific permissions first
        var userPermission = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && 
                                     tp.IsActive && !tp.IsDeleted);

        if (userPermission?.IsValid == true && userPermission.HasPermission(permission))
        {
            return true;
        }

        // 2. Check tenant default permissions
        if (tenantId.HasValue)
        {
            var tenantDefault = await _context.TenantPermissions
                .FirstOrDefaultAsync(tp => tp.UserId == null && tp.TenantId == tenantId && 
                                         tp.IsActive && !tp.IsDeleted);

            if (tenantDefault?.IsValid == true && tenantDefault.HasPermission(permission))
            {
                return true;
            }
        }

        // 3. Check global default permissions
        var globalDefault = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == null && tp.TenantId == null && 
                                     tp.IsActive && !tp.IsDeleted);

        return globalDefault?.IsValid == true && globalDefault.HasPermission(permission);
    }

    public async Task<IEnumerable<PermissionType>> GetTenantPermissionsAsync(Guid? userId, Guid? tenantId)
    {
        var permission = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && 
                                     tp.IsActive && !tp.IsDeleted);

        if (permission?.IsValid != true)
            return Enumerable.Empty<PermissionType>();

        return GetPermissionsFromEntity(permission);
    }

    public async Task RevokeTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions)
    {
        var tenantPermission = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && !tp.IsDeleted);

        if (tenantPermission == null)
            return;

        foreach (var permission in permissions)
        {
            tenantPermission.RemovePermission(permission);
        }

        tenantPermission.Touch();
        await _context.SaveChangesAsync();
    }

    // === DEFAULT PERMISSION MANAGEMENT ===

    public async Task<TenantPermission> SetTenantDefaultPermissionsAsync(Guid? tenantId, PermissionType[] permissions)
    {
        return await GrantTenantPermissionAsync(null, tenantId, permissions);
    }

    public async Task<TenantPermission> SetGlobalDefaultPermissionsAsync(PermissionType[] permissions)
    {
        return await GrantTenantPermissionAsync(null, null, permissions);
    }

    public async Task<IEnumerable<PermissionType>> GetTenantDefaultPermissionsAsync(Guid? tenantId)
    {
        return await GetTenantPermissionsAsync(null, tenantId);
    }

    public async Task<IEnumerable<PermissionType>> GetGlobalDefaultPermissionsAsync()
    {
        return await GetTenantPermissionsAsync(null, null);
    }

    public async Task<IEnumerable<PermissionType>> GetEffectiveTenantPermissionsAsync(Guid userId, Guid? tenantId)
    {
        var effectivePermissions = new HashSet<PermissionType>();

        // 1. Start with global defaults
        var globalDefaults = await GetGlobalDefaultPermissionsAsync();
        foreach (var permission in globalDefaults)
        {
            effectivePermissions.Add(permission);
        }

        // 2. Add tenant defaults (if applicable)
        if (tenantId.HasValue)
        {
            var tenantDefaults = await GetTenantDefaultPermissionsAsync(tenantId);
            foreach (var permission in tenantDefaults)
            {
                effectivePermissions.Add(permission);
            }
        }

        // 3. Add user-specific permissions (override/add to defaults)
        var userPermissions = await GetTenantPermissionsAsync(userId, tenantId);
        foreach (var permission in userPermissions)
        {
            effectivePermissions.Add(permission);
        }

        return effectivePermissions;
    }

    // === USER-TENANT MEMBERSHIP FUNCTIONALITY ===

    public async Task<IEnumerable<TenantPermission>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.TenantPermissions
            .Include(tp => tp.Tenant)
            .Where(tp => tp.UserId == userId && tp.TenantId != null && 
                        tp.IsActive && !tp.IsDeleted)
            .ToListAsync();
    }

    public async Task<TenantPermission> JoinTenantAsync(Guid userId, Guid tenantId)
    {
        // Check if user is already a member
        var existingMembership = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && !tp.IsDeleted);

        if (existingMembership != null)
        {
            // Reactivate if inactive
            if (!existingMembership.IsActive || existingMembership.Status != UserTenantStatus.Active)
            {
                existingMembership.IsActive = true;
                existingMembership.Status = UserTenantStatus.Active;
                existingMembership.JoinedAt = DateTime.UtcNow;
                existingMembership.Touch();
                await _context.SaveChangesAsync();
            }
            return existingMembership;
        }

        // Create new membership with minimal permissions
        var membership = new TenantPermission
        {
            UserId = userId,
            TenantId = tenantId,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Status = UserTenantStatus.Active
        };

        // Grant minimal permissions
        foreach (var permission in TenantPermissionConstants.MinimalUserPermissions)
        {
            membership.AddPermission(permission);
        }

        _context.TenantPermissions.Add(membership);
        await _context.SaveChangesAsync();

        return membership;
    }

    public async Task LeaveTenantAsync(Guid userId, Guid tenantId)
    {
        var membership = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && !tp.IsDeleted);

        if (membership != null)
        {
            membership.Status = UserTenantStatus.Inactive;
            membership.IsActive = false;
            membership.Touch();
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsUserInTenantAsync(Guid userId, Guid tenantId)
    {
        var membership = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && !tp.IsDeleted);

        return membership?.IsActiveMembership == true;
    }

    public async Task<TenantPermission> UpdateTenantMembershipStatusAsync(Guid userId, Guid tenantId, UserTenantStatus status)
    {
        var membership = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && !tp.IsDeleted);

        if (membership == null)
            throw new InvalidOperationException("User is not a member of this tenant");

        if (!membership.CanTransitionTo(status))
            throw new InvalidOperationException($"Cannot transition from {membership.Status} to {status}");

        membership.UpdateStatus(status);
        
        // Update IsActive based on status
        membership.IsActive = status == UserTenantStatus.Active;

        await _context.SaveChangesAsync();
        return membership;
    }

    // ===== LAYER 2: CONTENT-TYPE-WIDE PERMISSIONS =====

    public async Task GrantContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions)
    {
        if (string.IsNullOrWhiteSpace(contentTypeName))
            throw new ArgumentException("Content type name cannot be null or empty", nameof(contentTypeName));

        if (permissions?.Length == 0)
            throw new ArgumentException("At least one permission must be specified", nameof(permissions));

        // Find existing permission record or create new one
        var existingPermission = await _context.ContentTypePermissions
            .FirstOrDefaultAsync(ctp => ctp.UserId == userId && 
                                       ctp.TenantId == tenantId && 
                                       ctp.ContentTypeName == contentTypeName && 
                                       !ctp.IsDeleted);

        ContentTypePermission contentTypePermission;

        if (existingPermission != null)
        {
            // Update existing permission
            contentTypePermission = existingPermission;
            foreach (var permission in permissions!)
            {
                contentTypePermission.AddPermission(permission);
            }
            contentTypePermission.Touch();
        }
        else
        {
            // Create new permission record
            contentTypePermission = new ContentTypePermission
            {
                UserId = userId,
                TenantId = tenantId,
                ContentTypeName = contentTypeName,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = userId ?? Guid.Empty, // TODO: Get from current user context
                IsActive = true
            };

            // Add permissions
            foreach (var permission in permissions!)
            {
                contentTypePermission.AddPermission(permission);
            }

            _context.ContentTypePermissions.Add(contentTypePermission);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permission)
    {
        if (string.IsNullOrWhiteSpace(contentTypeName))
            return false;

        // Check user-specific content type permission
        var userPermission = await _context.ContentTypePermissions
            .FirstOrDefaultAsync(ctp => ctp.UserId == userId && 
                                       ctp.TenantId == tenantId && 
                                       ctp.ContentTypeName == contentTypeName && 
                                       ctp.IsValid);

        if (userPermission?.HasPermission(permission) == true)
            return true;

        // Check tenant default for this content type
        if (tenantId.HasValue)
        {
            var tenantDefault = await _context.ContentTypePermissions
                .FirstOrDefaultAsync(ctp => ctp.UserId == null && 
                                           ctp.TenantId == tenantId && 
                                           ctp.ContentTypeName == contentTypeName && 
                                           ctp.IsValid);

            if (tenantDefault?.HasPermission(permission) == true)
                return true;
        }

        // Check global default for this content type
        var globalDefault = await _context.ContentTypePermissions
            .FirstOrDefaultAsync(ctp => ctp.UserId == null && 
                                       ctp.TenantId == null && 
                                       ctp.ContentTypeName == contentTypeName && 
                                       ctp.IsValid);

        return globalDefault?.HasPermission(permission) == true;
    }

    public async Task<IEnumerable<PermissionType>> GetContentTypePermissionsAsync(Guid? userId, Guid? tenantId, string contentTypeName)
    {
        if (string.IsNullOrWhiteSpace(contentTypeName))
            return Enumerable.Empty<PermissionType>();

        var contentTypePermission = await _context.ContentTypePermissions
            .FirstOrDefaultAsync(ctp => ctp.UserId == userId && 
                                       ctp.TenantId == tenantId && 
                                       ctp.ContentTypeName == contentTypeName && 
                                       ctp.IsValid);

        if (contentTypePermission == null)
            return Enumerable.Empty<PermissionType>();

        return GetPermissionsFromContentTypeEntity(contentTypePermission);
    }

    public async Task RevokeContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions)
    {
        if (string.IsNullOrWhiteSpace(contentTypeName))
            throw new ArgumentException("Content type name cannot be null or empty", nameof(contentTypeName));

        if (permissions?.Length == 0)
            return; // Nothing to revoke

        var existingPermission = await _context.ContentTypePermissions
            .FirstOrDefaultAsync(ctp => ctp.UserId == userId && 
                                       ctp.TenantId == tenantId && 
                                       ctp.ContentTypeName == contentTypeName && 
                                       !ctp.IsDeleted);

        if (existingPermission != null)
        {
            foreach (var permission in permissions!)
            {
                existingPermission.RemovePermission(permission);
            }
            existingPermission.Touch();
            await _context.SaveChangesAsync();
        }
    }

    private static IEnumerable<PermissionType> GetPermissionsFromContentTypeEntity(ContentTypePermission permission)
    {
        var permissions = new List<PermissionType>();

        // Check all possible permission types
        foreach (PermissionType permissionType in Enum.GetValues<PermissionType>())
        {
            if (permission.HasPermission(permissionType))
            {
                permissions.Add(permissionType);
            }
        }

        return permissions;
    }

    // === PRIVATE HELPER METHODS ===

    private async Task<bool> CheckDefaultPermissionAsync(Guid? tenantId, PermissionType permission)
    {
        // Check tenant default
        if (tenantId.HasValue)
        {
            var tenantDefault = await _context.TenantPermissions
                .FirstOrDefaultAsync(tp => tp.UserId == null && tp.TenantId == tenantId && 
                                         tp.IsActive && !tp.IsDeleted);

            if (tenantDefault?.IsValid == true && tenantDefault.HasPermission(permission))
            {
                return true;
            }
        }

        // Check global default
        var globalDefault = await _context.TenantPermissions
            .FirstOrDefaultAsync(tp => tp.UserId == null && tp.TenantId == null && 
                                     tp.IsActive && !tp.IsDeleted);

        return globalDefault?.IsValid == true && globalDefault.HasPermission(permission);
    }

    private static IEnumerable<PermissionType> GetPermissionsFromEntity(TenantPermission permission)
    {
        var permissions = new List<PermissionType>();

        // Check all possible permission types
        foreach (PermissionType permissionType in Enum.GetValues<PermissionType>())
        {
            if (permission.HasPermission(permissionType))
            {
                permissions.Add(permissionType);
            }
        }

        return permissions;
    }

    // ===== HELPER METHODS =====

    public async Task<Guid?> GetResourceTenantIdAsync(Guid resourceId)
    {
        // TODO: Implement when we have concrete resource entities
        // This will need to query the specific resource tables to find tenant ID
        await Task.CompletedTask;
        return null;
    }

    public async Task<string?> GetResourceContentTypeAsync(Guid resourceId)
    {
        // TODO: Implement when we have concrete resource entities  
        // This will need to determine the content type based on the resource
        await Task.CompletedTask;
        return null;
    }
}
