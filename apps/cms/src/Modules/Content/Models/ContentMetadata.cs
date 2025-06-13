using System.ComponentModel.DataAnnotations;

namespace GameGuild.Common.Entities;

/// <summary>
/// Stores statistics and metadata for content.
/// </summary>
public class ContentMetadata
{
    [Key]
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the content
    /// </summary>
    [Required]
    public virtual Content Content
    {
        get;
        set;
    } = null!;

    public Guid ContentId
    {
        get;
        set;
    }

    /// <summary>
    /// Content statistics
    /// </summary>
    public int ViewCount
    {
        get;
        set;
    } = 0;

    public int DownloadCount
    {
        get;
        set;
    } = 0;

    public int LikeCount
    {
        get;
        set;
    } = 0;

    public int ShareCount
    {
        get;
        set;
    } = 0;

    public int CommentCount
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// SEO metadata
    /// </summary>
    [MaxLength(160)]
    public string? MetaDescription
    {
        get;
        set;
    }

    [MaxLength(255)]
    public string? MetaKeywords
    {
        get;
        set;
    }

    /// <summary>
    /// Timestamps for analytics
    /// </summary>
    public DateTime? LastViewedAt
    {
        get;
        set;
    }

    public DateTime? LastInteractionAt
    {
        get;
        set;
    }
}