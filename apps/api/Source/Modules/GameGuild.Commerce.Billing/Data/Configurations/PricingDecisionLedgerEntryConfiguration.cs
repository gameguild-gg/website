using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Billing.Data.Configurations;

/// <summary>
///     Entity Type Configuration for <see cref="PricingDecisionLedgerEntry"/> (issue #400).
///     The ledger is append-only: no updates are mapped beyond the drift-validation stamp,
///     and the unique idempotency index is the replay guard.
/// </summary>
public class PricingDecisionLedgerEntryConfiguration : IEntityTypeConfiguration<PricingDecisionLedgerEntry>
{
    public void Configure(EntityTypeBuilder<PricingDecisionLedgerEntry> builder)
    {
        builder.ToTable("pricing_decision_ledger");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.DecisionType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entry => entry.Currency)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("USD");

        builder.Property(entry => entry.BaseAmount).HasPrecision(18, 4);
        builder.Property(entry => entry.RuleAdjustmentTotal).HasPrecision(18, 4);
        builder.Property(entry => entry.PromoDiscountTotal).HasPrecision(18, 4);
        builder.Property(entry => entry.FinalAmount).HasPrecision(18, 4);
        builder.Property(entry => entry.RecomputedFinalAmount).HasPrecision(18, 4);

        builder.Property(entry => entry.BreakdownJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(entry => entry.AppliedPromoCodes).HasMaxLength(512);
        builder.Property(entry => entry.AppliedStackingRuleName).HasMaxLength(200);
        builder.Property(entry => entry.PricingEngineVersion).HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.ActorIpAddress).HasMaxLength(45);
        builder.Property(entry => entry.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.DriftStatus).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.DriftReason).HasMaxLength(500);

        // Replay guard: one durable row per calculation key.
        builder.HasIndex(entry => entry.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_pricing_decision_ledger_idempotency_key");

        // Read-path filters: tenant, order, subscription, promo code, date, drift.
        builder.HasIndex(entry => entry.TenantId)
            .HasDatabaseName("ix_pricing_decision_ledger_tenant_id");

        builder.HasIndex(entry => entry.OrderId)
            .HasDatabaseName("ix_pricing_decision_ledger_order_id");

        builder.HasIndex(entry => entry.SubscriptionId)
            .HasDatabaseName("ix_pricing_decision_ledger_subscription_id");

        builder.HasIndex(entry => entry.CreatedAt)
            .HasDatabaseName("ix_pricing_decision_ledger_created_at");

        builder.HasIndex(entry => new { entry.TenantId, entry.CreatedAt })
            .HasDatabaseName("ix_pricing_decision_ledger_tenant_created_at");

        builder.HasIndex(entry => new { entry.DriftStatus, entry.CreatedAt })
            .HasDatabaseName("ix_pricing_decision_ledger_drift_status_created_at");
    }
}
