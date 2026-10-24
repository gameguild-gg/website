using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for <see cref="FxRateSyncRun"/> (issue #412 spec B).
/// </summary>
public class FxRateSyncRunConfiguration : IEntityTypeConfiguration<FxRateSyncRun>
{
    public void Configure(EntityTypeBuilder<FxRateSyncRun> builder)
    {
        builder.ToTable("fx_rate_sync_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PrimaryProviderName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(x => x.RawResponse)
            .IsRequired();

        builder.Property(x => x.RawResponseContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.StartedAtUtc)
            .HasDatabaseName("ix_fx_rate_sync_runs_started_at");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_fx_rate_sync_runs_status");
    }
}
