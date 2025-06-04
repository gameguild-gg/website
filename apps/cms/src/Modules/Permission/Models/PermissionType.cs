using System;

namespace cms.Common.Entities;

/// <summary>
/// Bitwise permission flags for fine-grained access control
/// 
/// DEPRECATED: This enum mixes core CRUD operations with business logic permissions.
/// For new implementations, prefer using:
/// - CorePermissionType for basic CRUD operations
/// - BusinessLogicPermissionType for CMS-specific features
/// - ReputationSystem for reputation-based business logic permissions
/// 
/// This enum is maintained for backward compatibility but will be phased out.
/// </summary>
[Flags]
[Obsolete("Use CorePermissionType for CRUD operations and BusinessLogicPermissionType for business logic. This enum will be removed in a future version.")]
public enum PermissionType
{
    // CORE CRUD OPERATIONS - use CorePermissionType for new implementations
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

    // // BUSINESS LOGIC OPERATIONS - use BusinessLogicPermissionType and ReputationSystem for new implementations
    // /// <summary>
    // /// Moderate content (approve, reject, moderate discussions)
    // /// DEPRECATED: Use BusinessLogicPermissionType.Moderate with ReputationSystem
    // /// </summary>
    // [Obsolete("Use BusinessLogicPermissionType.Moderate with ReputationSystem")]
    // Moderate = 16,
    //
    // /// <summary>
    // /// Share content with other users or make it public
    // /// DEPRECATED: Use BusinessLogicPermissionType.Share with ReputationSystem
    // /// </summary>
    // [Obsolete("Use BusinessLogicPermissionType.Share with ReputationSystem")]
    // Share = 32,
    //
    // /// <summary>
    // /// Comment on content
    // /// DEPRECATED: Use BusinessLogicPermissionType.Comment with ReputationSystem
    // /// </summary>
    // [Obsolete("Use BusinessLogicPermissionType.Comment with ReputationSystem")]
    // Comment = 64,
    //
    // /// <summary>
    // /// Vote on content (like, dislike, rate)
    // /// DEPRECATED: Use BusinessLogicPermissionType.Vote with ReputationSystem
    // /// </summary>
    // [Obsolete("Use BusinessLogicPermissionType.Vote with ReputationSystem")]
    // Vote = 128,
    //
    // /// <summary>
    // /// Archive content (hide from normal view but preserve)
    // /// DEPRECATED: Use BusinessLogicPermissionType.Archive with ReputationSystem
    // /// </summary>
    // [Obsolete("Use BusinessLogicPermissionType.Archive with ReputationSystem")]
    // Archive = 256,
    //
    // /// <summary>
    // /// Publish content (make it publicly visible)
    // /// DEPRECATED: Use BusinessLogicPermissionType.Publish with ReputationSystem
    // /// </summary>
    // [Obsolete("Use BusinessLogicPermissionType.Publish with ReputationSystem")]
    // Publish = 512
    // // Add more permissions as needed
}
