using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for UserMfaConfiguration
/// </summary>
public class UserMfaConfigurationConfiguration : IEntityTypeConfiguration<UserMfaConfiguration>
{
    public void Configure(EntityTypeBuilder<UserMfaConfiguration> builder)
    {
        // Configure table name (snake_case convention)
        builder.ToTable("user_mfa_configuration", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure Id property
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        // Configure UserId property
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        // Configure IsEnabled property
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").IsRequired().IsConcurrencyToken();

        // Configure TotpSecretKey property
        builder.Property(x => x.TotpSecretKey).HasColumnName("totp_secret_key").HasMaxLength(500).IsRequired(false).IsConcurrencyToken();

        // Configure BackupCodes property
        builder.Property(x => x.BackupCodes).HasColumnName("backup_codes").IsRequired(false).IsConcurrencyToken();

        // Configure EnabledAt property
        builder.Property(x => x.EnabledAt).HasColumnName("enabled_at").IsRequired(false);

        // Configure LastUsedAt property
        builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").IsRequired(false);

        // Configure FailedAttempts property
        builder.Property(x => x.FailedAttempts).HasColumnName("failed_attempts").IsRequired().IsConcurrencyToken();

        // Configure LockedOutUntil property
        builder.Property(x => x.LockedOutUntil).HasColumnName("locked_out_until").IsRequired(false).IsConcurrencyToken();

        // Configure PreferredMethod property (enum)
        builder.Property(x => x.PreferredMethod).HasColumnName("preferred_method").HasConversion<string>().IsRequired();

        // Configure QrCodeSetupData property
        builder.Property(x => x.QrCodeSetupData).HasColumnName("qr_code_setup_data").IsRequired(false);

        // Configure IsSetupComplete property
        builder.Property(x => x.IsSetupComplete).HasColumnName("is_setup_complete").IsRequired().IsConcurrencyToken();

        builder.Property(x => x.SetupExpiresAt).HasColumnName("setup_expires_at").IsConcurrencyToken();

        // Configure CreatedAt property
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // Configure UpdatedAt property
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Configure indexes
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ix_user_mfa_configuration_user_id");
    }
}
