namespace cms.Common.Entities;

/// <summary>
/// Base resource that can have permissions
/// Simplified version of ResourceBase focusing only on permission management
/// This class matches your exact specification
/// </summary>
public abstract class PermissionableResource : BaseEntity
{
    /// <summary>
    /// Collection of permissions assigned to this resource
    /// </summary>
    public virtual ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();

    /// <summary>
    /// Collection of roles assigned to this resource
    /// </summary>
    public virtual ICollection<ResourceRole> ResourceRoles { get; set; } = new List<ResourceRole>();
}
