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
        InteractionPermissions = InteractionPerm.Report
    };
    
    /// <summary>
    /// Regular reader - can read and interact with content
    /// </summary>
    public static UnifiedPermissionContext Reader => new()
    {
        InteractionPermissions = InteractionPerm.BasicInteraction | 
                                InteractionPerm.Follow | 
                                InteractionPerm.Bookmark
    };
    
    /// <summary>
    /// Content creator - can create and manage own content
    /// </summary>
    public static UnifiedPermissionContext ContentCreator => new()
    {
        InteractionPermissions = InteractionPerm.All,
        CurationPermissions = CurationPerm.BasicCuration,
        LifecyclePermissions = LifecyclePerm.BasicLifecycle,
        PublishingPermissions = PublishingPerm.BasicPublishing
    };
    
    /// <summary>
    /// Editor - can edit and curate content, moderate discussions
    /// </summary>
    public static UnifiedPermissionContext Editor => new()
    {
        InteractionPermissions = InteractionPerm.All,
        CurationPermissions = CurationPerm.All,
        ModerationPermissions = ModerationPerm.BasicModeration | ModerationPerm.ContentControl,
        LifecyclePermissions = LifecyclePerm.All,
        PublishingPermissions = PublishingPerm.All,
        EditorialPermissions = EditorialPerm.All,
        PromotionPermissions = PromotionPerm.BasicPromotion,
        QualityPermissions = QualityControlPerm.BasicQuality
    };
    
    /// <summary>
    /// Moderator - focused on content and user moderation
    /// </summary>
    public static UnifiedPermissionContext Moderator => new()
    {
        InteractionPermissions = InteractionPerm.BasicInteraction,
        ModerationPermissions = ModerationPerm.All,
        LifecyclePermissions = LifecyclePerm.Archive | LifecyclePerm.SoftDelete,
        EditorialPermissions = EditorialPerm.QualityControl,
        QualityPermissions = QualityControlPerm.All
    };
    
    /// <summary>
    /// Community Manager - manages community interactions and promotions
    /// </summary>
    public static UnifiedPermissionContext CommunityManager => new()
    {
        InteractionPermissions = InteractionPerm.All,
        CurationPermissions = CurationPerm.All,
        ModerationPermissions = ModerationPerm.BasicModeration | ModerationPerm.ContentControl,
        LifecyclePermissions = LifecyclePerm.BasicLifecycle | LifecyclePerm.Archive,
        PublishingPermissions = PublishingPerm.All,
        PromotionPermissions = PromotionPerm.All,
        QualityPermissions = QualityControlPerm.BasicQuality
    };
    
    /// <summary>
    /// Business Manager - handles monetization and business operations
    /// </summary>
    public static UnifiedPermissionContext BusinessManager => new()
    {
        InteractionPermissions = InteractionPerm.BasicInteraction,
        MonetizationPermissions = MonetizationPerm.All,
        QualityPermissions = QualityControlPerm.QualityAnalytics
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
            InteractionPermissions = InteractionPerm.All,
            CurationPermissions = CurationPerm.Tag | CurationPerm.Collection
        };
        
        /// <summary>
        /// Guild leader - can manage guild content and moderate members
        /// </summary>
        public static UnifiedPermissionContext GuildLeader => new()
        {
            InteractionPermissions = InteractionPerm.All,
            CurationPermissions = CurationPerm.All,
            ModerationPermissions = ModerationPerm.BasicModeration | ModerationPerm.ContentControl,
            LifecyclePermissions = LifecyclePerm.BasicLifecycle,
            PublishingPermissions = PublishingPerm.BasicPublishing,
            PromotionPermissions = PromotionPerm.BasicPromotion
        };
        
        /// <summary>
        /// Tournament organizer - can organize and promote events
        /// </summary>
        public static UnifiedPermissionContext TournamentOrganizer => new()
        {
            InteractionPermissions = InteractionPerm.All,
            CurationPermissions = CurationPerm.All,
            LifecyclePermissions = LifecyclePerm.All,
            PublishingPermissions = PublishingPerm.All,
            PromotionPermissions = PromotionPerm.All,
            MonetizationPermissions = MonetizationPerm.BasicMonetization
        };
    }
}



