using System.ComponentModel.DataAnnotations;

namespace cms.Modules.Tenant.GraphQL;

/// <summary>
/// Input type for creating a new tenant
/// </summary>
public class CreateTenantInput
{
    /// <summary>
    /// Name of the tenant
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the tenant
    /// </summary>
    [StringLength(500)]
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
/// Input type for updating an existing tenant
/// </summary>
public class UpdateTenantInput
{
    /// <summary>
    /// ID of the tenant to update
    /// </summary>
    [Required]
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Name of the tenant
    /// </summary>
    [StringLength(100)]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>
    /// Description of the tenant
    /// </summary>
    [StringLength(500)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tenant is currently active
    /// </summary>
    public bool? IsActive
    {
        get;
        set;
    }
}

/// <summary>
/// Input type for creating a new tenant role
/// </summary>
public class CreateTenantRoleInput
{
    /// <summary>
    /// ID of the tenant this role belongs to
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
    [StringLength(50)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the role
    /// </summary>
    [StringLength(200)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Permissions associated with this role (JSON array of permission strings)
    /// </summary>
    [StringLength(2000)]
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
/// Input type for updating an existing tenant role
/// </summary>
public class UpdateTenantRoleInput
{
    /// <summary>
    /// ID of the tenant role to update
    /// </summary>
    [Required]
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Name of the role
    /// </summary>
    [StringLength(50)]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>
    /// Description of the role
    /// </summary>
    [StringLength(200)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Permissions associated with this role (JSON array of permission strings)
    /// </summary>
    [StringLength(2000)]
    public string? Permissions
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this role is currently active
    /// </summary>
    public bool? IsActive
    {
        get;
        set;
    }
}

/// <summary>
/// Input type for assigning a role to a user within a tenant
/// </summary>
public class AssignUserTenantRoleInput
{
    /// <summary>
    /// ID of the user
    /// </summary>
    [Required]
    public Guid UserId
    {
        get;
        set;
    }

    /// <summary>
    /// ID of the tenant
    /// </summary>
    [Required]
    public Guid TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// ID of the tenant role
    /// </summary>
    [Required]
    public Guid TenantRoleId
    {
        get;
        set;
    }

    /// <summary>
    /// Optional expiration date for the role assignment
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }
}

/// <summary>
/// Input type for adding a user to a tenant
/// </summary>
public class AddUserToTenantInput
{
    /// <summary>
    /// ID of the tenant
    /// </summary>
    [Required]
    public Guid TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// ID of the user
    /// </summary>
    [Required]
    public Guid UserId
    {
        get;
        set;
    }
}

/// <summary>
/// Input type for removing a user from a tenant
/// </summary>
public class RemoveUserFromTenantInput
{
    /// <summary>
    /// ID of the tenant
    /// </summary>
    [Required]
    public Guid TenantId
    {
        get;
        set;
    }

    /// <summary>
    /// ID of the user
    /// </summary>
    [Required]
    public Guid UserId
    {
        get;
        set;
    }
}
