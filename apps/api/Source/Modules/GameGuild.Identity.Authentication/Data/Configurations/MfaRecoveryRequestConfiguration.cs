using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class MfaRecoveryRequestConfiguration : IEntityTypeConfiguration<MfaRecoveryRequest>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryRequest> builder)
    {
        builder.ToTable("mfa_recovery_requests", "gameguild.authentication", table =>
        {
            table.HasCheckConstraint("ck_mfa_recovery_cooldown", "cooldown_ends_at >= requested_at");
            table.HasCheckConstraint("ck_mfa_recovery_status", "status IN ('Pending', 'AwaitingConfirmation', 'Completed', 'Cancelled', 'Expired')");
            table.HasCheckConstraint("ck_mfa_recovery_tokens", """
                (request_token_hash IS NULL AND confirmation_token_hash IS NULL AND confirmation_token_expires_at IS NULL)
                OR (request_token_hash ~ '^[a-f0-9]{64}$')
                """);
        });
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id");
        builder.Property(request => request.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(request => request.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(request => request.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(request => request.CooldownEndsAt).HasColumnName("cooldown_ends_at").IsRequired();
        builder.Property(request => request.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(request => request.RequestTokenHash).HasColumnName("request_token_hash").HasMaxLength(64);
        builder.Property(request => request.ConfirmationTokenHash).HasColumnName("confirmation_token_hash").HasMaxLength(64);
        builder.Property(request => request.ConfirmationTokenExpiresAt).HasColumnName("confirmation_token_expires_at");
        builder.Property(request => request.ConsumedAt).HasColumnName("consumed_at");
        builder.HasIndex(request => request.UserId)
            .IsUnique()
            .HasFilter("status IN ('Pending', 'AwaitingConfirmation')")
            .HasDatabaseName("ux_mfa_recovery_requests_active_user");
        builder.HasIndex(request => new { request.TenantId, request.Status }).HasDatabaseName("ix_mfa_recovery_requests_tenant_status");
    }
}
