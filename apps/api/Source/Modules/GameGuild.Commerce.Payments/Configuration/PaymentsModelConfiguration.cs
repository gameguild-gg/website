using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     EF Core model configuration for the Payments module (tax-related and revenue
///     auditing entities). Additional payment entities are currently disabled.
/// </summary>
public sealed class PaymentsModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(builder =>
        {
            builder.ToTable("payments", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_payments_provider_mapping_complete",
                    """(("ProviderEnvironment" IS NULL AND "ProviderAccountId" IS NULL AND "ProviderObjectId" IS NULL AND "ProviderObjectType" IS NULL AND "ProviderMonetaryLeg" IS NULL) OR ("ProviderEnvironment" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderObjectId" IS NOT NULL AND "ProviderObjectType" IS NOT NULL AND "ProviderMonetaryLeg" IS NOT NULL))""");
                tableBuilder.HasCheckConstraint(
                    "ck_payments_provider_environment",
                    "\"ProviderEnvironment\" IS NULL OR \"ProviderEnvironment\" IN ('test', 'live')");
                tableBuilder.HasCheckConstraint(
                    "ck_payments_stripe_value_mapping_required",
                    """(lower("Provider") <> 'stripe' OR "Status" NOT IN (1, 2, 5, 6, 7) OR ("ProviderEnvironment" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderObjectId" IS NOT NULL AND "ProviderObjectType" IS NOT NULL AND "ProviderMonetaryLeg" IS NOT NULL))""");
            });

            builder.Property(payment => payment.ProviderEnvironment)
                .HasMaxLength(32);
            builder.Property(payment => payment.ProviderAccountId)
                .HasMaxLength(255);
            builder.Property(payment => payment.ProviderObjectId)
                .HasMaxLength(255);
            builder.Property(payment => payment.ProviderObjectType)
                .HasMaxLength(100);
            builder.Property(payment => payment.ProviderMonetaryLeg)
                .HasMaxLength(100);

            builder.HasIndex(payment => new
                {
                    payment.Provider,
                    payment.ProviderEnvironment,
                    payment.ProviderAccountId,
                    payment.ProviderObjectId,
                    payment.ProviderMonetaryLeg
                })
                .HasFilter("\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"ProviderObjectId\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL")
                .IsUnique()
                .IsCreatedConcurrently()
                .HasDatabaseName("ix_payments_provider_object_leg");
        });
        modelBuilder.ApplyConfiguration(new TaxJurisdictionConfiguration());
        modelBuilder.ApplyConfiguration(new TaxRateConfiguration());
        modelBuilder.ApplyConfiguration(new TaxRuleConfiguration());
        modelBuilder.ApplyConfiguration(new UserWalletConfiguration());

        // Revenue auditing (issue #404): the audited revenue events themselves plus the
        // reconciliation runs, their discrepancies and the anomaly alerts.
        modelBuilder.Entity<RevenueEvent>(builder =>
        {
            builder.Property(revenueEvent => revenueEvent.EventType).HasConversion<string>().IsRequired();
            builder.Property(revenueEvent => revenueEvent.Source).HasConversion<string>().IsRequired();
            builder.Property(revenueEvent => revenueEvent.Status).HasConversion<string>().IsRequired();
            // FinancialLedgerEntry is not part of the mapped model; the optional ledger
            // link is preserved as the LedgerEntryId column instead of a navigation.
            builder.Ignore(revenueEvent => revenueEvent.LedgerEntry);
        });
        modelBuilder.Entity<RevenueReconciliationRun>(builder =>
        {
            builder.Property(run => run.Status).HasConversion<string>().IsRequired();
        });
        modelBuilder.Entity<RevenueReconciliationDiscrepancy>(builder =>
        {
            builder.Property(discrepancy => discrepancy.Kind).HasConversion<string>().IsRequired();
            builder.HasOne(discrepancy => discrepancy.Run)
                .WithMany(run => run.Discrepancies)
                .HasForeignKey(discrepancy => discrepancy.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RevenueAnomalyAlert>(builder =>
        {
            builder.Property(alert => alert.Kind).HasConversion<string>().IsRequired();
            builder.Property(alert => alert.Status).HasConversion<string>().IsRequired();
        });
    }
}
