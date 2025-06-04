using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Common.Entities;

// todo: should we have this?
/// <summary>
/// Entity representing tenant-wide permissions for users
/// Layer 1 of the three-layer permission system: Tenant → ContentType → Resource
/// Links to UserTenant to represent permissions within that specific user-tenant relationship
/// </summary>
public class UserTenantPermission : BaseEntity
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
    public virtual Modules.Tenant.Models.UserTenant UserTenant { get; set; } = null!;

    /// <summary>
    /// Bitwise permissions granted for this tenant (or globally if TenantId is null)
    /// </summary>
    [Required]
    public PermissionType Permissions { get; set; }

    /// <summary>
    /// When this permission was assigned
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who assigned this permission
    /// </summary>
    [Required]
    public Guid AssignedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the user who assigned this permission
    /// </summary>
    [ForeignKey(nameof(AssignedByUserId))]
    public virtual Modules.User.Models.User AssignedByUser { get; set; } = null!;

    /// <summary>
    /// Optional expiration date for this permission
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether this permission is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Check if the permission is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

    /// <summary>
    /// Check if the permission is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired && !IsDeleted;
}
