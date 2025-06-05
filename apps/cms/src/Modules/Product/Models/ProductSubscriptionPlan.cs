using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Product.Models;

/// <summary>
/// Entity representing subscription plans for products
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
[Table("product_subscription_plans")]
public class ProductSubscriptionPlan : BaseEntity
{
    /// <summary>
    /// Foreign key to the Product entity
    /// </summary>
    [Required]
    public Guid ProductId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the Product entity
    /// </summary>
    [ForeignKey(nameof(ProductId))]
    public virtual Product Product
    {
        get;
        set;
    } = null!;
    
    /// <summary>
    /// Name of the subscription plan
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name
    {
        get;
        set;
    } = string.Empty;
    
    /// <summary>
    /// Description of what's included in this plan
    /// </summary>
    [MaxLength(1000)]
    public string? Description
    {
        get;
        set;
    }
    
    /// <summary>
    /// Price for each billing cycle
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price
    {
        get;
        set;
    }
    
    /// <summary>
    /// Currency code for the price
    /// </summary>
    [MaxLength(3)]
    public string Currency
    {
        get;
        set;
    } = "USD";
    
    /// <summary>
    /// How often the subscription is billed
    /// </summary>
    public SubscriptionBillingInterval BillingInterval
    {
        get;
        set;
    }
    
    /// <summary>
    /// Number of billing intervals between charges (e.g., 3 months = interval_count: 3, billing_interval: Month)
    /// </summary>
    public int IntervalCount
    {
        get;
        set;
    } = 1;
    
    /// <summary>
    /// Free trial period in days
    /// </summary>
    public int? TrialPeriodDays
    {
        get;
        set;
    }
    
    /// <summary>
    /// Whether this plan is currently available for new subscriptions
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
    
    /// <summary>
    /// Whether this is the default plan for the product
    /// </summary>
    public bool IsDefault
    {
        get;
        set;
    } = false;

    /// <summary>
    /// Default constructor
    /// </summary>
    public ProductSubscriptionPlan() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial product subscription plan data</param>
    public ProductSubscriptionPlan(object partial) : base(partial) { }
    public virtual ICollection<Subscription.Models.UserSubscription> UserSubscriptions { get; set; } = new List<Subscription.Models.UserSubscription>();
}
