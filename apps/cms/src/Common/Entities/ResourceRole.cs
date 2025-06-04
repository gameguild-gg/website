using System.ComponentModel.DataAnnotations;

namespace cms.Common.Entities;

// todo: delete this! use three level pure permission dac 
/// <summary>
/// Entity representing roles that can be assigned to resources
/// Provides role-based access control for resources
/// </summary>
public class ResourceRole : BaseEntity
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
    /// Description of what this role provides
    /// </summary>
    [MaxLength(200)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Default permission level for this role
    /// </summary>
    [Required]
    public PermissionLevel DefaultPermission
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

    /// <summary>
    /// Collection of permissions using this role
    /// </summary>
    public virtual ICollection<ResourcePermission> ResourcePermissions
    {
        get;
        set;
    } = new List<ResourcePermission>();
}
