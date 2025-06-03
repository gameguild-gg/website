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
    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>
    /// Users in this tenant
    /// </summary>
    public ICollection<UserTenantResponseDto> UserTenants
    {
        get;
        set;
    } = new List<UserTenantResponseDto>();

    /// <summary>
    /// Roles in this tenant
    /// </summary>
    public ICollection<TenantRoleResponseDto> TenantRoles
    {
        get;
        set;
    } = new List<TenantRoleResponseDto>();
}

/// <summary>
/// DTO for creating a new tenant role
/// </summary>
public class CreateTenantRoleDto
{
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
    /// Name of the role
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the role
    /// </summary>
    [MaxLength(200)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Permissions associated with this role (JSON array of permission strings)
    /// </summary>
    [MaxLength(2000)]
    public string? Permissions
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this role is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
}

/// <summary>
/// DTO for updating an existing tenant role
/// </summary>
public class UpdateTenantRoleDto
{
    /// <summary>
    /// Name of the role
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the role
    /// </summary>
    [MaxLength(200)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Permissions associated with this role (JSON array of permission strings)
    /// </summary>
    [MaxLength(2000)]
    public string? Permissions
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this role is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
}

/// <summary>
/// DTO for tenant role response
/// </summary>
public class TenantRoleResponseDto
{
    /// <summary>
    /// Unique identifier for the role
    /// </summary>
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the Tenant entity
    /// </summary>
    public Guid TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Name of the role
    /// </summary>
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the role
    /// </summary>
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Permissions associated with this role
    /// </summary>
    public string? Permissions
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this role is currently active
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
    /// When the role was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the role was last updated
    /// </summary>
    public DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the role was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the role is soft deleted
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>
    /// Associated tenant information
    /// </summary>
    public TenantResponseDto? Tenant
    {
        get;
        set;
    }
}

/// <summary>
/// DTO for user-tenant relationship response
/// </summary>
public class UserTenantResponseDto
{
    /// <summary>
    /// Unique identifier for the user-tenant relationship
    /// </summary>
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the User entity
    /// </summary>
    public Guid UserId
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the Tenant entity
    /// </summary>
    public Guid TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this user-tenant relationship is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    }

    /// <summary>
    /// When the user joined this tenant
    /// </summary>
    public DateTime JoinedAt
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
    /// When the relationship was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the relationship was last updated
    /// </summary>
    public DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the relationship was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the relationship is soft deleted
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

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

    /// <summary>
    /// Role assignments for this user in this tenant
    /// </summary>
    public ICollection<UserTenantRoleResponseDto> UserTenantRoles
    {
        get;
        set;
    } = new List<UserTenantRoleResponseDto>();
}

/// <summary>
/// DTO for user-tenant-role assignment response
/// </summary>
public class UserTenantRoleResponseDto
{
    /// <summary>
    /// Unique identifier for the role assignment
    /// </summary>
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the UserTenant entity
    /// </summary>
    public Guid UserTenantId
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the TenantRole entity
    /// </summary>
    public Guid TenantRoleId
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this role assignment is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    }

    /// <summary>
    /// When this role was assigned
    /// </summary>
    public DateTime AssignedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When this role assignment expires (optional)
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Check if the role assignment is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

    /// <summary>
    /// Check if the role assignment is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired && !IsDeleted;

    /// <summary>
    /// Version number for optimistic concurrency control
    /// </summary>
    public int Version
    {
        get;
        set;
    }

    /// <summary>
    /// When the assignment was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the assignment was last updated
    /// </summary>
    public DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the assignment was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the assignment is soft deleted
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>
    /// Associated tenant role information
    /// </summary>
    public TenantRoleResponseDto? TenantRole
    {
        get;
        set;
    }
}
