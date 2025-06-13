namespace GameGuild.Common.Entities;

/// <summary>
/// Enumeration of visibility statuses for resources
/// </summary>
public enum Visibility
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
    /// Resource is unlisted and only accessible via direct link
    /// </summary>
    Unlisted = 5,

    /// <summary>
    /// Resource is protected and requires special access
    /// </summary>
    Protected = 6
}
