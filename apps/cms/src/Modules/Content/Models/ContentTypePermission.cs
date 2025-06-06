using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Common.Entities;

/// <summary>
/// Entity representing content-type-wide permissions for users
/// Layer 2 of the three-layer permission system: Tenant → ContentType → Resource
/// Grants permissions for all resources of a specific content type within a tenant (or globally when Tenant is null)
/// </summary>
public sealed class ContentTypePermission : BaseEntity, ITenantable
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
    public Modules.User.Models.User User
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Foreign key to the Tenant entity (null for global permissions)
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Navigation property to the tenant
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public override Modules.Tenant.Models.Tenant? Tenant { get; set; }

    /// <summary>
    /// Indicates if this permission applies globally across all tenants
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Content type name for polymorphic relationship
    /// Uses string-based content type names from ContentTypes class for flexibility
    /// Examples: "UserProfile", "Post", "Comment", "Document", etc.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContentTypeName
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Permissions granted for this content type
    /// </summary>
    [Required]
    public UnifiedPermissionContext PermissionContext
    {
        get;
        set;
    } = new UnifiedPermissionContext();

    /// <summary>
    /// When this permission was assigned
    /// </summary>
    public DateTime AssignedAt
    {
        get;
        set;
    } = DateTime.UtcNow;

    /// <summary>
    /// User who assigned this permission
    /// </summary>
    [Required]
    public Guid AssignedByUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the user who assigned this permission
    /// </summary>
    [ForeignKey(nameof(AssignedByUserId))]
    public Modules.User.Models.User AssignedByUser
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Optional expiration date for this permission
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this permission is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Check if the permission is expired
    /// </summary>
    public bool IsExpired
    {
        get => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// Check if the permission is valid (active and not expired)
    /// </summary>
    public bool IsValid
    {
        get => IsActive && !IsExpired && !IsDeleted;
    }
}
