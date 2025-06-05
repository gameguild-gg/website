using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for managing content lifecycle states
/// </summary>
[Flags]
public enum ContentLifecyclePermissionType
{
    None = 0,
    Draft = 1,               // Manage draft content
    Submit = 2,              // Submit for review
    Withdraw = 4,            // Withdraw from review
    Archive = 8,             // Archive content (remove from feeds, keep searchable)
    Restore = 16,            // Restore archived content
    SoftDelete = 32,         // Soft delete (hidden but recoverable, audit trail)
    HardDelete = 64,         // Permanently delete content
    Backup = 128,            // Create content backups
    Migrate = 256,           // Migrate content between systems
    Clone = 512,             // Clone/duplicate content
    
    // Convenience combinations
    BasicLifecycle = Draft | Submit | Withdraw,
    AdvancedLifecycle = Archive | Restore | Clone,
    DeletionControl = SoftDelete | HardDelete,
    All = Draft | Submit | Withdraw | Archive | Restore | SoftDelete | HardDelete | Backup | Migrate | Clone
}
