using System.ComponentModel.DataAnnotations;

namespace cms.Common.Entities;

/// <summary>
/// Entity for storing localized content for resources
/// Provides multi-language support for resources
/// </summary>
/// todo: field should be the one coming from the resource as a generic origin, and not a plain string. revist the others entries too, such as resource type.
public class ResourceLocalization : BaseEntity
{
    // todo: apply polymorphism
    /// <summary>
    /// Type of the resource being localized
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Navigation property to the language
    /// Entity Framework will automatically create the LanguageId foreign key
    /// </summary>
    [Required]
    public virtual Language Language
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// The field name being localized (e.g., 'Title', 'Description', 'Content')
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FieldName
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The localized content
    /// </summary>
    [Required]
    public string Content
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Whether this localization is the default for the language
    /// </summary>
    public bool IsDefault
    {
        get;
        set;
    } = false;

    /// <summary>
    /// Status of the localization (Draft, Published, NeedsReview, etc.)
    /// </summary>
    public LocalizationStatus Status
    {
        get;
        set;
    } = LocalizationStatus.Draft;
}
