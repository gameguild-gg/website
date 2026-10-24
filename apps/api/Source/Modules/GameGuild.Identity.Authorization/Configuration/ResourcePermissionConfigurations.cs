using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     EF Core configuration for ResourceUserPermission entity.
/// </summary>
public class ResourceUserPermissionConfiguration : IEntityTypeConfiguration<ResourceUserPermission>
{
    public void Configure(EntityTypeBuilder<ResourceUserPermission> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.TenantId, e.UserId, e.ResourceType, e.ResourceId });

        builder.HasIndex(e => new { e.TenantId, e.ResourceType, e.ResourceId });

        builder.HasIndex(e => new { e.TenantId, e.UserId });

        builder.HasIndex(e => e.ExpiresAt);

        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.Value,
                v => new TenantId(v))
            .IsRequired();

        builder.Property(e => e.UserId)
            .UsePropertyAccessMode(PropertyAccessMode.Property)
            .IsRequired();

        builder.Property(e => e.ResourceType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ResourceId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Permissions)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(e => e.GrantedByUserName)
            .HasMaxLength(256);

        builder.Property(e => e.RevokedByUserName)
            .HasMaxLength(256);

        builder.Property(e => e.RevocationReason)
            .HasMaxLength(2000);

        // Ignore computed properties
        builder.Ignore(e => e.IsActive);
        builder.Ignore(e => e.IsExpired);
        builder.Ignore(e => e.CanAccess);
    }
}

/// <summary>
///     EF Core configuration for ResourceInvitation entity.
/// </summary>
public class ResourceInvitationConfiguration : IEntityTypeConfiguration<ResourceInvitation>
{
    public void Configure(EntityTypeBuilder<ResourceInvitation> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.TenantId, e.Email });

        builder.HasIndex(e => new { e.TenantId, e.ResourceType, e.ResourceId });

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.ExpiresAt);

        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.Value,
                v => new TenantId(v))
            .IsRequired();

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ResourceType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ResourceId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Permissions)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(e => e.InvitedByUserName)
            .HasMaxLength(256);

        builder.Property(e => e.Message)
            .HasMaxLength(2000);

        builder.Property(e => e.DeclineReason)
            .HasMaxLength(2000);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        // Ignore computed properties
        builder.Ignore(e => e.IsPending);
        builder.Ignore(e => e.IsExpired);
        builder.Ignore(e => e.CanBeAccepted);
        builder.Ignore(e => e.CanBeRevoked);
    }
}
