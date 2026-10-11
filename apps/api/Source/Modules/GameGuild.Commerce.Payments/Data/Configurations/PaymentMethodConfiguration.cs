using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     EF Core configuration for the tenant-scoped payment-method registry (issue #414).
/// </summary>
public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.HasKey(paymentMethod => paymentMethod.Id);

        builder.Property(paymentMethod => paymentMethod.TenantId).IsRequired();
        builder.Property(paymentMethod => paymentMethod.UserId).IsRequired();
        builder.Property(paymentMethod => paymentMethod.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(paymentMethod => paymentMethod.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(paymentMethod => paymentMethod.IsDefault).IsRequired();
        builder.Property(paymentMethod => paymentMethod.CardBrand).HasMaxLength(50);
        builder.Property(paymentMethod => paymentMethod.CardLast4).HasMaxLength(4);
        builder.Property(paymentMethod => paymentMethod.CardFingerprint).HasMaxLength(255);
        builder.Property(paymentMethod => paymentMethod.Nickname).HasMaxLength(100);
        builder.Property(paymentMethod => paymentMethod.BillingAddress).HasMaxLength(1000);

        // Duplicate-token guard: one live local row per gateway payment method per tenant.
        builder.HasIndex(paymentMethod => new { paymentMethod.TenantId, paymentMethod.GatewayProvider, paymentMethod.GatewayPaymentMethodId })
            .IsUnique()
            .HasDatabaseName("ix_payment_methods_tenant_gateway_unique")
            .HasFilter("\"DeletedAt\" IS NULL");

        // Single-default invariant per tenant + user among non-deleted rows (atomic DB backstop).
        builder.HasIndex(paymentMethod => new { paymentMethod.TenantId, paymentMethod.UserId })
            .IsUnique()
            .HasDatabaseName("ix_payment_methods_single_default")
            .HasFilter("\"IsDefault\" AND \"DeletedAt\" IS NULL");
    }
}
