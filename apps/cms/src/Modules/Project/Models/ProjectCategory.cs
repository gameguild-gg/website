using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Project.Models;

/// <summary>
/// Represents a project category (game, tool, art, etc.)
/// </summary>
public class ProjectCategory : ResourceBase
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Projects in this category
    /// </summary>
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}
