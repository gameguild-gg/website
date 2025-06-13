using System.ComponentModel.DataAnnotations;

namespace GameGuild.Modules.Tenant.GraphQL;

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
