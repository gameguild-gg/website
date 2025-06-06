using cms.Modules.Permission.Helpers;

namespace cms.Common.Entities;

/// <summary>
/// Predefined permission contexts for common user roles and scenarios
/// </summary>
public static class PermissionPresets
{
    /// <summary>
    /// Guest user - minimal read-only access
    /// </summary>
    public static UnifiedPermissionContext Guest => new()
    {
        InteractionPermissions = ContentInteractionPermission.Report
    };
    
    /// <summary>
    /// Regular reader - can read and interact with content
    /// </summary>
    public static UnifiedPermissionContext Reader => new()
    {
        InteractionPermissions = ContentInteractionPermission.BasicInteraction | 
                                ContentInteractionPermission.Follow | 
                                ContentInteractionPermission.Bookmark
    };
    
    /// <summary>
    /// Content creator - can create and manage own content
    /// </summary>
    public static UnifiedPermissionContext ContentCreator => new()
    {
        InteractionPermissions = ContentInteractionPermission.All,
        CurationPermissions = ContentCurationPermission.BasicCuration,
        LifecyclePermissions = ContentLifecyclePermission.BasicLifecycle,
        PublishingPermissions = PublishingPermission.BasicPublishing
    };
    
    /// <summary>
    /// Editor - can edit and curate content, moderate discussions
    /// </summary>
    public static UnifiedPermissionContext Editor => new()
    {
        InteractionPermissions = ContentInteractionPermission.All,
        CurationPermissions = ContentCurationPermission.All,
        ModerationPermissions = ModerationPermission.BasicModeration | ModerationPermission.ContentControl,
        LifecyclePermissions = ContentLifecyclePermission.All,
        PublishingPermissions = PublishingPermission.All,
        EditorialPermissions = EditorialPermission.All,
        PromotionPermissions = PromotionPermission.BasicPromotion,
        QualityPermissions = QualityControlPermission.BasicQuality
    };
    
    /// <summary>
    /// Moderator - focused on content and user moderation
    /// </summary>
    public static UnifiedPermissionContext Moderator => new()
    {
        InteractionPermissions = ContentInteractionPermission.BasicInteraction,
        ModerationPermissions = ModerationPermission.All,
        LifecyclePermissions = ContentLifecyclePermission.Archive | ContentLifecyclePermission.SoftDelete,
        EditorialPermissions = EditorialPermission.QualityControl,
        QualityPermissions = QualityControlPermission.All
    };
    
    /// <summary>
    /// Community Manager - manages community interactions and promotions
    /// </summary>
    public static UnifiedPermissionContext CommunityManager => new()
    {
        InteractionPermissions = ContentInteractionPermission.All,
        CurationPermissions = ContentCurationPermission.All,
        ModerationPermissions = ModerationPermission.BasicModeration | ModerationPermission.ContentControl,
        LifecyclePermissions = ContentLifecyclePermission.BasicLifecycle | ContentLifecyclePermission.Archive,
        PublishingPermissions = PublishingPermission.All,
        PromotionPermissions = PromotionPermission.All,
        QualityPermissions = QualityControlPermission.BasicQuality
    };
    
    /// <summary>
    /// Business Manager - handles monetization and business operations
    /// </summary>
    public static UnifiedPermissionContext BusinessManager => new()
    {
        InteractionPermissions = ContentInteractionPermission.BasicInteraction,
        MonetizationPermissions = MonetizationPermission.All,
        QualityPermissions = QualityControlPermission.QualityAnalytics
    };
    
    /// <summary>
    /// Admin - full access to all systems
    /// </summary>
    public static UnifiedPermissionContext Admin => PermissionContextBuilder.CreateForAdmin().Build();
    
    /// <summary>
    /// Gaming-specific permission presets for different game contexts
    /// </summary>
    public static class Gaming
    {
        /// <summary>
        /// Game player - can interact with game content and community
        /// </summary>
        public static UnifiedPermissionContext Player => new()
        {
            InteractionPermissions = ContentInteractionPermission.All,
            CurationPermissions = ContentCurationPermission.Tag | ContentCurationPermission.Collection
        };
        
        /// <summary>
        /// Guild leader - can manage guild content and moderate members
        /// </summary>
        public static UnifiedPermissionContext GuildLeader => new()
        {
            InteractionPermissions = ContentInteractionPermission.All,
            CurationPermissions = ContentCurationPermission.All,
            ModerationPermissions = ModerationPermission.BasicModeration | ModerationPermission.ContentControl,
            LifecyclePermissions = ContentLifecyclePermission.BasicLifecycle,
            PublishingPermissions = PublishingPermission.BasicPublishing,
            PromotionPermissions = PromotionPermission.BasicPromotion
        };
        
        /// <summary>
        /// Tournament organizer - can organize and promote events
        /// </summary>
        public static UnifiedPermissionContext TournamentOrganizer => new()
        {
            InteractionPermissions = ContentInteractionPermission.All,
            CurationPermissions = ContentCurationPermission.All,
            LifecyclePermissions = ContentLifecyclePermission.All,
            PublishingPermissions = PublishingPermission.All,
            PromotionPermissions = PromotionPermission.All,
            MonetizationPermissions = MonetizationPermission.BasicMonetization
        };
    }
}



