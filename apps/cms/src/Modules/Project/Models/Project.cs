using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;
using cms.Modules.User.Models;

namespace cms.Modules.Project.Models;

/// <summary>
/// Represents a project (game, tool, art, etc.)
/// </summary>
public class Project : Content
{
    /// <summary>
    /// Short description (max 500 chars)
    /// </summary>
    [MaxLength(500)]
    public string? ShortDescription
    {
        get;
        set;
    }

    /// <summary>
    /// Project category (entity)
    /// </summary>
    [Required]
    public virtual ProjectCategory Category
    {
        get;
        set;
    } = null!;

    public Guid CategoryId
    {
        get;
        set;
    }

    /// <summary>
    /// Website URL
    /// </summary>
    [MaxLength(500)]
    public string? WebsiteUrl
    {
        get;
        set;
    }

    /// <summary>
    /// Repository URL
    /// </summary>
    [MaxLength(500)]
    public string? RepositoryUrl
    {
        get;
        set;
    }

    /// <summary>
    /// Social links (JSON string)
    /// </summary>
    public string? SocialLinks
    {
        get;
        set;
    }

    /// <summary>
    /// Project metadata and statistics
    /// </summary>
    public virtual ProjectMetadata? ProjectMetadata
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to project versions
    /// </summary>
    public virtual ICollection<ProjectVersion> Versions
    {
        get;
        set;
    } = new List<ProjectVersion>();

    /// <summary>
    /// User who created the project
    /// </summary>
    [Required]
    public virtual cms.Modules.User.Models.User CreatedBy
    {
        get;
        set;
    } = null!;

    public Guid CreatedById
    {
        get;
        set;
    }
}
