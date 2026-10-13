using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Entity Type Configuration for Subscription
/// </summary>
public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure required properties
        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.PlanId).IsRequired();

        builder.Property(x => x.CreatedByUserId).IsRequired();

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50);

        // Configure billing cycle
        builder.Property(x => x.BillingCycle).IsRequired().HasConversion<string>().HasMaxLength(20);

        // Configure dates
        builder.Property(x => x.StartDate).IsRequired();

        builder.Property(x => x.EndDate);

        builder.Property(x => x.NextBillingDate).IsRequired();

        builder.Property(x => x.CurrentPeriodStart).IsRequired();

        builder.Property(x => x.CurrentPeriodEnd).IsRequired();

        builder.Property(x => x.LastPaymentAt);

        // Configure Amount as owned type (Money value object) 
        builder.OwnsOne(x => x.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount").IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").IsRequired().HasMaxLength(3);
        });

        // Configure trial
        builder.Property(x => x.TrialEndDate);

        // Configure external identifiers
        builder.Property(x => x.ExternalId).HasMaxLength(100);

        builder.Property(x => x.ExternalCustomerId).HasMaxLength(100);

        // Local payment-method registry reference (issue #414): plain indexed column,
        // no cross-module FK; removal safety is enforced by the Payments module.
        builder.Property(x => x.PaymentMethodId);

        builder.HasIndex(x => new { x.TenantId, x.PaymentMethodId })
            .HasDatabaseName("ix_subscriptions_tenant_payment_method");

        // Configure cancellation
        builder.Property(x => x.CancelledAt);

        builder.Property(x => x.CancellationReason).HasConversion<string>();

        builder.Property(x => x.CancellationNote).HasMaxLength(1000);

        // Configure other properties
        builder.Property(x => x.AutoRenew).IsRequired();

        builder.Property(x => x.BillingCycleCount).IsRequired();

        builder.Property(x => x.Metadata).HasMaxLength(2000);

        // Configure optimistic concurrency control via RowVersion
        // Prevents race conditions during payment processing, cancellation, and suspension
        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Configure relationships
        builder.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);

        // Configure indexes
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.NextBillingDate);
        builder.HasIndex(x => x.ExternalId).IsUnique();
    }
}
