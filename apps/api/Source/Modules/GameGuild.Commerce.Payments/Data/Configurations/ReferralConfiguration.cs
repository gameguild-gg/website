using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for Referral (issue #410 v1).
/// </summary>
public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable(tb => tb.HasCheckConstraint(
            "ck_referrals_referrer_not_empty",
            "\"ReferrerUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReferrerUserId).IsRequired();
        builder.Property(x => x.ReferralCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReferralLink).HasMaxLength(500);
        builder.Property(x => x.RefereeEmail).HasMaxLength(255);
        builder.Property(x => x.ReferrerIpHash).HasMaxLength(64);
        builder.Property(x => x.RefereeIpHash).HasMaxLength(64);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.FraudReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.ReferrerUserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ReferralCode });
        builder.HasIndex(x => new { x.TenantId, x.RefereeUserId });
        builder.HasIndex(x => new { x.Status, x.ReferrerIpHash });
    }
}
