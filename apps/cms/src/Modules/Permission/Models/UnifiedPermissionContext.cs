using System;

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