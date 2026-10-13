using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for GiftSubscription (issue #410 v1).
/// </summary>
public class GiftSubscriptionConfiguration : IEntityTypeConfiguration<GiftSubscription>
{
    public void Configure(EntityTypeBuilder<GiftSubscription> builder)
    {
        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint(
                "ck_gift_subscriptions_giver_not_empty",
                "\"GiverUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            tb.HasCheckConstraint(
                "ck_gift_subscriptions_amount_positive",
                "\"Amount\" > 0");
            tb.HasCheckConstraint(
                "ck_gift_subscriptions_duration_range",
                "\"DurationMonths\" >= 1 AND \"DurationMonths\" <= 36");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.GiverUserId).IsRequired();
        builder.Property(x => x.RecipientEmail).HasMaxLength(255).IsRequired();
        builder.Property(x => x.RecipientName).HasMaxLength(255);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.PersonalMessage).HasMaxLength(500);
        builder.Property(x => x.RedemptionCodeHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RedemptionCodePrefix).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.GiverUserId });
        builder.HasIndex(x => new { x.TenantId, x.RecipientUserId });
        builder.HasIndex(x => x.RedemptionCodeHash).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        builder.HasIndex(x => x.RecipientEmail);
    }
}
