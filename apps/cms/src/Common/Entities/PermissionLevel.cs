namespace cms.Common.Entities;

/// <summary>
/// Enumeration of permission levels for resources
/// </summary>
public enum PermissionLevel
{
    /// <summary>
    /// Can view/read the resource
    /// </summary>
    Read = 1,

    /// <summary>
    /// Can edit/update the resource
    /// </summary>
    Write = 2,

    /// <summary>
    /// Can delete the resource
    /// </summary>
    Delete = 3,

    /// <summary>
    /// Can manage permissions for the resource
    /// </summary>
    Admin = 4,

    /// <summary>
    /// Full ownership and control over the resource
    /// </summary>
    Owner = 5
}
