using System.ComponentModel.DataAnnotations;
using cms.Modules.User.Dtos;

namespace cms.Modules.Tenant.Dtos;

/// <summary>
/// DTO for creating a new tenant
/// </summary>
public class CreateTenantDto
{
    /// <summary>
    /// Name of the tenant
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the tenant
    /// </summary>
    [MaxLength(500)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tenant is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
}

/// <summary>
/// DTO for updating an existing tenant
/// </summary>
public class UpdateTenantDto
{
    /// <summary>
    /// Name of the tenant
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the tenant
    /// </summary>
    [MaxLength(500)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tenant is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
}

/// <summary>
/// DTO for tenant response
/// </summary>
public class TenantResponseDto
{
    /// <summary>
    /// Unique identifier for the tenant
    /// </summary>
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Name of the tenant
    /// </summary>
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the tenant
    /// </summary>
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tenant is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    }

    /// <summary>
    /// Version number for optimistic concurrency control
    /// </summary>
    public int Version
    {
        get;
        set;
    }

    /// <summary>
    /// When the tenant was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the tenant was last updated
    /// </summary>
    public DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the tenant was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the tenant is soft deleted
    /// </summary>
    public bool IsDeleted
    {
        get => DeletedAt.HasValue;
    }

    /// <summary>
    /// Permissions and users in this tenant
    /// </summary>
    public ICollection<TenantPermissionResponseDto> TenantPermissions
    {
        get;
        set;
    } = new List<TenantPermissionResponseDto>();
}
/// <summary>
/// DTO for tenant permission response
/// </summary>
public class TenantPermissionResponseDto
{
    /// <summary>
    /// Unique identifier for the tenant permission
    /// </summary>
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the User entity (null for default permissions)
    /// </summary>
    public Guid? UserId
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the Tenant entity (null for global defaults)
    /// </summary>
    public Guid? TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tenant permission is currently valid (not expired and not deleted)
    /// </summary>
    public bool IsValid
    {
        get;
        set;
    }

    /// <summary>
    /// Permission expiry date
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Permission flags for bits 0-63
    /// </summary>
    public ulong PermissionFlags1
    {
        get;
        set;
    }

    /// <summary>
    /// Permission flags for bits 64-127
    /// </summary>
    public ulong PermissionFlags2
    {
        get;
        set;
    }

    /// <summary>
    /// Version number for optimistic concurrency control
    /// </summary>
    public int Version
    {
        get;
        set;
    }

    /// <summary>
    /// When the permission was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the permission was last updated
    /// </summary>
    public DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the permission was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the permission is soft deleted
    /// </summary>
    public bool IsDeleted
    {
        get => DeletedAt.HasValue;
    }

    /// <summary>
    /// Associated user information
    /// </summary>
    public UserResponseDto? User
    {
        get;
        set;
    }

    /// <summary>
    /// Associated tenant information
    /// </summary>
    public TenantResponseDto? Tenant
    {
        get;
        set;
    }
}

