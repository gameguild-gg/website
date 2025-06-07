using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Certificate.Models;

[Table("certificate_tags")]
[Index(nameof(CertificateId), nameof(TagId), IsUnique = true)]
[Index(nameof(CertificateId))]
[Index(nameof(TagId))]
[Index(nameof(RelationshipType))]
public class CertificateTag : BaseEntity
{
    public Guid CertificateId { get; set; }
    public Guid TagId { get; set; }
    
    public CertificateTagRelationshipType RelationshipType { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(CertificateId))]
    public virtual Certificate Certificate { get; set; } = null!;
    
    [ForeignKey(nameof(TagId))]
    public virtual Tag.Models.TagProficiency Tag { get; set; } = null!;
}

public class CertificateTagConfiguration : IEntityTypeConfiguration<CertificateTag>
{
    public void Configure(EntityTypeBuilder<CertificateTag> builder)
    {
        // Configure relationship with Certificate (can't be done with annotations)
        builder.HasOne(ct => ct.Certificate)
            .WithMany()
            .HasForeignKey(ct => ct.CertificateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with Tag (can't be done with annotations)
        builder.HasOne(ct => ct.Tag)
            .WithMany()
            .HasForeignKey(ct => ct.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
