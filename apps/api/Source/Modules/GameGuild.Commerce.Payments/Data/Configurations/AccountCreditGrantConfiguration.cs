using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for AccountCreditGrant (issue #410 v1).
/// </summary>
public class AccountCreditGrantConfiguration : IEntityTypeConfiguration<AccountCreditGrant>
{
    public void Configure(EntityTypeBuilder<AccountCreditGrant> builder)
    {
        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint(
                "ck_account_credit_grants_user_not_empty",
                "\"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            tb.HasCheckConstraint(
                "ck_account_credit_grants_amounts_positive",
                "\"Amount\" > 0 AND \"RemainingAmount\" >= 0 AND \"RemainingAmount\" <= \"Amount\"");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RevocationReason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.RemainingAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Source).IsRequired();
        builder.Property(x => x.CreditType).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => new { x.Source, x.SourceReferenceId });

        builder.HasMany<AccountCreditTransaction>()
            .WithOne()
            .HasForeignKey(transaction => transaction.GrantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
