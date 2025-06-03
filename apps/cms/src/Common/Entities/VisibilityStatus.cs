namespace cms.Common.Entities;

/// <summary>
/// Enumeration of visibility statuses for resources
/// </summary>
public enum VisibilityStatus
{
    /// <summary>
    /// Resource is private and only visible to owner and explicitly granted users
    /// </summary>
    Private = 0,

    /// <summary>
    /// Resource is publicly visible to all users
    /// </summary>
    Public = 1,

    /// <summary>
    /// Resource is restricted and only visible to users with specific permissions
    /// </summary>
    Restricted = 2,

    /// <summary>
    /// Resource is archived and not normally visible
    /// </summary>
    Archived = 3,

    /// <summary>
    /// Resource is in draft status
    /// </summary>
    Draft = 4
}
