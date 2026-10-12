using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Entity Type Configuration for <see cref="SubscriptionPlanIntervalPrice" /> (issue #392).
///     One price point per (plan, interval); the unique index is the configuration guard.
/// </summary>
public class SubscriptionPlanIntervalPriceConfiguration : IEntityTypeConfiguration<SubscriptionPlanIntervalPrice>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlanIntervalPrice> builder)
    {
        builder.ToTable("SubscriptionPlanIntervalPrices");

        builder.HasKey(price => price.Id);

        builder.Property(price => price.PlanId)
            .IsRequired();

        builder.Property(price => price.Interval)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(price => price.PriceInCents)
            .IsRequired();

        // One price point per plan interval: upserts converge on this constraint.
        builder.HasIndex(price => new { price.PlanId, price.Interval })
            .IsUnique()
            .HasDatabaseName("ix_subscription_plan_interval_prices_plan_interval");

        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(price => price.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(price => price.PlanId)
            .HasDatabaseName("ix_subscription_plan_interval_prices_plan_id");
    }
}
