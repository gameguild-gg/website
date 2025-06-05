using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;

namespace cms.Modules.Certificate.Models;

[Table("certificate_blockchain_anchors")]
public class CertificateBlockchainAnchor : BaseEntity
{
    
    public int CertificateId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string BlockchainNetwork { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string TransactionHash { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? BlockHash { get; set; }
    
    public long? BlockNumber { get; set; }
    
    [MaxLength(500)]
    public string? ContractAddress { get; set; }
    
    [MaxLength(100)]
    public string? TokenId { get; set; }
    
    /// <summary>
    /// Status of the blockchain anchoring
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "pending";
    
    /// <summary>
    /// Date when the anchoring was initiated
    /// </summary>
    public DateTime AnchoredAt { get; set; }
    
    /// <summary>
    /// Date when the anchoring was confirmed on-chain
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }
    
    // Navigation properties
    public virtual UserCertificate Certificate { get; set; } = null!;
}
