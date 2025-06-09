using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using cms.Common.Enums;
using System.Collections.ObjectModel;

namespace cms.Modules.Tenant.Models;

/// <summary>
/// Unified entity for tenant permissions and user-tenant relationships
/// Replaces UserTenant and provides the foundation for the three-layer DAC permission system
/// </summary>
[Table("TenantPermissions")]
[Index(nameof(UserId), nameof(TenantId), IsUnique = false)]
[Index(nameof(TenantId))]
[Index(nameof(UserId))]
[Index(nameof(Status))]
[Index(nameof(ExpiresAt))]
[Index(nameof(IsActive))]
public class TenantPermission : WithPermissions
{
    /// <summary>
    /// User relationship - NULL means default permissions for the tenant
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// Navigation property to the User entity
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual cms.Modules.User.Models.User? User { get; set; }
    
    /// <summary>
    /// Tenant relationship - NULL means global tenant default permissions
    /// </summary>
    public Guid? TenantId { get; set; }
    
    /// <summary>
    /// Navigation property to the Tenant entity
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public virtual new Tenant? Tenant { get; set; }
    
    /// <summary>
    /// Permission expiration date
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Whether this permission/membership is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When the user joined this tenant (for user-tenant relationships)
    /// </summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Status of the user-tenant relationship
    /// </summary>
    public UserTenantStatus Status { get; set; } = UserTenantStatus.Active;
    
    /// <summary>
    /// Navigation property to content type permissions for this user within this tenant (Layer 2 of permission system)
    /// </summary>
    public virtual ICollection<cms.Common.Entities.ContentTypePermission> ContentTypePermissions { get; set; } = new List<cms.Common.Entities.ContentTypePermission>();
    
    // Computed properties
    
    /// <summary>
    /// Whether this permission is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow) && !this.IsDeleted;
    
    /// <summary>
    /// Whether this is a default permission for a specific tenant
    /// </summary>
    public bool IsDefaultTenantPermission => UserId == null && TenantId != null;
    
    /// <summary>
    /// Whether this is a global default permission
    /// </summary>
    public bool IsGlobalDefaultPermission => UserId == null && TenantId == null;
    
    /// <summary>
    /// Whether this is a user-specific tenant permission/membership
    /// </summary>
    public bool IsUserTenantPermission => UserId != null && TenantId != null;
    
    /// <summary>
    /// Whether this represents an active membership
    /// </summary>
    public bool IsActiveMembership => Status == UserTenantStatus.Active && IsValid && UserId != null && TenantId != null;
    
    /// <summary>
    /// Default constructor
    /// </summary>
    public TenantPermission() : base() { }
    
    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial tenant permission data</param>
    public TenantPermission(object partial) : base() 
    {
        // Manual property setting since WithPermissions doesn't have a parameterized constructor
        Status = UserTenantStatus.Active;
        JoinedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Add permissions to this tenant permission
    /// </summary>
    /// <param name="permissions">Permissions to add</param>
    public void AddPermissions(IEnumerable<PermissionType> permissions)
    {
        foreach (var permission in permissions)
        {
            AddPermission(permission);
        }
        Touch();
    }
    
    /// <summary>
    /// Add permissions to this tenant permission
    /// </summary>
    /// <param name="permissions">Permissions to add</param>
    public void AddPermissions(params PermissionType[] permissions)
    {
        AddPermissions((IEnumerable<PermissionType>)permissions);
    }
    
    /// <summary>
    /// Remove permissions from this tenant permission
    /// </summary>
    /// <param name="permissions">Permissions to remove</param>
    public void RemovePermissions(IEnumerable<PermissionType> permissions)
    {
        foreach (var permission in permissions)
        {
            RemovePermission(permission);
        }
        Touch();
    }
    
    /// <summary>
    /// Remove permissions from this tenant permission
    /// </summary>
    /// <param name="permissions">Permissions to remove</param>
    public void RemovePermissions(params PermissionType[] permissions)
    {
        RemovePermissions((IEnumerable<PermissionType>)permissions);
    }
    
    /// <summary>
    /// Get all permissions as an enumerable
    /// </summary>
    /// <returns>Collection of permissions that are set</returns>
    public IEnumerable<PermissionType> GetPermissions()
    {
        var permissions = new List<PermissionType>();
        
        for (int i = 0; i < 128; i++)
        {
            var permission = (PermissionType)i;
            if (HasPermission(permission))
            {
                permissions.Add(permission);
            }
        }
        
        return permissions;
    }
    
    /// <summary>
    /// Set multiple permissions at once
    /// </summary>
    /// <param name="permissions">Permissions to set</param>
    public void SetPermissions(IEnumerable<PermissionType> permissions)
    {
        // Clear all permissions first
        PermissionFlags1 = 0;
        PermissionFlags2 = 0;
        
        // Add the new permissions
        AddPermissions(permissions);
    }
    
    /// <summary>
    /// Set multiple permissions at once
    /// </summary>
    /// <param name="permissions">Permissions to set</param>
    public void SetPermissions(params PermissionType[] permissions)
    {
        SetPermissions((IEnumerable<PermissionType>)permissions);
    }
    
    /// <summary>
    /// Activate the permission/membership
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        Touch();
    }
    
    /// <summary>
    /// Deactivate the permission/membership
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
    
    /// <summary>
    /// Update the membership status
    /// </summary>
    /// <param name="newStatus">New status to set</param>
    public void UpdateStatus(UserTenantStatus newStatus)
    {
        Status = newStatus;
        Touch();
    }
    
    /// <summary>
    /// Check if the status transition is valid
    /// </summary>
    /// <param name="newStatus">Status to transition to</param>
    /// <returns>True if the transition is valid</returns>
    public bool CanTransitionTo(UserTenantStatus newStatus)
    {
        return TenantPermissionConstants.ValidStatusTransitions.TryGetValue(Status, out var validTransitions) &&
               validTransitions.Contains(newStatus);
    }
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
    
    /// <summary>
    /// Valid status transitions for user-tenant relationships
    /// </summary>
    public static readonly Dictionary<UserTenantStatus, UserTenantStatus[]> ValidStatusTransitions = new()
    {
        { UserTenantStatus.Active, new[] { UserTenantStatus.Suspended, UserTenantStatus.Inactive } },
        { UserTenantStatus.Suspended, new[] { UserTenantStatus.Active, UserTenantStatus.Inactive } },
        { UserTenantStatus.Inactive, new[] { UserTenantStatus.Active } }
    };
}
