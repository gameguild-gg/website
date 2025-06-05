using System;

namespace cms.Common.Entities;

/// <summary>
/// Legacy permission flags maintained for backward compatibility only.
/// 
/// For new implementations, use the modular permission system:
/// - CorePermissionType for basic CRUD operations
/// - ContentInteractionPermissionType for user interactions
/// - ModerationPermissionType for moderation activities
/// - ContentLifecyclePermissionType for lifecycle management
/// - PublishingPermissionType for publishing operations
/// - MonetizationPermissionType for business operations
/// - EditorialPermissionType for editorial oversight
/// - PromotionPermissionType for content promotion
/// - QualityControlPermissionType for quality management
/// - UnifiedPermissionContext for comprehensive permission handling
/// 
/// Use PermissionMigrationService to migrate existing implementations.
/// </summary>
[Flags]
[Obsolete("Use modular permission system with UnifiedPermissionContext. Use PermissionMigrationService for migration.")]
public enum PermissionType
{
    // CORE CRUD OPERATIONS - migrate to CorePermissionType for new implementations
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

    // Convenience combinations for backward compatibility
    ReadWrite = Read | Create | Update,
    FullAccess = Read | Create | Update | Delete
}
