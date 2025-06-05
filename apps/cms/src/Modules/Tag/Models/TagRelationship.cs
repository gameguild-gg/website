using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Tag.Models;

[Table("tag_relationships")]
public class TagRelationship : BaseEntity
{
    
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    
    public TagRelationshipType Type { get; set; }
    
    /// <summary>
    /// Weight or strength of the relationship (optional)
    /// </summary>
    [Column(TypeName = "decimal(3,2)")]
    public decimal? Weight { get; set; }
    
    /// <summary>
    /// Additional metadata about the relationship
    /// </summary>
    [MaxLength(500)]
    public string? Metadata { get; set; }
    
    // Navigation properties
    public virtual Tag Source { get; set; } = null!;
    public virtual Tag Target { get; set; } = null!;
}
