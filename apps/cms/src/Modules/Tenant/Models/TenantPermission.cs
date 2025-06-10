using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Tenant.Models;

/// <summary>
/// Unified entity for tenant permissions and user-tenant relationships
/// Tenant-wide permissions for users, or default permissions for tenants
/// </summary>
[Table("TenantPermissions")]
[Index(nameof(UserId), nameof(TenantId), IsUnique = true, Name = "IX_TenantPermissions_User_Tenant")]
[Index(nameof(TenantId), Name = "IX_TenantPermissions_TenantId")]
[Index(nameof(UserId), Name = "IX_TenantPermissions_UserId")]
[Index(nameof(ExpiresAt), Name = "IX_TenantPermissions_ExpiresAt")]
public class TenantPermission : PermissionBase
{
    /// <summary>
    /// Navigation property to content type permissions for this user within this tenant (Layer 2 of permission system)
    /// </summary>
    public virtual ICollection<cms.Common.Entities.ContentTypePermission> ContentTypePermissions { get; set; } = new List<cms.Common.Entities.ContentTypePermission>();
    
    // Computed properties specific to tenant permissions
    
    /// <summary>
    /// Whether this represents an active membership
    /// </summary>
    public bool IsActiveMembership => IsValid && UserId != null && TenantId != null;
}

/// <summary>
/// Constants for tenant permission system
/// </summary>
public static class TenantPermissionConstants
{
    public const int MaxPermissionsPerGrant = 50;
    public const int MaxExpirationDays = 365;
    
    /// <summary>
    /// Default permissions that every user gets
    /// </summary>
    public static readonly PermissionType[] MinimalUserPermissions = { PermissionType.Comment, PermissionType.Vote, PermissionType.Share };
}
