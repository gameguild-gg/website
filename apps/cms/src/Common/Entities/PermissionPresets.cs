namespace cms.Common.Entities;

/// <summary>
/// Helper class for common permission combinations using the existing PermissionType enum
/// Provides preset combinations for typical user roles without introducing role complexity
/// </summary>
public static class PermissionPresets
{
    // todo: this presets does not map the idea we are aiming for. Probably a default permission set for each table, tenant, resource, etc would be better.
    /// <summary>
    /// Full administrative permissions - can do everything
    /// </summary>
    public static readonly PermissionType Admin = 
        PermissionType.Read | PermissionType.Create | PermissionType.Update | PermissionType.Delete | 
        PermissionType.Moderate | PermissionType.Share | PermissionType.Archive | PermissionType.Publish;

    /// <summary>
    /// Editor permissions - can create, read, update and publish content
    /// </summary>
    public static readonly PermissionType Editor = 
        PermissionType.Read | PermissionType.Create | PermissionType.Update | 
        PermissionType.Comment | PermissionType.Publish | PermissionType.Share;

    /// <summary>
    /// Moderator permissions - can moderate, delete and archive content
    /// </summary>
    public static readonly PermissionType Moderator = 
        PermissionType.Read | PermissionType.Moderate | PermissionType.Delete | 
        PermissionType.Archive | PermissionType.Comment;

    /// <summary>
    /// Author permissions - can create and update their own content
    /// </summary>
    public static readonly PermissionType Author = 
        PermissionType.Read | PermissionType.Create | PermissionType.Update | 
        PermissionType.Comment | PermissionType.Share;

    /// <summary>
    /// Viewer permissions - basic read access with social features
    /// </summary>
    public static readonly PermissionType Viewer = 
        PermissionType.Read | PermissionType.Comment | PermissionType.Vote;

    /// <summary>
    /// All possible permissions - equivalent to super admin
    /// </summary>
    public static readonly PermissionType All = 
        (PermissionType)1023; // Binary: 1111111111 (all 10 permissions)

    /// <summary>
    /// No permissions - used for denied access
    /// </summary>
    public static readonly PermissionType None = 0;
}
