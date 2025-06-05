using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Certificate.Models;

[Table("certificate_tags")]
public class CertificateTag : BaseEntity
{
    
    public Guid CertificateId { get; set; }
    public Guid TagId { get; set; }
    
    public CertificateTagRelationshipType RelationshipType { get; set; }
    
    // Navigation properties
    public virtual Certificate Certificate { get; set; } = null!;
    public virtual Tag.Models.TagProficiency Tag { get; set; } = null!;
}
