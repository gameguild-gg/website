using System.ComponentModel.DataAnnotations;

namespace GameGuild.Common.Entities;

/// <summary>
/// Represents a content item that is a specialized resource.
/// </summary>
public abstract class Content : ResourceBase
{
    // Content-specific properties can be added here if needed

    /// <summary>
    /// Licenses associated with this content (many-to-many)
    /// </summary>
    public virtual ICollection<ContentLicense> Licenses
    {
        get;
        set;
    } = new List<ContentLicense>();

    /// <summary>
    /// Slug for the content (URL-friendly unique identifier)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Slug
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Status of the content (draft, published, etc.)
    /// </summary>
    public ContentStatus Status
    {
        get;
        set;
    } = ContentStatus.Draft;
}
