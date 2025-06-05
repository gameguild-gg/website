using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for curating and organizing content
/// </summary>
[Flags]
public enum ContentCurationPermissionType
{
    None = 0,
    Categorize = 1,          // Change content categories/tags
    Tag = 2,                 // Add/remove tags
    Collection = 4,          // Add to collections/playlists
    Series = 8,              // Organize into content series
    CrossReference = 16,     // Link related content
    Translate = 32,          // Manage translations
    Version = 64,            // Manage content versions
    Template = 128,          // Create/apply templates
    
    // Convenience combinations
    BasicCuration = Categorize | Tag | Collection,
    AdvancedCuration = Series | CrossReference | Version | Template,
    All = Categorize | Tag | Collection | Series | CrossReference | Translate | Version | Template
}
