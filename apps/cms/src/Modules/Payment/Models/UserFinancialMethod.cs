using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Payment.Models;

[Table("user_financial_methods")]
public class UserFinancialMethod : BaseEntity
{
   
    
    public int UserId { get; set; }
    
    public PaymentMethodType Type { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// External ID from payment provider (Stripe, PayPal, etc.)
    /// </summary>
    [MaxLength(255)]
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// Last 4 digits of card number or identifier for the method
    /// </summary>
    [MaxLength(10)]
    public string? LastFour { get; set; }
    
    /// <summary>
    /// Expiration month for cards (MM format)
    /// </summary>
    [MaxLength(2)]
    public string? ExpiryMonth { get; set; }
    
    /// <summary>
    /// Expiration year for cards (YYYY format)
    /// </summary>
    [MaxLength(4)]
    public string? ExpiryYear { get; set; }
    
    /// <summary>
    /// Card brand (Visa, Mastercard, etc.) or wallet type
    /// </summary>
    [MaxLength(50)]
    public string? Brand { get; set; }
    
    public PaymentMethodStatus Status { get; set; } = PaymentMethodStatus.Active;
    
    /// <summary>
    /// Whether this is the default payment method for the user
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    // Navigation properties
    public virtual User.Models.User User { get; set; } = null!;
}
