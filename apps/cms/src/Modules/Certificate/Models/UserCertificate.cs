using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Certificate.Models;

[Table("user_certificates")]
[Index(nameof(UserId))]
[Index(nameof(CertificateId))]
[Index(nameof(ProgramId))]
[Index(nameof(ProductId))]
[Index(nameof(ProgramUserId))]
[Index(nameof(VerificationCode), IsUnique = true)]
[Index(nameof(Status))]
[Index(nameof(IssuedAt))]
public class UserCertificate : BaseEntity
{
    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    
    [Required]
    [ForeignKey(nameof(Certificate))]
    public Guid CertificateId { get; set; }
    
    /// <summary>
    /// Program associated with this certificate issuance (null for non-program certificates)
    /// </summary>
    public Guid? ProgramId { get; set; }
    
    /// <summary>
    /// Product associated with this certificate issuance (null for non-product certificates)
    /// </summary>
    public Guid? ProductId { get; set; }
    
    /// <summary>
    /// Program user record for program-based certificates
    /// </summary>
    public Guid? ProgramUserId { get; set; }
    
    /// <summary>
    /// Unique verification code for this certificate
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string VerificationCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Final grade or score achieved for this certificate
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? FinalGrade { get; set; }
    
    /// <summary>
    /// Additional metadata about the certificate issuance
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }
    
    public CertificateStatus Status { get; set; } = CertificateStatus.Active;
    
    /// <summary>
    /// Date when the certificate was issued
    /// </summary>
    public DateTime IssuedAt { get; set; }
    
    /// <summary>
    /// Date when the certificate expires (null if never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Date when the certificate was revoked (null if not revoked)
    /// </summary>
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// Reason for revocation (null if not revoked)
    /// </summary>
    [MaxLength(500)]
    public string? RevocationReason { get; set; }
    
    // Navigation properties
    public virtual User.Models.User User { get; set; } = null!;
    public virtual Certificate Certificate { get; set; } = null!;
    public virtual Program.Models.Program? Program { get; set; }
    public virtual Product.Models.Product? Product { get; set; }
    public virtual Program.Models.ProgramUser? ProgramUser { get; set; }
    public virtual ICollection<CertificateBlockchainAnchor> BlockchainAnchors { get; set; } = new List<CertificateBlockchainAnchor>();
}

public class UserCertificateConfiguration : IEntityTypeConfiguration<UserCertificate>
{
    public void Configure(EntityTypeBuilder<UserCertificate> builder)
    {
        // Configure relationship with Certificate (can't be done with annotations)
        builder.HasOne(uc => uc.Certificate)
            .WithMany()
            .HasForeignKey(uc => uc.CertificateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with User (can't be done with annotations)
        builder.HasOne(uc => uc.User)
            .WithMany()
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure optional relationship with Program (can't be done with annotations)
        builder.HasOne(uc => uc.Program)
            .WithMany()
            .HasForeignKey(uc => uc.ProgramId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure optional relationship with Product (can't be done with annotations)
        builder.HasOne(uc => uc.Product)
            .WithMany()
            .HasForeignKey(uc => uc.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure optional relationship with ProgramUser (can't be done with annotations)
        builder.HasOne(uc => uc.ProgramUser)
            .WithMany(pu => pu.UserCertificates)
            .HasForeignKey(uc => uc.ProgramUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
