using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;

namespace cms.Modules.Tenant.Models;

/// <summary>
/// Junction entity representing the assignment of a role to a user within a specific tenant
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
public class UserTenantRole : BaseEntity
{
    /// <summary>
    /// Foreign key to the UserTenant entity
    /// </summary>
    [Required]
    public Guid UserTenantId { get; set; }

    /// <summary>
    /// Navigation property to the UserTenant entity
    /// </summary>
    [ForeignKey(nameof(UserTenantId))]
    public virtual UserTenant UserTenant { get; set; } = null!;

    /// <summary>
    /// Foreign key to the TenantRole entity
    /// </summary>
    [Required]
    public Guid TenantRoleId { get; set; }

    /// <summary>
    /// Navigation property to the TenantRole entity
    /// </summary>
    [ForeignKey(nameof(TenantRoleId))]
    public virtual TenantRole TenantRole { get; set; } = null!;

    /// <summary>
    /// Whether this role assignment is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this role was assigned
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this role assignment expires (optional)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public UserTenantRole() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user tenant role data</param>
    public UserTenantRole(object partial) : base(partial) { }

    /// <summary>
    /// Check if the role assignment is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

    /// <summary>
    /// Check if the role assignment is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired && !IsDeleted;

    /// <summary>
    /// Activate the role assignment
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    /// <summary>
    /// Deactivate the role assignment
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
