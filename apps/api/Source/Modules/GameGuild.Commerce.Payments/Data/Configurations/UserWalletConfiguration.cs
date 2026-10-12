using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for UserWallet
/// </summary>
public class UserWalletConfiguration : IEntityTypeConfiguration<UserWallet>
{
    public void Configure(EntityTypeBuilder<UserWallet> builder)
    {
        // Configure table with constraints for EF Core 9.0
        builder.ToTable(tb =>
            {
                tb.HasCheckConstraint("CK_UserWallet_UserId_NotEmpty", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                tb.HasCheckConstraint("CK_UserWallet_Balance_NonNegative", "\"Balance\" >= 0");
            }
        );

        // Primary key configuration
        builder.HasKey(x => x.Id);

        // Essential property configurations
        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        builder.Property(x => x.Balance).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(x => x.IsActive).IsRequired();

        // Configure indexes for performance. Issue #406: uniqueness moved from UserId alone to
        // (UserId, Currency) so a user can hold one wallet per currency concurrently.
        builder.HasIndex(x => new { x.UserId, x.Currency }).IsUnique();
        builder.HasIndex(x => x.Currency);
        builder.HasIndex(x => x.IsActive);
    }
}
