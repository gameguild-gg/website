using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Certificate.Models;

[Table("certificates")]
public class Certificate : BaseEntity, ITenantable
{
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public CertificateType Type { get; set; }
    
    /// <summary>
    /// Program that this certificate is associated with (null for non-program certificates)
    /// </summary>
    public int? ProgramId { get; set; }
    
    /// <summary>
    /// Product that this certificate is associated with (null for non-product certificates)
    /// </summary>
    public int? ProductId { get; set; }
    
    /// <summary>
    /// Required completion percentage for program-based certificates (0-100)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionPercentage { get; set; } = 100;
    
    /// <summary>
    /// Minimum grade required for certificate issuance (0-100, null = no minimum)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? MinimumGrade { get; set; }
    
    /// <summary>
    /// Whether feedback submission is required for certificate issuance
    /// </summary>
    public bool RequiresFeedback { get; set; } = false;
    
    /// <summary>
    /// Whether rating submission is required for certificate issuance
    /// </summary>
    public bool RequiresRating { get; set; } = false;
    
    /// <summary>
    /// Minimum rating required if rating is required (1-5, null = any rating accepted)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal? MinimumRating { get; set; }
    
    /// <summary>
    /// How long the certificate remains valid (in days, null = never expires)
    /// </summary>
    public int? ValidityDays { get; set; }
    
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Code;
    
    /// <summary>
    /// Template for certificate design/layout
    /// </summary>
    [MaxLength(500)]
    public string? CertificateTemplate { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public int? TenantId { get; set; }
    
    // Navigation properties
    public virtual Program.Models.Program? Program { get; set; }
    public virtual Product.Models.Product? Product { get; set; }
    public virtual ICollection<UserCertificate> UserCertificates { get; set; } = new List<UserCertificate>();
    public virtual ICollection<CertificateTag> CertificateTags { get; set; } = new List<CertificateTag>();
}
