using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;

namespace cms.Modules.Tenant.Models;

/// <summary>
/// Represents a role within a specific tenant
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
public class TenantRole : BaseEntity
{
    /// <summary>
    /// Foreign key to the Tenant entity
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Navigation property to the Tenant entity
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public virtual Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Name of the role (e.g., "Admin", "User", "Viewer")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the role
    /// </summary>
    [MaxLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// Permissions associated with this role (JSON array of permission strings)
    /// </summary>
    [MaxLength(2000)]
    public string? Permissions { get; set; }

    /// <summary>
    /// Whether this role is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property to user-tenant-role assignments
    /// </summary>
    public virtual ICollection<UserTenantRole> UserTenantRoles { get; set; } = new List<UserTenantRole>();

    /// <summary>
    /// Default constructor
    /// </summary>
    public TenantRole() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial tenant role data</param>
    public TenantRole(object partial) : base(partial) { }

    /// <summary>
    /// Activate the role
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    /// <summary>
    /// Deactivate the role
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
