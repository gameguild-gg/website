using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for AccountCreditInvoiceSettlement (issue #410 v1).
/// </summary>
public class AccountCreditInvoiceSettlementConfiguration : IEntityTypeConfiguration<AccountCreditInvoiceSettlement>
{
    public void Configure(EntityTypeBuilder<AccountCreditInvoiceSettlement> builder)
    {
        builder.ToTable(tb => tb.HasCheckConstraint(
            "ck_account_credit_invoice_settlements_amount_positive",
            "\"Amount\" > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.AppliedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.InvoiceId });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
    }
}
