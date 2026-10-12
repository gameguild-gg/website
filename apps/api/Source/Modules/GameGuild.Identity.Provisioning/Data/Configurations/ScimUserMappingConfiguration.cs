using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Provisioning;

public sealed class ScimUserMappingConfiguration : IEntityTypeConfiguration<ScimUserMapping>
{
    public void Configure(EntityTypeBuilder<ScimUserMapping> builder)
    {
        builder.ToTable("scim_user_mappings", "gameguild.authentication");
        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).HasColumnName("id").IsRequired();
        builder.Property(mapping => mapping.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(mapping => mapping.ExternalId).HasColumnName("external_id").HasMaxLength(256).IsRequired();
        builder.Property(mapping => mapping.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(mapping => mapping.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(mapping => mapping.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(mapping => mapping.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(mapping => mapping.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(mapping => new { mapping.TenantId, mapping.ExternalId })
            .IsUnique()
            .HasDatabaseName("ix_scim_user_mappings_tenant_id_external_id");
        builder.HasIndex(mapping => mapping.UserId).HasDatabaseName("ix_scim_user_mappings_user_id");
        builder.Ignore(mapping => mapping.DomainEvents);
        builder.Ignore(mapping => mapping.IsGlobal);
        builder.Ignore(mapping => mapping.IsNew);
        builder.Ignore(mapping => mapping.IsDeleted);
    }
}
