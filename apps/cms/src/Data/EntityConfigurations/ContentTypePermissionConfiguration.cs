using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;

namespace cms.Data.EntityConfigurations;

/// <summary>
/// Entity Framework configuration for ContentTypePermission entity
/// Configures the multiple relationships to User entity
/// </summary>
public class ContentTypePermissionConfiguration : IEntityTypeConfiguration<ContentTypePermission>
{
    public void Configure(EntityTypeBuilder<ContentTypePermission> builder)
    {
        // Configure the User relationship (primary user who has the permission)
        builder.HasOne(p => p.User)
            .WithMany(u => u.ContentTypePermissions)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        // Configure the AssignedByUser relationship (user who granted the permission)
        builder.HasOne(p => p.AssignedByUser)
            .WithMany() // No navigation property on User for this relationship
            .HasForeignKey(p => p.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict) // Prevent deletion of user who assigned permissions
            .IsRequired();

        // Configure indexes for performance
        builder.HasIndex(p => new { p.UserId, p.TenantId, p.ContentTypeName })
            .IsUnique()
            .HasDatabaseName("IX_ContentTypePermissions_User_Tenant_ContentType");

        builder.HasIndex(p => p.AssignedByUserId)
            .HasDatabaseName("IX_ContentTypePermissions_AssignedByUser");

        // Configure property constraints
        builder.Property(p => p.ContentTypeName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.AssignedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Configure UnifiedPermissionContext as owned type
        builder.OwnsOne(p => p.PermissionContext, pc =>
        {
            UnifiedPermissionContextConfiguration.ConfigureUnifiedPermissionContext(pc);
        });
    }
}
