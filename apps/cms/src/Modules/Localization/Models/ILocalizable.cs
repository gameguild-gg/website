namespace GameGuild.Common.Entities;

/// <summary>
/// Interface for entities that can be localized
/// </summary>
public interface ILocalizable
{
    /// <summary>
    /// Collection of localizations for this entity
    /// </summary>
    ICollection<ResourceLocalization> Localizations
    {
        get;
    }

    /// <summary>
    /// Adds a localization for a specific field in the given language
    /// </summary>
    ResourceLocalization AddLocalization(string fieldName, string content, Language language, LocalizationStatus status = LocalizationStatus.Draft);
}
