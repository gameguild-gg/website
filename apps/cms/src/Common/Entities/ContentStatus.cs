namespace cms.Common.Entities;

/// <summary>
/// Enumeration of status values for content items
/// </summary>
public enum ContentStatus
{
    /// <summary>
    /// Content is in draft state and not visible to the public
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Content is under review and not yet published
    /// </summary>
    UnderReview = 1,

    /// <summary>
    /// Content is published and visible to users
    /// </summary>
    Published = 2,

    /// <summary>
    /// Content is archived and not normally visible
    /// </summary>
    Archived = 3
}
