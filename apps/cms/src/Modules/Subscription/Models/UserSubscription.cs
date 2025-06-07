using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Subscription.Models;

[Table("user_subscriptions")]
[Index(nameof(UserId))]
[Index(nameof(Status))]
[Index(nameof(SubscriptionPlanId))]
[Index(nameof(CurrentPeriodStart))]
[Index(nameof(CurrentPeriodEnd))]
[Index(nameof(NextBillingAt))]
[Index(nameof(ExternalSubscriptionId))]
public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    
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
    [ForeignKey(nameof(UserId))]
    public virtual User.Models.User User { get; set; } = null!;
    
    [ForeignKey(nameof(SubscriptionPlanId))]
    public virtual Product.Models.ProductSubscriptionPlan SubscriptionPlan { get; set; } = null!;
    
    public virtual ICollection<Product.Models.UserProduct> UserProducts { get; set; } = new List<Product.Models.UserProduct>();
}

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        // Configure relationship with User (can't be done with annotations)
        builder.HasOne(us => us.User)
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with SubscriptionPlan (can't be done with annotations)
        builder.HasOne(us => us.SubscriptionPlan)
            .WithMany()
            .HasForeignKey(us => us.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
