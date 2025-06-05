using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for publishing and distributing content
/// </summary>
[Flags]
public enum PublishingPermissionType
{
    None = 0,
    Publish = 1,             // Make content publicly visible
    Unpublish = 2,           // Remove from public view
    Schedule = 4,            // Schedule future publication
    Reschedule = 8,          // Modify scheduled publication
    Distribute = 16,         // Push to external channels
    Syndicate = 32,          // Syndicate to other platforms
    RSS = 64,                // Include in RSS feeds
    Newsletter = 128,        // Include in newsletters
    SocialMedia = 256,       // Post to social media
    API = 512,               // Make available via API
    
    // Convenience combinations
    BasicPublishing = Publish | Unpublish | Schedule,
    ExternalDistribution = Distribute | Syndicate | SocialMedia,
    AutomatedDistribution = RSS | Newsletter | API,
    All = Publish | Unpublish | Schedule | Reschedule | Distribute | Syndicate | RSS | Newsletter | SocialMedia | API
}
