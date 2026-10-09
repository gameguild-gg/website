using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authorization.Configuration;

/// <summary>
///     EF Core configuration for DynamicRole entity.
/// </summary>
public class DynamicRoleConfiguration : IEntityTypeConfiguration<DynamicRole>
{
    public void Configure(EntityTypeBuilder<DynamicRole> builder)
    {
        builder.HasKey(e => e.Id);
        
        // Indexes
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.ParentRoleId);
        
        // Properties
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(256);
        
        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(256);
        
        builder.Property(e => e.Description)
            .HasMaxLength(2000);
        
        // PostgreSQL native array support for Permissions (handled automatically by Npgsql)
        builder.Property(e => e.Permissions)
            .IsRequired();
        
        // PostgreSQL native array support for DenyPermissions (handled automatically by Npgsql)
        builder.Property(e => e.DenyPermissions)
            .IsRequired();
        
        // PostgreSQL native arrays for role IDs (handled automatically by Npgsql)
        builder.Property(e => e.MutuallyExclusiveRoleIds)
            .IsRequired();

        builder.Property(e => e.PrerequisiteRoleIds)
            .IsRequired();

        // Multi-parent inheritance (issue #358): additional parents beyond the primary parent
        builder.Property(e => e.AdditionalParentRoleIds)
            .IsRequired();

        // Selective inheritance blocking (issue #358): inherited permissions this role opts out of
        builder.Property(e => e.BlockedInheritedPermissions)
            .IsRequired();

        // JSONB column for Metadata dictionary — stored as PostgreSQL jsonb type
        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, object>>(
                (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
        
        // Self-referencing relationship for role hierarchy
        builder.HasOne(e => e.ParentRole)
            .WithMany(e => e.ChildRoles)
            .HasForeignKey(e => e.ParentRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
///     EF Core configuration for DynamicRoleAssignment entity.
/// </summary>
public class DynamicRoleAssignmentConfiguration : IEntityTypeConfiguration<DynamicRoleAssignment>
{
    public void Configure(EntityTypeBuilder<DynamicRoleAssignment> builder)
    {
        builder.HasKey(e => e.Id);
        
        // Indexes
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.RoleId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.UserId, e.RoleId, e.TenantId }).IsUnique();
        builder.HasIndex(e => new { e.StartsAt, e.ExpiresAt });
        
        // Properties
        builder.Property(e => e.Reason)
            .HasMaxLength(2000);
        
        // Relationship to DynamicRole
        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
