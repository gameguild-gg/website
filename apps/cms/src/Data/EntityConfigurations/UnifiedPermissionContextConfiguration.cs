using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;

namespace cms.Data.EntityConfigurations;

/// <summary>
/// Helper methods for configuring UnifiedPermissionContext as an owned type
/// </summary>
public static class UnifiedPermissionContextConfiguration
{
    /// <summary>
    /// Configures UnifiedPermissionContext as an owned type for any entity
    /// </summary>
    public static void ConfigureUnifiedPermissionContext<TOwner>(
        OwnedNavigationBuilder<TOwner, UnifiedPermissionContext> builder, 
        string prefix = "") 
        where TOwner : class
    {
        // Configure enum conversions for database storage
        builder.Property(e => e.InteractionPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}InteractionPermissions");
            
        builder.Property(e => e.CurationPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}CurationPermissions");
            
        builder.Property(e => e.ModerationPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}ModerationPermissions");
            
        builder.Property(e => e.LifecyclePermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}LifecyclePermissions");
            
        builder.Property(e => e.PublishingPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}PublishingPermissions");
            
        builder.Property(e => e.EditorialPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}EditorialPermissions");
            
        builder.Property(e => e.PromotionPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}PromotionPermissions");
            
        builder.Property(e => e.MonetizationPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}MonetizationPermissions");
            
        builder.Property(e => e.QualityPermissions)
            .HasConversion<int>()
            .HasColumnName($"{prefix}QualityPermissions");
    }
}
