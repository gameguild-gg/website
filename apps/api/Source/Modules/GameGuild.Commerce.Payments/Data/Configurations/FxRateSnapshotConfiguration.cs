using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for <see cref="FxRateSnapshot"/> (issue #412 spec B).
/// </summary>
public class FxRateSnapshotConfiguration : IEntityTypeConfiguration<FxRateSnapshot>
{
    public void Configure(EntityTypeBuilder<FxRateSnapshot> builder)
    {
        builder.ToTable("fx_rate_snapshots", tb =>
        {
            tb.HasCheckConstraint("CK_FxRateSnapshot_Rate_Positive", "\"Rate\" > 0");
            tb.HasCheckConstraint("CK_FxRateSnapshot_Pair_Distinct", "\"BaseCurrency\" <> \"QuoteCurrency\"");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BaseCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.QuoteCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Rate)
            .HasColumnType("decimal(28,12)")
            .IsRequired();

        builder.Property(x => x.ProviderName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(50);

        // Idempotent re-sync of the same provider/date: one row per (pair, date, provider, source).
        builder.HasIndex(x => new { x.BaseCurrency, x.QuoteCurrency, x.EffectiveDateUtc, x.ProviderName, x.Source })
            .IsUnique()
            .HasDatabaseName("ix_fx_rate_snapshots_pair_date_provider");

        builder.HasIndex(x => new { x.BaseCurrency, x.QuoteCurrency, x.EffectiveDateUtc })
            .HasDatabaseName("ix_fx_rate_snapshots_pair_date");

        builder.HasIndex(x => x.EffectiveDateUtc)
            .HasDatabaseName("ix_fx_rate_snapshots_effective_date");
    }
}
