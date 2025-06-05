using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Tag.Models;

[Table("tags")]
public class Tag : BaseEntity, ITenantable
{
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public TagType Type { get; set; }
    
    /// <summary>
    /// Hexadecimal color code for UI display
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }
    
    /// <summary>
    /// Icon identifier for UI display
    /// </summary>
    [MaxLength(100)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Whether this tag is available for use
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public int? TenantId { get; set; }
    
    // Navigation properties
    public virtual ICollection<TagRelationship> SourceRelationships { get; set; } = new List<TagRelationship>();
    public virtual ICollection<TagRelationship> TargetRelationships { get; set; } = new List<TagRelationship>();
}
