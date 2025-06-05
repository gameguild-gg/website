using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Kyc.Models;

[Table("user_kyc_verifications")]
public class UserKycVerification : BaseEntity
{
    
    public Guid UserId { get; set; }
    
    public KycProvider Provider { get; set; }
    
    public KycVerificationStatus Status { get; set; } = KycVerificationStatus.Pending;
    
    /// <summary>
    /// External verification ID from the KYC provider
    /// </summary>
    [MaxLength(255)]
    public string? ExternalVerificationId { get; set; }
    
    /// <summary>
    /// Verification level (basic, enhanced, full)
    /// </summary>
    [MaxLength(50)]
    public string? VerificationLevel { get; set; }
    
    /// <summary>
    /// Document types submitted for verification
    /// </summary>
    [MaxLength(500)]
    public string? DocumentTypes { get; set; }
    
    /// <summary>
    /// Country of the submitted documents
    /// </summary>
    [MaxLength(2)]
    public string? DocumentCountry { get; set; }
    
    /// <summary>
    /// Date when verification was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; }
    
    /// <summary>
    /// Date when verification was completed (approved/rejected)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Date when verification expires and needs renewal
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Reason for rejection or additional notes
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Additional metadata from the KYC provider
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ProviderData { get; set; }
    
    // Navigation properties
    public virtual User.Models.User User { get; set; } = null!;
}
