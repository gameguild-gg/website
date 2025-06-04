using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;

namespace cms.Modules.Tenant.Models;

/// <summary>
/// Junction entity representing the relationship between a User and a Tenant
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
public class UserTenant : BaseEntity
{
    /// <summary>
    /// Foreign key to the User entity
    /// </summary>
    [Required]
    public Guid UserId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the User entity
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User.Models.User User
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Foreign key to the Tenant entity
    /// </summary>
    [Required]
    public Guid TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the Tenant entity
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public virtual Tenant Tenant
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Whether this user-tenant relationship is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// When the user joined this tenant
    /// </summary>
    public DateTime JoinedAt
    {
        get;
        set;
    } = DateTime.UtcNow;    /// <summary>
    /// Navigation property to user-tenant-role assignments
    /// </summary>
    public virtual ICollection<UserTenantRole> UserTenantRoles
    {
        get;
        set;
    } = new List<UserTenantRole>();    /// <summary>
    /// Navigation property to user tenant permissions (Layer 1 of permission system)
    /// Represents the specific permissions this user has within this tenant
    /// </summary>
    public virtual ICollection<cms.Common.Entities.UserTenantPermission> UserTenantPermissions
    {
        get;
        set;
    } = new List<cms.Common.Entities.UserTenantPermission>();

    /// <summary>
    /// Default constructor
    /// </summary>
    public UserTenant() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user tenant data</param>
    public UserTenant(object partial) : base(partial) { }

    /// <summary>
    /// Activate the user-tenant relationship
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    /// <summary>
    /// Deactivate the user-tenant relationship
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
