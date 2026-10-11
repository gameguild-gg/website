using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class SessionMfaEvidenceConfiguration : IEntityTypeConfiguration<SessionMfaEvidence>
{
    public void Configure(EntityTypeBuilder<SessionMfaEvidence> builder)
    {
        builder.ToTable("session_mfa_evidence", "gameguild.authentication", table =>
        {
            table.HasCheckConstraint("ck_session_mfa_version", "token_version > 0");
            table.HasCheckConstraint("ck_session_mfa_policy", "policy_fingerprint ~ '^[a-f0-9]{64}$'");
            table.HasCheckConstraint("ck_session_mfa_time", "verified_at >= first_factor_verified_at AND verified_at <= first_factor_verified_at + INTERVAL '5 minutes'");
            table.HasCheckConstraint("ck_session_mfa_method", "method IN ('Totp', 'BackupCode', 'WebAuthn')");
        });
        builder.HasKey(row => row.SessionId);
        builder.Property(row => row.SessionId).HasColumnName("session_id").ValueGeneratedNever();
        builder.Property(row => row.ChallengeId).HasColumnName("challenge_id");
        builder.Property(row => row.SubjectId).HasColumnName("subject_id");
        builder.Property(row => row.TenantId).HasColumnName("tenant_id");
        builder.Property(row => row.TokenVersion).HasColumnName("token_version");
        builder.Property(row => row.PolicyFingerprint).HasColumnName("policy_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(row => row.FirstFactor).HasColumnName("first_factor").HasConversion<string>().HasMaxLength(32);
        builder.Property(row => row.FirstFactorVerifiedAt).HasColumnName("first_factor_verified_at");
        builder.Property(row => row.VerifiedAt).HasColumnName("verified_at");
        builder.Property(row => row.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(row => row.ChallengeId).IsUnique().HasDatabaseName("ux_session_mfa_evidence_challenge");
        builder.HasOne<UserSession>().WithOne().HasForeignKey<SessionMfaEvidence>(row => row.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
