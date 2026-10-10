using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for ServiceAccount.
///     Configures database schema for OAuth2 client_credentials service accounts.
/// </summary>
public class ServiceAccountConfiguration : IEntityTypeConfiguration<ServiceAccount>
{
    public void Configure(EntityTypeBuilder<ServiceAccount> builder)
    {
        // Configure table name (snake_case convention)
        builder.ToTable("service_accounts", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure Id property
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();

        // Client ID - unique identifier for OAuth2
        builder.Property(x => x.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.ClientId)
            .IsUnique()
            .HasDatabaseName("idx_service_accounts_client_id");

        // Client Secret Hash - never store plaintext
        builder.Property(x => x.ClientSecretHash)
            .HasColumnName("client_secret_hash")
            .HasMaxLength(256)
            .IsRequired();

        // Service name
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // Description
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        // Scopes (stored as comma-separated string)
        builder.Property(x => x.Scopes)
            .HasColumnName("scopes")
            .HasMaxLength(2000)
            .IsRequired();

        // Tenant relationship
        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("idx_service_accounts_tenant_id");

        // Status flags
        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at");

        // IP restrictions (stored as comma-separated string)
        builder.Property(x => x.AllowedIpAddresses)
            .HasColumnName("allowed_ip_addresses")
            .HasMaxLength(2000);

        // X.509 client-certificate binding (thumbprint + optional SPKI key pin)
        builder.Property(x => x.CertificateThumbprint)
            .HasColumnName("certificate_thumbprint")
            .HasMaxLength(40);

        builder.Property(x => x.CertificateSpkiSha256)
            .HasColumnName("certificate_spki_sha256")
            .HasMaxLength(64);

        builder.HasIndex(x => x.CertificateThumbprint)
            .IsUnique()
            .HasDatabaseName("idx_service_accounts_certificate_thumbprint");

        builder.HasIndex(x => x.CertificateSpkiSha256)
            .IsUnique()
            .HasDatabaseName("idx_service_accounts_certificate_spki_sha256");

        // Lockout management
        builder.Property(x => x.IsLocked)
            .HasColumnName("is_locked")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.LockedAt)
            .HasColumnName("locked_at");

        builder.Property(x => x.FailedAuthenticationAttempts)
            .HasColumnName("failed_authentication_attempts")
            .IsRequired()
            .HasDefaultValue(0);

        // Authentication tracking
        builder.Property(x => x.LastAuthenticatedAt)
            .HasColumnName("last_authenticated_at");

        builder.Property(x => x.LastAuthenticatedFromIp)
            .HasColumnName("last_authenticated_from_ip")
            .HasMaxLength(45); // IPv6 max length

        builder.Property(x => x.AuthenticationCount)
            .HasColumnName("authentication_count")
            .IsRequired()
            .HasDefaultValue(0L);

        // Secret rotation tracking
        builder.Property(x => x.SecretRotatedAt)
            .HasColumnName("secret_rotated_at");

        builder.Property(x => x.SecretRotationCount)
            .HasColumnName("secret_rotation_count")
            .IsRequired()
            .HasDefaultValue(0);

        // Audit fields
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Composite index for common queries
        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("idx_service_accounts_tenant_active");

        // Ignore computed properties
        builder.Ignore(x => x.CanAuthenticate);
    }
}
