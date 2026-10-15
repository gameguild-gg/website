using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for ContentTypePermission
/// </summary>
public class ContentTypePermissionConfiguration : IEntityTypeConfiguration<ContentTypePermission>
{
    public void Configure(EntityTypeBuilder<ContentTypePermission> builder)
    {
        // Configure table name (snake_case convention)
        builder.ToTable("contenttypepermission", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure Id property
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();

        // Property configurations
        builder.Property(x => x.ContentTypeName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Permissions).HasMaxLength(500);
        // Content-type scope and hierarchy (issue #314)
        builder.Property(x => x.ParentContentTypeName).HasMaxLength(256);
        builder.Property(x => x.Restrictions).HasMaxLength(2000);
        builder.Property(x => x.AppliesTo).HasMaxLength(2000);
        builder.Property(x => x.IsRegistered).IsRequired().HasDefaultValue(false);

        // Indexes
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_contenttypepermission_tenant_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_contenttypepermission_user_id");
        builder.HasIndex(x => new { x.TenantId, x.ContentTypeName }).HasDatabaseName("ix_contenttypepermission_tenant_contenttype");
        builder.HasIndex(x => x.ParentContentTypeName).HasDatabaseName("ix_contenttypepermission_parent_contenttype");
    }
}
