using System.ComponentModel.DataAnnotations;

namespace cms.Common.Entities;

/// <summary>
/// Entity for storing additional metadata about resources
/// Provides extensible metadata storage for resources
/// </summary>
public class ResourceMetadata : BaseEntity
{
    /// <summary>
    /// The type of resource this metadata belongs to
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata stored as JSON
    /// </summary>
    public string? AdditionalData { get; set; }

    /// <summary>
    /// Tags associated with this resource
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// SEO metadata like meta description, keywords, etc.
    /// </summary>
    public string? SeoMetadata { get; set; }

    /// <summary>
    /// Collection of permissions for resources using this metadata
    /// </summary>
    public virtual ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
}
