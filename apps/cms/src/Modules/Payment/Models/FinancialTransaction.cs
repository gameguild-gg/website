using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Payment.Models;

[Table("financial_transactions")]
public class FinancialTransaction : BaseEntity
{
    
    /// <summary>
    /// User who initiated the transaction (payer)
    /// </summary>
    public Guid? FromUserId { get; set; }
    
    /// <summary>
    /// User who receives the transaction (payee)
    /// </summary>
    public Guid? ToUserId { get; set; }
    
    public TransactionType Type { get; set; }
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }
    
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";
    
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    
    /// <summary>
    /// External transaction ID from payment provider
    /// </summary>
    [MaxLength(255)]
    public string? ExternalTransactionId { get; set; }
    
    /// <summary>
    /// Payment method used for this transaction
    /// </summary>
    public Guid? PaymentMethodId { get; set; }
    
    /// <summary>
    /// Promo code applied to this transaction
    /// </summary>
    public Guid? PromoCodeId { get; set; }
    
    /// <summary>
    /// Platform fee charged for this transaction
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PlatformFee { get; set; }
    
    /// <summary>
    /// Payment processor fee for this transaction
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? ProcessorFee { get; set; }
    
    /// <summary>
    /// Net amount after fees
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? NetAmount { get; set; }
    
    /// <summary>
    /// Description or memo for the transaction
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Additional metadata about the transaction
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }
    
    /// <summary>
    /// Date when the transaction was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>
    /// Date when the transaction failed (if applicable)
    /// </summary>
    public DateTime? FailedAt { get; set; }
    
    /// <summary>
    /// Error message if transaction failed
    /// </summary>
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
    
    // Navigation properties
    public virtual User.Models.User? FromUser { get; set; }
    public virtual User.Models.User? ToUser { get; set; }
    public virtual UserFinancialMethod? PaymentMethod { get; set; }
    public virtual Product.Models.PromoCode? PromoCode { get; set; }
    public virtual ICollection<Product.Models.PromoCodeUse> PromoCodeUses { get; set; } = new List<Product.Models.PromoCodeUse>();
}
