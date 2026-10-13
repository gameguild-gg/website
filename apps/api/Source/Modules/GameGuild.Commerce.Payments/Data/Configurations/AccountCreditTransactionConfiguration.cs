using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for AccountCreditTransaction (issue #410 v1).
/// </summary>
public class AccountCreditTransactionConfiguration : IEntityTypeConfiguration<AccountCreditTransaction>
{
    public void Configure(EntityTypeBuilder<AccountCreditTransaction> builder)
    {
        builder.ToTable(tb => tb.HasCheckConstraint(
            "ck_account_credit_transactions_amount_positive",
            "\"Amount\" > 0 AND \"RemainingAfter\" >= 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.GrantId).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.RemainingAfter).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Type).IsRequired();

        builder.HasIndex(x => x.GrantId);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
        builder.HasIndex(x => x.ReferenceId);
        builder.HasIndex(x => x.IdempotencyKey);
    }
}
