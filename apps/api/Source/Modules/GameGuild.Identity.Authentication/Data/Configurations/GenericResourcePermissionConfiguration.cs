using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for GenericResourcePermission (#357).
///     Maps the generic resource-level permission entity (layer 3) so it is part of the
///     runtime EF model — <c>Set&lt;GenericResourcePermission&gt;()</c> previously threw because
///     this concrete <see cref="ResourcePermission{TResource}"/> descendant was never mapped.
///     Follows the same snake_case mapping convention as <see cref="ContentTypePermissionConfiguration"/>.
/// </summary>
public class GenericResourcePermissionConfiguration : IEntityTypeConfiguration<GenericResourcePermission>
{
    public void Configure(EntityTypeBuilder<GenericResourcePermission> builder)
    {
        // Configure table name (snake_case convention)
        builder.ToTable("genericresourcepermission", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure Id property
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();

        // Property configurations
        builder.Property(x => x.ResourceType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ResourceTitle).HasMaxLength(512);
        builder.Property(x => x.Permissions).HasMaxLength(500);

        // Indexes for the common permission query paths
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_genericresourcepermission_tenant_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_genericresourcepermission_user_id");
        builder.HasIndex(x => x.ResourceType).HasDatabaseName("ix_genericresourcepermission_resource_type");
        builder.HasIndex(x => new { x.TenantId, x.ResourceType, x.ResourceId })
            .HasDatabaseName("ix_genericresourcepermission_tenant_resource");
    }
}
