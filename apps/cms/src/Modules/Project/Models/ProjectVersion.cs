using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;
using cms.Modules.User.Models;

namespace cms.Modules.Project.Models;

/// <summary>
/// Represents a version/release of a project
/// </summary>
public class ProjectVersion : BaseEntity
{
    /// <summary>
    /// The project this version belongs to
    /// </summary>
    [Required]
    public virtual Project Project
    {
        get;
        set;
    } = null!;

    public Guid ProjectId
    {
        get;
        set;
    }

    /// <summary>
    /// Version number (e.g., "1.0.0", "alpha-0.1")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string VersionNumber
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Release notes
    /// </summary>
    public string? ReleaseNotes
    {
        get;
        set;
    }

    /// <summary>
    /// Status (enum as string)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status
    {
        get;
        set;
    } = "draft";

    /// <summary>
    /// Download count
    /// </summary>
    public int DownloadCount
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// User who created this version
    /// </summary>
    [Required]
    public virtual User.Models.User CreatedBy
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
