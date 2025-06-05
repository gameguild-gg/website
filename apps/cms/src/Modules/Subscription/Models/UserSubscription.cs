using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Subscription.Models;

[Table("user_subscriptions")]
public class UserSubscription : BaseEntity
{
    
    public int UserId { get; set; }
    public int SubscriptionPlanId { get; set; }
    
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    
    /// <summary>
    /// External subscription ID from payment provider (Stripe, PayPal, etc.)
    /// </summary>
    [MaxLength(255)]
    public string? ExternalSubscriptionId { get; set; }
    
    /// <summary>
    /// Current billing period start date
    /// </summary>
    public DateTime CurrentPeriodStart { get; set; }
    
    /// <summary>
    /// Current billing period end date
    /// </summary>
    public DateTime CurrentPeriodEnd { get; set; }
    
    /// <summary>
    /// Date when the subscription was canceled (null if not canceled)
    /// </summary>
    public DateTime? CanceledAt { get; set; }
    
    /// <summary>
    /// Date when the subscription will end (null if indefinite)
    /// </summary>
    public DateTime? EndsAt { get; set; }
    
    /// <summary>
    /// Date when the trial period ends (null if no trial)
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }
    
    /// <summary>
    /// Last successful payment date
    /// </summary>
    public DateTime? LastPaymentAt { get; set; }
    
    /// <summary>
    /// Next scheduled billing date
    /// </summary>
    public DateTime? NextBillingAt { get; set; }
    
    // Navigation properties
    public virtual User.Models.User User { get; set; } = null!;
    public virtual ProductSubscriptionPlan SubscriptionPlan { get; set; } = null!;
    public virtual ICollection<Product.Models.UserProduct> UserProducts { get; set; } = new List<Product.Models.UserProduct>();
}
