using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Resources;

public sealed class QuotaLedgerEntryConfiguration : IEntityTypeConfiguration<QuotaLedgerEntry>
{
    public void Configure(EntityTypeBuilder<QuotaLedgerEntry> builder)
    {
        builder.ToTable("quota_ledger_entries", "resources");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.TenantId).IsRequired();
        builder.Property(entry => entry.EventId).IsRequired();
        builder.Property(entry => entry.ResourceType).IsRequired();
        builder.Property(entry => entry.Delta).IsRequired();
        builder.Property(entry => entry.OperationCode).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.AggregateType).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.AggregateId).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.TimestampUtc).IsRequired();
        builder.HasIndex(entry => entry.EventId).IsUnique();
        builder.HasIndex(entry => new { entry.TenantId, entry.ResourceType, entry.TimestampUtc });
    }
}
