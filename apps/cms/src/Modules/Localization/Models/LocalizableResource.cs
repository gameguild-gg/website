namespace GameGuild.Common.Entities;

/// <summary>
/// Base resource that can be localized
/// Provides localization capabilities for resources
/// Mirrors the TypeScript LocalizableResourceBase functionality
/// </summary>
public abstract class LocalizableResource : BaseEntity
{
    /// <summary>
    /// Collection of localizations for this resource
    /// </summary>
    public virtual ICollection<ResourceLocalization> Localizations
    {
        get;
        set;
    } = new List<ResourceLocalization>();

    /// <summary>
    /// Default language code for this resource
    /// </summary>
    public string? DefaultLanguageCode
    {
        get;
        set;
    } = "en-US";

    /// <summary>
    /// Whether this resource supports localization
    /// </summary>
    public bool IsLocalizationEnabled
    {
        get;
        set;
    } = true;
}
