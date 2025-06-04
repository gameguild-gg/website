namespace cms.Common.Entities;

/// <summary>
/// Enumeration of localization statuses
/// </summary>
public enum LocalizationStatus
{
    /// <summary>
    /// Localization is in draft status
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Localization is published and active
    /// </summary>
    Published = 1,

    /// <summary>
    /// Localization needs review
    /// </summary>
    NeedsReview = 2,

    /// <summary>
    /// Localization is archived
    /// </summary>
    Archived = 3,

    /// <summary>
    /// Machine-translated content, may need human review
    /// </summary>
    MachineTrans = 4
}
