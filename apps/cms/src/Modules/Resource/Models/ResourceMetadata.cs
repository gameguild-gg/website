using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Common.Entities;

/// <summary>
/// Entity for storing additional metadata about resources
/// Provides extensible metadata storage for resources
/// </summary>
[Table("ResourceMetadata")]
[Index(nameof(ResourceType))]
public class ResourceMetadata : BaseEntity
{
    /// <summary>
    /// The type of resource this metadata belongs to
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Additional metadata stored as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? AdditionalData
    {
        get;
        set;
    }

    /// <summary>
    /// Tags associated with this resource
    /// </summary>
    [MaxLength(500)]
    public string? Tags
    {
        get;
        set;
    }

    /// <summary>
    /// SEO metadata like meta description, keywords, etc.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? SeoMetadata
    {
        get;
        set;
    }
}
