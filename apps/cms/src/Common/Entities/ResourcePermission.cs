using System.ComponentModel.DataAnnotations;

namespace cms.Common.Entities;

/// <summary>
/// Entity representing permissions on resources for specific users or roles
/// Provides fine-grained access control for resources
/// </summary>
public class ResourcePermission : BaseEntity
{
    /// <summary>
    /// Navigation property to the user who has this permission
    /// Entity Framework will automatically create the UserId foreign key
    /// </summary>
    public virtual Modules.User.Models.User? User
    {
        get;
        set;
    }

    // todo: add polymorphism support for resource types
    /// <summary>
    /// Type of the resource (for polymorphic relationships)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Navigation property to resource metadata
    /// Entity Framework will automatically create the ResourceMetadataId foreign key
    /// </summary>
    public virtual ResourceMetadata? ResourceMetadata
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the specific resource this permission applies to
    /// Entity Framework will automatically create the ResourceId foreign key
    /// </summary>
    [Required]
    public virtual ResourceBase Resource
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Bitwise permissions granted for this specific resource
    /// Layer 3 of the three-layer permission system: Tenant → ContentType → Resource
    /// </summary>
    [Required]
    public PermissionType Permissions
    {
        get;
        set;
    }

    /// <summary>
    /// When this permission was granted
    /// </summary>
    public DateTime GrantedAt
    {
        get;
        set;
    } = DateTime.UtcNow;

    /// <summary>
    /// User who granted this permission
    /// </summary>
    [Required]
    public Guid GrantedByUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the user who granted this permission
    /// </summary>
    public virtual Modules.User.Models.User GrantedByUser
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
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

    /// <summary>
    /// Check if the permission is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired && !IsDeleted;
}
