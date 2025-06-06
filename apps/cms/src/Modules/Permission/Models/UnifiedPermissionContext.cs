using System;

namespace cms.Common.Entities;

/// <summary>
/// Unified context containing all permission types for comprehensive access control
/// </summary>
public class UnifiedPermissionContext
{
    public ContentInteractionPermission InteractionPermissions { get; set; } = ContentInteractionPermission.None;
    public ContentCurationPermission CurationPermissions { get; set; } = ContentCurationPermission.None;
    public ModerationPermission ModerationPermissions { get; set; } = ModerationPermission.None;
    public ContentLifecyclePermission LifecyclePermissions { get; set; } = ContentLifecyclePermission.None;
    public PublishingPermission PublishingPermissions { get; set; } = PublishingPermission.None;
    public MonetizationPermission MonetizationPermissions { get; set; } = MonetizationPermission.None;
    public EditorialPermission EditorialPermissions { get; set; } = EditorialPermission.None;
    public PromotionPermission PromotionPermissions { get; set; } = PromotionPermission.None;
    public QualityControlPermission QualityPermissions { get; set; } = QualityControlPermission.None;    
    
    // Helper methods for common permission checks
    public bool CanInteract => InteractionPermissions != ContentInteractionPermission.None;
    public bool CanModerate => ModerationPermissions != ModerationPermission.None;
    public bool CanPublish => PublishingPermissions.HasFlag(PublishingPermission.Publish);
    public bool CanMonetize => MonetizationPermissions != MonetizationPermission.None;
    public bool CanPromote => PromotionPermissions != PromotionPermission.None;
    public bool CanEdit => EditorialPermissions != EditorialPermission.None;
    
    // Context-aware permission validation
    public bool HasPermission<T>(T permission) where T : Enum
    {
        return permission switch
        {
            ContentInteractionPermission interaction => InteractionPermissions.HasFlag(interaction),
            ContentCurationPermission curation => CurationPermissions.HasFlag(curation),
            ModerationPermission moderation => ModerationPermissions.HasFlag(moderation),
            ContentLifecyclePermission lifecycle => LifecyclePermissions.HasFlag(lifecycle),
            PublishingPermission publishing => PublishingPermissions.HasFlag(publishing),
            MonetizationPermission monetization => MonetizationPermissions.HasFlag(monetization),
            EditorialPermission editorial => EditorialPermissions.HasFlag(editorial),
            PromotionPermission promotion => PromotionPermissions.HasFlag(promotion),
            QualityControlPermission quality => QualityPermissions.HasFlag(quality),
            _ => false
        };
    }
}