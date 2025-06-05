using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Certificate.Models;

[Table("user_certificates")]
public class UserCertificate : BaseEntity
{
    
    public int UserId { get; set; }
    public int CertificateId { get; set; }
    
    /// <summary>
    /// Program associated with this certificate issuance (null for non-program certificates)
    /// </summary>
    public int? ProgramId { get; set; }
    
    /// <summary>
    /// Product associated with this certificate issuance (null for non-product certificates)
    /// </summary>
    public int? ProductId { get; set; }
    
    /// <summary>
    /// Program user record for program-based certificates
    /// </summary>
    public int? ProgramUserId { get; set; }
    
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
