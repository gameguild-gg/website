using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class AccountRecoveryRequestConfiguration : IEntityTypeConfiguration<AccountRecoveryRequest>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryRequest> builder)
    {
        builder.ToTable("account_recovery_requests", "gameguild.authentication", table =>
        {
            table.HasCheckConstraint("ck_account_recovery_status", "status IN ('PendingApprovals', 'ResetUnlocked', 'Completed', 'Cancelled', 'Expired')");
            table.HasCheckConstraint("ck_account_recovery_cooldown", "cooldown_ends_at >= requested_at");
            table.HasCheckConstraint("ck_account_recovery_window", "approval_window_ends_at >= cooldown_ends_at");
            table.HasCheckConstraint("ck_account_recovery_required_approvals", "required_approvals >= 1");
            table.HasCheckConstraint("ck_account_recovery_reset_token", "(reset_token_hash IS NULL OR reset_token_hash ~ '^[a-f0-9]{64}$')");
        });
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id");
        builder.Property(request => request.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(request => request.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(request => request.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(request => request.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(request => request.CooldownEndsAt).HasColumnName("cooldown_ends_at").IsRequired();
        builder.Property(request => request.ApprovalWindowEndsAt).HasColumnName("approval_window_ends_at").IsRequired();
        builder.Property(request => request.RequiredApprovals).HasColumnName("required_approvals").IsRequired();
        builder.Property(request => request.ResetTokenHash).HasColumnName("reset_token_hash").HasMaxLength(64);
        builder.Property(request => request.ResetTokenExpiresAt).HasColumnName("reset_token_expires_at");
        builder.Property(request => request.ResetUnlockedAt).HasColumnName("reset_unlocked_at");
        builder.Property(request => request.CompletedAt).HasColumnName("completed_at");
        builder.Property(request => request.CancelledAt).HasColumnName("cancelled_at");
        builder.HasIndex(request => request.OwnerUserId)
            .IsUnique()
            .HasFilter("status IN ('PendingApprovals', 'ResetUnlocked')")
            .HasDatabaseName("ux_account_recovery_requests_active_owner");
        builder.HasIndex(request => new { request.TenantId, request.Status }).HasDatabaseName("ix_account_recovery_requests_tenant_status");
    }
}
