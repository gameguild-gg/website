using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     EF Core configuration for the <see cref="Refund"/> entity (issue #419): one row per
///     refund with a retry-safe idempotency key unique per payment.
/// </summary>
public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");
        builder.HasKey(refund => refund.Id);
        builder.Property(refund => refund.Amount).HasColumnType("decimal(18,2)");
        builder.Property(refund => refund.Kind).IsRequired();
        builder.Property(refund => refund.Currency).HasMaxLength(3).IsRequired();
        builder.Property(refund => refund.Reason).HasMaxLength(100).IsRequired();
        builder.Property(refund => refund.ReasonDetails).HasMaxLength(1000);
        builder.Property(refund => refund.IdempotencyKey).HasMaxLength(255).IsRequired();
        builder.Property(refund => refund.DecisionNotes).HasMaxLength(1000);
        builder.Property(refund => refund.GatewayRefundId).HasMaxLength(255);
        builder.Property(refund => refund.GatewayName).HasMaxLength(50);
        builder.Property(refund => refund.FailureReason).HasMaxLength(1000);

        builder.HasIndex(refund => new { refund.PaymentId, refund.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ix_refunds_payment_idempotency_key");
        builder.HasIndex(refund => refund.PaymentId).HasDatabaseName("ix_refunds_payment_id");
        builder.HasIndex(refund => refund.Status).HasDatabaseName("ix_refunds_status");
        builder.HasIndex(refund => new { refund.TenantId, refund.Status }).HasDatabaseName("ix_refunds_tenant_status");
        builder.HasIndex(refund => refund.CreatedAt).HasDatabaseName("ix_refunds_created_at");

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(refund => refund.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
