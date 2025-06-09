using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cms.Common.Entities;

/// <summary>
/// Entity representing permissions on resources for specific users
/// Provides fine-grained access control for resources
/// </summary>
public class ResourcePermission : BaseEntity
{
    /// <summary>
    /// Navigation property to the user who has this permission
    /// Entity Framework will automatically create the UserId foreign key
    /// </summary>
    public virtual Modules.User.Models.User? User
    {
        get;
        set;
    }

    // todo: add polymorphism support for resource types
    /// <summary>
    /// Type of the resource (for polymorphic relationships)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Navigation property to resource metadata
    /// Entity Framework will automatically create the ResourceMetadataId foreign key
    /// </summary>
    public virtual ResourceMetadata? ResourceMetadata
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the specific resource this permission applies to
    /// Entity Framework will automatically create the ResourceId foreign key
    /// </summary>
    [Required]
    public virtual ResourceBase Resource
    {
        get;
        set;
    } = null!;


    /// <summary>
    /// When this permission was granted
    /// </summary>
    public DateTime GrantedAt
    {
        get;
        set;
    } = DateTime.UtcNow;

    /// <summary>
    /// User who granted this permission
    /// </summary>
    [Required]
    public Guid GrantedByUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the user who granted this permission
    /// </summary>
    public virtual Modules.User.Models.User GrantedByUser
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Optional expiration date for this permission
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this permission is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Check if the permission is expired
    /// </summary>
    public bool IsExpired
    {
        get => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// Check if the permission is valid (active and not expired)
    /// </summary>
    public bool IsValid
    {
        get => IsActive && !IsExpired && !IsDeleted;
    }
}


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

    }
}
