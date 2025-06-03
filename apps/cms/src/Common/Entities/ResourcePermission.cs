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

    /// <summary>
    /// Navigation property to the resource role
    /// Entity Framework will automatically create the ResourceRoleId foreign key
    /// </summary>
    public virtual ResourceRole? ResourceRole
    {
        get;
        set;
    }

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
    /// The permission level granted (Read, Write, Delete, Admin, etc.)
    /// </summary>
    [Required]
    public PermissionLevel Permission
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
