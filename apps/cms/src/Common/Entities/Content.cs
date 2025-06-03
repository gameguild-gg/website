using System.ComponentModel.DataAnnotations;

namespace cms.Common.Entities;

/// <summary>
/// Represents a content item that is a specialized resource.
/// </summary>
public class Content : ResourceBase
{
    // Content-specific properties can be added here if needed

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
}
