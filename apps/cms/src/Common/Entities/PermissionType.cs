using System;

namespace cms.Common.Entities;

/// <summary>
/// Bitwise permission flags for fine-grained access control
/// </summary>
[Flags]
public enum PermissionType
{
    /// <summary>
    /// No permissions granted
    /// </summary>
    None = 0,

    /// <summary>
    /// Read-only access to resources
    /// </summary>
    Read = 1,

    /// <summary>
    /// Create new resources
    /// </summary>
    Create = 2,

    /// <summary>
    /// Update existing resources
    /// </summary>
    Update = 4,

    /// <summary>
    /// Delete resources (permission-based access control)
    /// </summary>
    Delete = 8,

    /// <summary>
    /// Moderate content (approve, reject, moderate discussions)
    /// </summary>
    Moderate = 16,

    /// <summary>
    /// Share content with other users or make it public
    /// </summary>
    Share = 32,

    /// <summary>
    /// Comment on content
    /// </summary>
    Comment = 64,

    /// <summary>
    /// Vote on content (like, dislike, rate)
    /// </summary>
    Vote = 128,

    /// <summary>
    /// Archive content (hide from normal view but preserve)
    /// </summary>
    Archive = 256,

    /// <summary>
    /// Publish content (make it publicly visible)
    /// </summary>
    Publish = 512
    // Add more permissions as needed
}
