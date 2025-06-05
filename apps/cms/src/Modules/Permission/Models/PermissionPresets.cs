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
        CorePermissions = CorePermissionType.Read,
        InteractionPermissions = ContentInteractionPermissionType.Report
    };
    
    /// <summary>
    /// Regular reader - can read and interact with content
    /// </summary>
    public static UnifiedPermissionContext Reader => new()
    {
        CorePermissions = CorePermissionType.Read,
        InteractionPermissions = ContentInteractionPermissionType.BasicInteraction | 
                                ContentInteractionPermissionType.Follow | 
                                ContentInteractionPermissionType.Bookmark
    };
    
    /// <summary>
    /// Content creator - can create and manage own content
    /// </summary>
    public static UnifiedPermissionContext ContentCreator => new()
    {
        CorePermissions = CorePermissionType.ReadWrite,
        InteractionPermissions = ContentInteractionPermissionType.All,
        CurationPermissions = ContentCurationPermissionType.BasicCuration,
        LifecyclePermissions = ContentLifecyclePermissionType.BasicLifecycle,
        PublishingPermissions = PublishingPermissionType.BasicPublishing
    };
    
    /// <summary>
    /// Editor - can edit and curate content, moderate discussions
    /// </summary>
    public static UnifiedPermissionContext Editor => new()
    {
        CorePermissions = CorePermissionType.All,
        InteractionPermissions = ContentInteractionPermissionType.All,
        CurationPermissions = ContentCurationPermissionType.All,
        ModerationPermissions = ModerationPermissionType.BasicModeration | ModerationPermissionType.ContentControl,
        LifecyclePermissions = ContentLifecyclePermissionType.All,
        PublishingPermissions = PublishingPermissionType.All,
        EditorialPermissions = EditorialPermissionType.All,
        PromotionPermissions = PromotionPermissionType.BasicPromotion,
        QualityPermissions = QualityControlPermissionType.BasicQuality
    };
    
    /// <summary>
    /// Moderator - focused on content and user moderation
    /// </summary>
    public static UnifiedPermissionContext Moderator => new()
    {
        CorePermissions = CorePermissionType.Read,
        InteractionPermissions = ContentInteractionPermissionType.BasicInteraction,
        ModerationPermissions = ModerationPermissionType.All,
        LifecyclePermissions = ContentLifecyclePermissionType.Archive | ContentLifecyclePermissionType.SoftDelete,
        EditorialPermissions = EditorialPermissionType.QualityControl,
        QualityPermissions = QualityControlPermissionType.All
    };
    
    /// <summary>
    /// Community Manager - manages community interactions and promotions
    /// </summary>
    public static UnifiedPermissionContext CommunityManager => new()
    {
        CorePermissions = CorePermissionType.ReadWrite,
        InteractionPermissions = ContentInteractionPermissionType.All,
        CurationPermissions = ContentCurationPermissionType.All,
        ModerationPermissions = ModerationPermissionType.BasicModeration | ModerationPermissionType.ContentControl,
        LifecyclePermissions = ContentLifecyclePermissionType.BasicLifecycle | ContentLifecyclePermissionType.Archive,
        PublishingPermissions = PublishingPermissionType.All,
        PromotionPermissions = PromotionPermissionType.All,
        QualityPermissions = QualityControlPermissionType.BasicQuality
    };
    
    /// <summary>
    /// Business Manager - handles monetization and business operations
    /// </summary>
    public static UnifiedPermissionContext BusinessManager => new()
    {
        CorePermissions = CorePermissionType.Read,
        InteractionPermissions = ContentInteractionPermissionType.BasicInteraction,
        MonetizationPermissions = MonetizationPermissionType.All,
        QualityPermissions = QualityControlPermissionType.QualityAnalytics
    };
    
    /// <summary>
    /// Admin - full access to all systems
    /// </summary>
    public static UnifiedPermissionContext Admin => PermissionContextBuilder.CreateForAdmin().Build();
    
    /// <summary>
    /// Gets a permission context for a specific user role
    /// </summary>
    public static UnifiedPermissionContext GetForRole(UserRole role)
    {
        return role switch
        {
            UserRole.Guest => Guest,
            UserRole.Reader => Reader,
            UserRole.ContentCreator => ContentCreator,
            UserRole.Contributor => ContentCreator, // Alias for ContentCreator
            UserRole.Editor => Editor,
            UserRole.Moderator => Moderator,
            UserRole.CommunityManager => CommunityManager,
            UserRole.BusinessManager => BusinessManager,
            UserRole.Admin => Admin,
            _ => Guest
        };
    }
    
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
            CorePermissions = CorePermissionType.Read,
            InteractionPermissions = ContentInteractionPermissionType.All,
            CurationPermissions = ContentCurationPermissionType.Tag | ContentCurationPermissionType.Collection
        };
        
        /// <summary>
        /// Guild leader - can manage guild content and moderate members
        /// </summary>
        public static UnifiedPermissionContext GuildLeader => new()
        {
            CorePermissions = CorePermissionType.ReadWrite,
            InteractionPermissions = ContentInteractionPermissionType.All,
            CurationPermissions = ContentCurationPermissionType.All,
            ModerationPermissions = ModerationPermissionType.BasicModeration | ModerationPermissionType.ContentControl,
            LifecyclePermissions = ContentLifecyclePermissionType.BasicLifecycle,
            PublishingPermissions = PublishingPermissionType.BasicPublishing,
            PromotionPermissions = PromotionPermissionType.BasicPromotion
        };
        
        /// <summary>
        /// Tournament organizer - can organize and promote events
        /// </summary>
        public static UnifiedPermissionContext TournamentOrganizer => new()
        {
            CorePermissions = CorePermissionType.ReadWrite,
            InteractionPermissions = ContentInteractionPermissionType.All,
            CurationPermissions = ContentCurationPermissionType.All,
            LifecyclePermissions = ContentLifecyclePermissionType.All,
            PublishingPermissions = PublishingPermissionType.All,
            PromotionPermissions = PromotionPermissionType.All,
            MonetizationPermissions = MonetizationPermissionType.BasicMonetization
        };
    }
}



