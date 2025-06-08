using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;

namespace cms.Common.Entities;

/// <summary>
/// Unified context containing all permission types for comprehensive access control
/// </summary>
public class UnifiedPermissionContext
{
    public InteractionPerm InteractionPermissions { get; set; } = InteractionPerm.None;
    public CurationPerm CurationPermissions { get; set; } = CurationPerm.None;
    public ModerationPerm ModerationPermissions { get; set; } = ModerationPerm.None;
    public LifecyclePerm LifecyclePermissions { get; set; } = LifecyclePerm.None;
    public PublishingPerm PublishingPermissions { get; set; } = PublishingPerm.None;
    public MonetizationPerm MonetizationPermissions { get; set; } = MonetizationPerm.None;
    public EditorialPerm EditorialPermissions { get; set; } = EditorialPerm.None;
    public PromotionPerm PromotionPermissions { get; set; } = PromotionPerm.None;
    public QualityControlPerm QualityPermissions { get; set; } = QualityControlPerm.None;

    // Helper methods for common permission checks
    public bool CanInteract => InteractionPermissions != InteractionPerm.None;
    public bool CanModerate => ModerationPermissions != ModerationPerm.None;
    public bool CanPublish => PublishingPermissions.HasFlag(PublishingPerm.Publish);
    public bool CanMonetize => MonetizationPermissions != MonetizationPerm.None;
    public bool CanPromote => PromotionPermissions != PromotionPerm.None;
    public bool CanEdit => EditorialPermissions != EditorialPerm.None;

    // Context-aware permission validation
    public bool HasPermission<T>(T permission) where T : Enum
    {
        return permission switch
        {
            InteractionPerm interaction => InteractionPermissions.HasFlag(interaction),
            CurationPerm curation => CurationPermissions.HasFlag(curation),
            ModerationPerm moderation => ModerationPermissions.HasFlag(moderation),
            LifecyclePerm lifecycle => LifecyclePermissions.HasFlag(lifecycle),
            PublishingPerm publishing => PublishingPermissions.HasFlag(publishing),
            MonetizationPerm monetization => MonetizationPermissions.HasFlag(monetization),
            EditorialPerm editorial => EditorialPermissions.HasFlag(editorial),
            PromotionPerm promotion => PromotionPermissions.HasFlag(promotion),
            QualityControlPerm quality => QualityPermissions.HasFlag(quality),
            _ => false
        };
    }
}


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
