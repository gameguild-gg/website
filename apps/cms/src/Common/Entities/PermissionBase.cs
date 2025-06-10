using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Common.Entities;

/// <summary>
/// Base class for all permission entities that contain common user/tenant relationship patterns
/// Eliminates duplication between TenantPermission and ContentTypePermission
/// </summary>
public abstract class PermissionBase : WithPermissions
{
    /// <summary>
    /// User relationship - NULL means default permissions
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// Navigation property to the User entity
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual Modules.User.Models.User? User { get; set; }
    
    /// <summary>
    /// Tenant relationship - NULL means global defaults
    /// </summary>
    public Guid? TenantId { get; set; }
    
    /// <summary>
    /// Navigation property to the Tenant entity
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public virtual new Modules.Tenant.Models.Tenant? Tenant { get; set; }
    
    /// <summary>
    /// Optional expiration date for this permission
    /// If null, permission never expires
    /// If date has passed, permission is expired
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    // Computed properties
    
    /// <summary>
    /// Check if the permission is expired based on ExpiresAt date
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    
    /// <summary>
    /// Check if the permission is valid (not deleted and not expired)
    /// </summary>
    public bool IsValid => !IsDeleted && !IsExpired;
    
    /// <summary>
    /// Check if this is a default permission for a specific tenant
    /// </summary>
    public bool IsDefaultPermission => UserId == null && TenantId != null;
    
    /// <summary>
    /// Check if this is a global default permission
    /// </summary>
    public bool IsGlobalDefaultPermission => UserId == null && TenantId == null;
    
    /// <summary>
    /// Check if this is a user-specific permission
    /// </summary>
    public bool IsUserPermission => UserId != null;
}
