using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for <see cref="CurrencyConfiguration"/> (issue #412 spec A).
/// </summary>
public class CurrencyConfigurationConfiguration : IEntityTypeConfiguration<CurrencyConfiguration>
{
    public void Configure(EntityTypeBuilder<CurrencyConfiguration> builder)
    {
        builder.ToTable("currency_configurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.DisplaySymbol)
            .HasMaxLength(16);

        // One configuration row per tenant + currency.
        builder.HasIndex(x => new { x.TenantId, x.CurrencyCode })
            .IsUnique()
            .HasDatabaseName("ix_currency_configurations_tenant_currency");

        // At most one base currency per tenant. PostgreSQL partial index; the SQLite/InMemory
        // test providers ignore the filter and keep the plain unique constraint semantics
        // usable by seeding at most one base row per tenant in tests.
        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("\"IsBaseCurrency\" = true")
            .HasDatabaseName("ix_currency_configurations_tenant_base");
    }
}
