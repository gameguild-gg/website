using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;

namespace cms.Data.EntityConfigurations;

/// <summary>
/// Entity Framework configuration for ResourcePermission entity
/// Configures the multiple relationships to User entity
/// </summary>
public class ResourcePermissionConfiguration : IEntityTypeConfiguration<ResourcePermission>
{
    public void Configure(EntityTypeBuilder<ResourcePermission> builder)
    {
        // Configure the User relationship (user who has the permission)
        builder.HasOne(p => p.User)
            .WithMany(u => u.GrantedResourcePermissions) // Maps to User.GrantedResourcePermissions
            .HasForeignKey("UserId") // Shadow foreign key
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false); // Optional - can be null

        // Configure the GrantedByUser relationship (user who granted the permission)
        builder.HasOne(p => p.GrantedByUser)
            .WithMany(u => u.GrantedPermissions) // Maps to User.GrantedPermissions
            .HasForeignKey(p => p.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict) // Prevent deletion of user who granted permissions
            .IsRequired();

        // Configure the Resource relationship
        builder.HasOne(p => p.Resource)
            .WithMany(r => r.ResourcePermissions)
            .HasForeignKey("ResourceId") // Shadow foreign key
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        // Configure the ResourceMetadata relationship
        builder.HasOne(p => p.ResourceMetadata)
            .WithMany(rm => rm.ResourcePermissions)
            .HasForeignKey("ResourceMetadataId") // Shadow foreign key
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false); // Optional

        // Configure indexes for performance
        builder.HasIndex("UserId", "ResourceId")
            .IsUnique()
            .HasDatabaseName("IX_ResourcePermissions_User_Resource");

        builder.HasIndex(p => p.GrantedByUserId)
            .HasDatabaseName("IX_ResourcePermissions_GrantedByUser");

        builder.HasIndex("ResourceId")
            .HasDatabaseName("IX_ResourcePermissions_Resource");

        // Configure property constraints
        builder.Property(p => p.ResourceType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.GrantedAt)
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
