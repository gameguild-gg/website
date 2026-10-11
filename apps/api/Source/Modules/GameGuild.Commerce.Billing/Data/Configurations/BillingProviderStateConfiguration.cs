using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Entity Type Configuration for <see cref="BillingProviderState"/>.
/// </summary>
public class BillingProviderStateConfiguration : IEntityTypeConfiguration<BillingProviderState>
{
    public void Configure(EntityTypeBuilder<BillingProviderState> builder)
    {
        builder.ToTable("billing_provider_states");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.LastChangedByUserId);

        // One management row per provider key; the unique index also makes the
        // enable/disable upsert race-safe at the database level.
        builder.HasIndex(x => x.ProviderKey)
            .IsUnique()
            .HasDatabaseName("ix_billing_provider_states_provider_key");
    }
}
