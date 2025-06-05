using System;

namespace cms.Common.Entities;

/// <summary>
/// Unified context containing all permission types for comprehensive access control
/// </summary>
public class UnifiedPermissionContext
{
    public CorePermissionType CorePermissions { get; set; } = CorePermissionType.None;
    public ContentInteractionPermissionType InteractionPermissions { get; set; } = ContentInteractionPermissionType.None;
    public ContentCurationPermissionType CurationPermissions { get; set; } = ContentCurationPermissionType.None;
    public ModerationPermissionType ModerationPermissions { get; set; } = ModerationPermissionType.None;
    public ContentLifecyclePermissionType LifecyclePermissions { get; set; } = ContentLifecyclePermissionType.None;
    public PublishingPermissionType PublishingPermissions { get; set; } = PublishingPermissionType.None;
    public MonetizationPermissionType MonetizationPermissions { get; set; } = MonetizationPermissionType.None;
    public EditorialPermissionType EditorialPermissions { get; set; } = EditorialPermissionType.None;
    public PromotionPermissionType PromotionPermissions { get; set; } = PromotionPermissionType.None;
    public QualityControlPermissionType QualityPermissions { get; set; } = QualityControlPermissionType.None;
    
    // Helper methods for common permission checks
    public bool CanRead => CorePermissions.HasFlag(CorePermissionType.Read);
    public bool CanWrite => CorePermissions.HasFlag(CorePermissionType.Create | CorePermissionType.Update);
    public bool CanDelete => CorePermissions.HasFlag(CorePermissionType.Delete);
    public bool CanInteract => InteractionPermissions != ContentInteractionPermissionType.None;
    public bool CanModerate => ModerationPermissions != ModerationPermissionType.None;
    public bool CanPublish => PublishingPermissions.HasFlag(PublishingPermissionType.Publish);
    public bool CanMonetize => MonetizationPermissions != MonetizationPermissionType.None;
    public bool CanPromote => PromotionPermissions != PromotionPermissionType.None;
    public bool CanEdit => EditorialPermissions != EditorialPermissionType.None;
    
    // Context-aware permission validation
    public bool HasPermission<T>(T permission) where T : Enum
    {
        return permission switch
        {
            CorePermissionType core => CorePermissions.HasFlag(core),
            ContentInteractionPermissionType interaction => InteractionPermissions.HasFlag(interaction),
            ContentCurationPermissionType curation => CurationPermissions.HasFlag(curation),
            ModerationPermissionType moderation => ModerationPermissions.HasFlag(moderation),
            ContentLifecyclePermissionType lifecycle => LifecyclePermissions.HasFlag(lifecycle),
            PublishingPermissionType publishing => PublishingPermissions.HasFlag(publishing),
            MonetizationPermissionType monetization => MonetizationPermissions.HasFlag(monetization),
            EditorialPermissionType editorial => EditorialPermissions.HasFlag(editorial),
            PromotionPermissionType promotion => PromotionPermissions.HasFlag(promotion),
            QualityControlPermissionType quality => QualityPermissions.HasFlag(quality),
            _ => false
        };
    }
    
    // Migration helper for backward compatibility
#pragma warning disable CS0618 // Type or member is obsolete
    public static UnifiedPermissionContext FromLegacyPermissionType(PermissionType legacyPermission)
    {
        var context = new UnifiedPermissionContext();
        
        // Map core permissions
        if (legacyPermission.HasFlag(PermissionType.Read))
            context.CorePermissions |= CorePermissionType.Read;
        if (legacyPermission.HasFlag(PermissionType.Create))
            context.CorePermissions |= CorePermissionType.Create;
        if (legacyPermission.HasFlag(PermissionType.Update))
            context.CorePermissions |= CorePermissionType.Update;
        if (legacyPermission.HasFlag(PermissionType.Delete))
            context.CorePermissions |= CorePermissionType.Delete;
            
        return context;
    }
#pragma warning restore CS0618 // Type or member is obsolete
}