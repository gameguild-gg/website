using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class SignInMfaChallengeConfiguration : IEntityTypeConfiguration<SignInMfaChallenge>
{
    public void Configure(EntityTypeBuilder<SignInMfaChallenge> builder)
    {
        builder.ToTable("sign_in_mfa_challenges", "gameguild.authentication", table =>
        {
            table.HasCheckConstraint("ck_sign_in_mfa_subject_version", "subject_token_version > 0");
            table.HasCheckConstraint("ck_sign_in_mfa_lifetime", "expires_at > created_at AND expires_at <= created_at + INTERVAL '5 minutes'");
            table.HasCheckConstraint("ck_sign_in_mfa_token_hash", "token_hash ~ '^[a-f0-9]{64}$'");
            table.HasCheckConstraint("ck_sign_in_mfa_policy_hash", "policy_fingerprint ~ '^[a-f0-9]{64}$'");
            table.HasCheckConstraint("ck_sign_in_mfa_enrollment_binding", """
                (enrollment_configuration_id IS NULL AND enrollment_secret_fingerprint IS NULL AND enrollment_initialized_at IS NULL)
                OR (purpose = 'EnrollFactor' AND enrollment_configuration_id IS NOT NULL
                    AND enrollment_configuration_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND enrollment_secret_fingerprint IS NOT NULL AND enrollment_secret_fingerprint ~ '^[a-f0-9]{64}$'
                    AND enrollment_initialized_at IS NOT NULL AND enrollment_initialized_at >= created_at AND enrollment_initialized_at < expires_at)
                """);
        });
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(challenge => challenge.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(challenge => challenge.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(challenge => challenge.SubjectTokenVersion).HasColumnName("subject_token_version").IsRequired();
        builder.Property(challenge => challenge.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(challenge => challenge.PolicyFingerprint).HasColumnName("policy_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(challenge => challenge.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(challenge => challenge.FirstFactor).HasColumnName("first_factor").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(challenge => challenge.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(challenge => challenge.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(challenge => challenge.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(challenge => challenge.RevokedAt).HasColumnName("revoked_at");
        builder.Property(challenge => challenge.VerificationMethod).HasColumnName("verification_method").HasConversion<string>().HasMaxLength(32);
        builder.Property(challenge => challenge.EnrollmentConfigurationId).HasColumnName("enrollment_configuration_id");
        builder.Property(challenge => challenge.EnrollmentSecretFingerprint).HasColumnName("enrollment_secret_fingerprint").HasMaxLength(64);
        builder.Property(challenge => challenge.EnrollmentInitializedAt).HasColumnName("enrollment_initialized_at");
        builder.HasIndex(challenge => challenge.TokenHash).IsUnique().HasDatabaseName("ux_sign_in_mfa_challenges_token_hash");
        builder.HasIndex(challenge => new { challenge.SubjectId, challenge.ExpiresAt }).HasDatabaseName("ix_sign_in_mfa_challenges_subject_expiry");
    }
}
