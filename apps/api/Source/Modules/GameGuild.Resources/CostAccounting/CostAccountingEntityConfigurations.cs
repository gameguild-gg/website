using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Resources;

public sealed class InternalUsageLedgerEntryConfiguration : IEntityTypeConfiguration<InternalUsageLedgerEntry>
{
    public void Configure(EntityTypeBuilder<InternalUsageLedgerEntry> builder)
    {
        builder.ToTable("internal_usage_ledger_entries", "resources");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.TenantId).IsRequired();
        builder.Property(entry => entry.EventName).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.MetricCode).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Quantity).HasPrecision(28, 9).IsRequired();
        builder.Property(entry => entry.Unit).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Region).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.AggregateType).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.AggregateId).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.ValuationStatus).HasMaxLength(30).IsRequired();
        builder.HasIndex(entry => new { entry.EventId, entry.MetricCode }).IsUnique();
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAtUtc });
        builder.HasIndex(entry => new { entry.ValuationStatus, entry.OccurredAtUtc });
    }
}

public sealed class CloudPriceRateConfiguration : IEntityTypeConfiguration<CloudPriceRate>
{
    public void Configure(EntityTypeBuilder<CloudPriceRate> builder)
    {
        builder.ToTable("cloud_price_rates", "resources");
        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.Provider).HasMaxLength(80).IsRequired();
        builder.Property(rate => rate.ServiceCode).HasMaxLength(120).IsRequired();
        builder.Property(rate => rate.MetricCode).HasMaxLength(200).IsRequired();
        builder.Property(rate => rate.Region).HasMaxLength(100).IsRequired();
        builder.Property(rate => rate.Unit).HasMaxLength(80).IsRequired();
        builder.Property(rate => rate.SourceCurrency).HasMaxLength(3).IsRequired();
        builder.Property(rate => rate.SourceUnitPrice).HasPrecision(28, 12).IsRequired();
        builder.Property(rate => rate.UsdExchangeRate).HasPrecision(28, 12).IsRequired();
        builder.Property(rate => rate.ProviderRateId).HasMaxLength(300).IsRequired();
        builder.HasIndex(rate => new { rate.Provider, rate.ProviderRateId, rate.EffectiveAtUtc }).IsUnique();
        builder.HasIndex(rate => new { rate.Provider, rate.MetricCode, rate.Region, rate.EffectiveAtUtc });
    }
}

public sealed class InternalCostValuationConfiguration : IEntityTypeConfiguration<InternalCostValuation>
{
    public void Configure(EntityTypeBuilder<InternalCostValuation> builder)
    {
        builder.ToTable("internal_cost_valuations", "resources");
        builder.HasKey(valuation => valuation.Id);
        builder.Property(valuation => valuation.TenantId).IsRequired();
        builder.Property(valuation => valuation.SourceCurrency).HasMaxLength(3).IsRequired();
        builder.Property(valuation => valuation.SourceUnitPrice).HasPrecision(28, 12).IsRequired();
        builder.Property(valuation => valuation.SourceCost).HasPrecision(28, 12).IsRequired();
        builder.Property(valuation => valuation.UsdExchangeRate).HasPrecision(28, 12).IsRequired();
        builder.Property(valuation => valuation.UsdCost).HasPrecision(28, 12).IsRequired();
        builder.HasIndex(valuation => valuation.UsageLedgerEntryId).IsUnique();
        builder.HasIndex(valuation => new { valuation.TenantId, valuation.ValuedAtUtc });
    }
}

public sealed class ActualCloudCostEntryConfiguration : IEntityTypeConfiguration<ActualCloudCostEntry>
{
    public void Configure(EntityTypeBuilder<ActualCloudCostEntry> builder)
    {
        builder.ToTable("actual_cloud_cost_entries", "resources");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.TenantId).IsRequired();
        builder.Property(entry => entry.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Source).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.ExternalLineId).HasMaxLength(300).IsRequired();
        builder.Property(entry => entry.ServiceCode).HasMaxLength(120).IsRequired();
        builder.Property(entry => entry.UsageType).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.ResourceId).HasMaxLength(500);
        builder.Property(entry => entry.SourceCurrency).HasMaxLength(3).IsRequired();
        builder.Property(entry => entry.SourceCost).HasPrecision(28, 12).IsRequired();
        builder.Property(entry => entry.UsdCost).HasPrecision(28, 12).IsRequired();
        builder.Property(entry => entry.IsUsdNormalizationPending).IsRequired();
        builder.HasIndex(entry => new { entry.Provider, entry.Source, entry.ExternalLineId }).IsUnique();
        builder.HasIndex(entry => new { entry.TenantId, entry.BillingPeriodStartUtc });
    }
}

public sealed class SharedCostAllocationEntryConfiguration : IEntityTypeConfiguration<SharedCostAllocationEntry>
{
    public void Configure(EntityTypeBuilder<SharedCostAllocationEntry> builder)
    {
        builder.ToTable("shared_cost_allocation_entries", "resources");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.TenantId).IsRequired();
        builder.Property(entry => entry.DurationMilliseconds).HasPrecision(18, 4).IsRequired();
        builder.Property(entry => entry.DatabaseMilliseconds).HasPrecision(18, 4).IsRequired();
        builder.Property(entry => entry.Method).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.RouteTemplate).HasMaxLength(500).IsRequired();
        builder.HasIndex(entry => entry.RequestEventId).IsUnique();
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAtUtc });
    }
}
