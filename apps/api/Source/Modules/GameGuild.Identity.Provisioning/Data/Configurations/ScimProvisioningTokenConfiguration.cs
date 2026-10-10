using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Provisioning;

public sealed class ScimProvisioningTokenConfiguration : IEntityTypeConfiguration<ScimProvisioningToken>
{
    public void Configure(EntityTypeBuilder<ScimProvisioningToken> builder)
    {
        builder.ToTable("scim_provisioning_tokens", "gameguild.authentication");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").IsRequired();
        builder.Property(token => token.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(token => token.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(token => token.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(token => token.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(20).IsRequired();
        builder.Property(token => token.Scopes).HasColumnName("scopes").HasMaxLength(1000).IsRequired();
        builder.Property(token => token.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(token => token.UsageCount).HasColumnName("usage_count").HasDefaultValue(0L).IsRequired();
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at");
        builder.Property(token => token.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(200);
        builder.Property(token => token.ReplacesTokenId).HasColumnName("replaces_token_id");
        builder.Property(token => token.RotationGraceEndsAt).HasColumnName("rotation_grace_ends_at");
        builder.Property(token => token.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(token => token.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(token => token.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(token => token.KeyHash).IsUnique().HasDatabaseName("ix_scim_provisioning_tokens_key_hash");
        builder.HasIndex(token => token.TenantId).HasDatabaseName("ix_scim_provisioning_tokens_tenant_id");
        builder.HasIndex(token => token.IsActive).HasDatabaseName("ix_scim_provisioning_tokens_is_active");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_scim_provisioning_tokens_expires_at");
        builder.HasIndex(token => token.ReplacesTokenId).HasDatabaseName("ix_scim_provisioning_tokens_replaces_token_id");
        builder.Ignore(token => token.DomainEvents);
        builder.Ignore(token => token.IsGlobal);
        builder.Ignore(token => token.IsNew);
        builder.Ignore(token => token.IsDeleted);
    }
}
