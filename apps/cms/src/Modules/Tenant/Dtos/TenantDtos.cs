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
    /// Users in this tenant
    /// </summary>
    public ICollection<UserTenantResponseDto> UserTenants
    {
        get;
        set;
    } = new List<UserTenantResponseDto>();
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

