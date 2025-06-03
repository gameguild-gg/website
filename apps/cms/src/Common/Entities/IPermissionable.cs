namespace cms.Common.Entities;

/// <summary>
/// Interface for entities that can have permissions assigned
/// </summary>
public interface IPermissionable
{
    /// <summary>
    /// Collection of permissions assigned to this entity
    /// </summary>
    ICollection<ResourcePermission> ResourcePermissions { get; }

    /// <summary>
    /// Grants permission to a user for this entity
    /// </summary>
    ResourcePermission GrantPermission(Modules.User.Models.User user, PermissionLevel permission);
}
