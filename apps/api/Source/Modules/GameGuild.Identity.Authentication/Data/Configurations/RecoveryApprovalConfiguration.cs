using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class RecoveryApprovalConfiguration : IEntityTypeConfiguration<RecoveryApproval>
{
    public void Configure(EntityTypeBuilder<RecoveryApproval> builder)
    {
        builder.ToTable("recovery_approvals", "gameguild.authentication", table =>
        {
            table.HasCheckConstraint("ck_recovery_approvals_factor", "factor IN ('TrustedContact', 'Passkey')");
            table.HasCheckConstraint("ck_recovery_approvals_factor_target", """
                (factor = 'Passkey' AND webauthn_credential_id IS NOT NULL AND approval_code_hash IS NULL AND trusted_contact_id IS NULL)
                OR (factor = 'TrustedContact' AND trusted_contact_id IS NOT NULL AND approval_code_hash IS NOT NULL AND webauthn_credential_id IS NULL)
                """);
        });
        builder.HasKey(approval => approval.Id);
        builder.Property(approval => approval.Id).HasColumnName("id");
        builder.Property(approval => approval.RecoveryRequestId).HasColumnName("recovery_request_id").IsRequired();
        builder.Property(approval => approval.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(approval => approval.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(approval => approval.Factor).HasColumnName("factor").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(approval => approval.TrustedContactId).HasColumnName("trusted_contact_id");
        builder.Property(approval => approval.ApprovalCodeHash).HasColumnName("approval_code_hash").HasMaxLength(64);
        builder.Property(approval => approval.WebAuthnCredentialId).HasColumnName("webauthn_credential_id");
        builder.Property(approval => approval.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(approval => approval.ApprovedAt).HasColumnName("approved_at");
        builder.Property(approval => approval.ApproverIpAddress).HasColumnName("approver_ip_address").HasMaxLength(45);
        builder.Property(approval => approval.ApproverUserAgent).HasColumnName("approver_user_agent").HasMaxLength(500);
        builder.HasIndex(approval => approval.RecoveryRequestId).HasDatabaseName("ix_recovery_approvals_request");
        builder.HasIndex(approval => new { approval.RecoveryRequestId, approval.TrustedContactId })
            .IsUnique()
            .HasFilter("trusted_contact_id IS NOT NULL")
            .HasDatabaseName("ux_recovery_approvals_request_contact");
        builder.HasIndex(approval => new { approval.RecoveryRequestId, approval.WebAuthnCredentialId })
            .IsUnique()
            .HasFilter("webauthn_credential_id IS NOT NULL")
            .HasDatabaseName("ux_recovery_approvals_request_credential");
        builder.HasIndex(approval => approval.TenantId).HasDatabaseName("ix_recovery_approvals_tenant");
    }
}
