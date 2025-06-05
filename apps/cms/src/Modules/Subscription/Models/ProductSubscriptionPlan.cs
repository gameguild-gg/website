using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Subscription.Models;

[Table("product_subscription_plans")]
public class ProductSubscriptionPlan : BaseEntity
{
    
    public int ProductId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }
    
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";
    
    public SubscriptionBillingInterval BillingInterval { get; set; }
    
    /// <summary>
    /// Number of billing intervals between charges (e.g., 3 months = interval_count: 3, billing_interval: Month)
    /// </summary>
    public int IntervalCount { get; set; } = 1;
    
    /// <summary>
    /// Free trial period in days
    /// </summary>
    public int? TrialPeriodDays { get; set; }
    
    /// <summary>
    /// Whether this plan is currently available for new subscriptions
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether this is the default plan for the product
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    // Navigation properties
    public virtual Product.Models.Product Product { get; set; } = null!;
    public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}
