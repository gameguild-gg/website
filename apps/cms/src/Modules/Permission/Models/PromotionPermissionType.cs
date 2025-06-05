using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for promoting and featuring content
/// </summary>
[Flags]
public enum PromotionPermissionType
{
    None = 0,
    Feature = 1,             // Feature on homepage
    Pin = 2,                 // Pin to top of categories
    Trending = 4,            // Add to trending sections
    Recommend = 8,           // Add to recommendations
    Spotlight = 16,          // Highlight in spotlight
    Banner = 32,             // Display as banner content
    Carousel = 64,           // Add to carousel displays
    Widget = 128,            // Feature in widgets
    Email = 256,             // Include in email campaigns
    Push = 512,              // Send push notifications
    
    // Convenience combinations
    BasicPromotion = Feature | Pin | Recommend,
    VisualPromotion = Banner | Carousel | Spotlight,
    ActivePromotion = Email | Push | Trending,
    All = Feature | Pin | Trending | Recommend | Spotlight | Banner | Carousel | Widget | Email | Push
}
