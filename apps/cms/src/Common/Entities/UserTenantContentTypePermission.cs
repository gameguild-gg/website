using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Common.Entities;

// todo: delete this
/// <summary>
/// Entity representing tenant-specific content-type-wide permissions for users
/// Layer 2b of the three-layer permission system: Tenant-specific content type permissions
/// Grants permissions for all resources of a specific content type within a specific tenant
/// Links to UserTenant to maintain proper tenant relationship architecture
/// </summary>
public class UserTenantContentTypePermission : BaseEntity
{
    /// <summary>
    /// Foreign key to the UserTenant junction entity
    /// This maintains the proper relationship architecture by going through UserTenant
    /// </summary>
    [Required]
    public Guid UserTenantId { get; set; }

    /// <summary>
    /// Navigation property to the UserTenant junction entity
    /// </summary>
    [ForeignKey(nameof(UserTenantId))]
    public virtual Modules.Tenant.Models.UserTenant UserTenant { get; set; } = null!;

    /// <summary>
    /// Content type name for polymorphic relationship
    /// Uses string-based content type names from ContentTypes class for flexibility
    /// Examples: "UserProfile", "Post", "Comment", "Document", etc.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContentTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Bitwise permissions granted for this content type within the tenant
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
