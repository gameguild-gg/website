using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Tag.Models;

[Table("tag_proficiencies")]
[Index(nameof(Name))]
[Index(nameof(Type))]
[Index(nameof(ProficiencyLevel))]
[Index(nameof(IsActive))]
public class TagProficiency : BaseEntity
{
[Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public TagType Type { get; set; }
    
    public SkillProficiencyLevel ProficiencyLevel { get; set; }
    
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
    /// Whether this tag proficiency is available for use
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<Certificate.Models.CertificateTag> CertificateTags { get; set; } = new List<Certificate.Models.CertificateTag>();
}
