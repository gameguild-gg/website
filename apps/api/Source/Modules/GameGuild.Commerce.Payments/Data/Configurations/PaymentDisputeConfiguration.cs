using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for PaymentDispute
/// </summary>
public class PaymentDisputeConfiguration : IEntityTypeConfiguration<PaymentDispute>
{
    public void Configure(EntityTypeBuilder<PaymentDispute> builder)
    {
        // Configure table with constraints for EF Core 9.0
        builder.ToTable(tb =>
            {
                tb.HasCheckConstraint("CK_PaymentDispute_PaymentId_NotEmpty", "payment_id != '00000000-0000-0000-0000-000000000000'");
                tb.HasCheckConstraint("CK_PaymentDispute_UserId_NotEmpty", "user_id != '00000000-0000-0000-0000-000000000000'");
                tb.HasCheckConstraint("CK_PaymentDispute_Amount_Positive", "amount > 0");
            }
        );

        // Primary key configuration
        builder.HasKey(x => x.Id);

        // Property configurations based on entity attributes
        builder.Property(x => x.PaymentId).IsRequired();

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Type).HasConversion<string>().IsRequired();

        builder.Property(x => x.Status).HasConversion<string>().IsRequired();

        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(x => x.DisputeFee).HasColumnType("decimal(18,2)").IsRequired();

        // Configure indexes for performance
        builder.HasIndex(x => x.PaymentId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.DueDate);
    }
}
