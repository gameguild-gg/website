using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys", "gameguild.authentication");
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).HasColumnName("id").IsRequired();
        builder.Property(key => key.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(key => key.TenantId).HasColumnName("tenant_id");
        builder.Property(key => key.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(key => key.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
        builder.Property(key => key.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(20).IsRequired();
        builder.Property(key => key.Scopes).HasColumnName("scopes").HasMaxLength(1000).IsRequired();
        builder.Property(key => key.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(key => key.ExpiresAt).HasColumnName("expires_at");
        builder.Property(key => key.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(key => key.UsageCount).HasColumnName("usage_count").HasDefaultValue(0L).IsRequired();
        builder.Property(key => key.IpWhitelist).HasColumnName("ip_whitelist").HasMaxLength(100);
        builder.Property(key => key.RevokedAt).HasColumnName("revoked_at");
        builder.Property(key => key.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(200);
        builder.Property(key => key.ReplacesKeyId).HasColumnName("replaces_key_id");
        builder.Property(key => key.RotationGraceEndsAt).HasColumnName("rotation_grace_ends_at");
        builder.Property(key => key.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(key => key.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(key => key.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(key => key.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(key => key.KeyHash).IsUnique().HasDatabaseName("ix_api_keys_key_hash");
        builder.HasIndex(key => key.UserId).HasDatabaseName("ix_api_keys_user_id");
        builder.HasIndex(key => key.TenantId).HasDatabaseName("ix_api_keys_tenant_id");
        builder.HasIndex(key => key.IsActive).HasDatabaseName("ix_api_keys_is_active");
        builder.HasIndex(key => key.ExpiresAt).HasDatabaseName("ix_api_keys_expires_at");
        builder.HasIndex(key => key.ReplacesKeyId).HasDatabaseName("ix_api_keys_replaces_key_id");
        builder.Ignore(key => key.DomainEvents);
        builder.Ignore(key => key.IsGlobal);
        builder.Ignore(key => key.IsNew);
        builder.Ignore(key => key.IsDeleted);
    }
}
