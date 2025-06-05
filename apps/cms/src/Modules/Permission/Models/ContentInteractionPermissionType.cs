using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for user interactions with content
/// </summary>
[Flags]
public enum ContentInteractionPermissionType
{
    None = 0,
    Comment = 1,
    Vote = 2,
    Share = 4,
    Report = 8,
    Follow = 16,
    Bookmark = 32,
    React = 64,              // Like, love, laugh reactions
    Subscribe = 128,         // Subscribe to content updates
    Mention = 256,           // Mention other users
    Tag = 512,               // Tag other users in content
    
    // Convenience combinations
    BasicInteraction = Comment | Vote | Share,
    SocialInteraction = React | Follow | Mention | Tag,
    All = Comment | Vote | Share | Report | Follow | Bookmark | React | Subscribe | Mention | Tag
}
