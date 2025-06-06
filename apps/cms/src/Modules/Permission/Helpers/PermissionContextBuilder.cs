using cms.Common.Entities;

namespace cms.Modules.Permission.Helpers;

/// <summary>
/// Builder pattern for creating UnifiedPermissionContext instances
/// </summary>
public class PermissionContextBuilder
{
    private readonly UnifiedPermissionContext _context = new();
    
    // Content interaction permissions
    public PermissionContextBuilder WithInteractionPermissions(ContentInteractionPermission permissions)
    {
        _context.InteractionPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicInteraction() => WithInteractionPermissions(ContentInteractionPermission.BasicInteraction);
    public PermissionContextBuilder WithSocialInteraction() => WithInteractionPermissions(ContentInteractionPermission.SocialInteraction);
    public PermissionContextBuilder WithAllInteractions() => WithInteractionPermissions(ContentInteractionPermission.All);
    
    // Content curation permissions
    public PermissionContextBuilder WithCurationPermissions(ContentCurationPermission permissions)
    {
        _context.CurationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicCuration() => WithCurationPermissions(ContentCurationPermission.BasicCuration);
    public PermissionContextBuilder WithAdvancedCuration() => WithCurationPermissions(ContentCurationPermission.AdvancedCuration);
    
    // Moderation permissions
    public PermissionContextBuilder WithModerationPermissions(ModerationPermission permissions)
    {
        _context.ModerationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicModeration() => WithModerationPermissions(ModerationPermission.BasicModeration);
    public PermissionContextBuilder WithContentControl() => WithModerationPermissions(ModerationPermission.ContentControl);
    public PermissionContextBuilder WithUserControl() => WithModerationPermissions(ModerationPermission.UserControl);
    
    // Lifecycle permissions
    public PermissionContextBuilder WithLifecyclePermissions(ContentLifecyclePermission permissions)
    {
        _context.LifecyclePermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicLifecycle() => WithLifecyclePermissions(ContentLifecyclePermission.BasicLifecycle);
    public PermissionContextBuilder WithAdvancedLifecycle() => WithLifecyclePermissions(ContentLifecyclePermission.AdvancedLifecycle);
    
    // Publishing permissions
    public PermissionContextBuilder WithPublishingPermissions(PublishingPermission permissions)
    {
        _context.PublishingPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicPublishing() => WithPublishingPermissions(PublishingPermission.BasicPublishing);
    public PermissionContextBuilder WithExternalDistribution() => WithPublishingPermissions(PublishingPermission.ExternalDistribution);
    
    // Monetization permissions
    public PermissionContextBuilder WithMonetizationPermissions(MonetizationPermission permissions)
    {
        _context.MonetizationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicMonetization() => WithMonetizationPermissions(MonetizationPermission.BasicMonetization);
    public PermissionContextBuilder WithAdvancedMonetization() => WithMonetizationPermissions(MonetizationPermission.AdvancedMonetization);
    
    // Editorial permissions
    public PermissionContextBuilder WithEditorialPermissions(EditorialPermission permissions)
    {
        _context.EditorialPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicEditorial() => WithEditorialPermissions(EditorialPermission.BasicEditorial);
    public PermissionContextBuilder WithQualityControl() => WithEditorialPermissions(EditorialPermission.QualityControl);
    
    // Promotion permissions
    public PermissionContextBuilder WithPromotionPermissions(PromotionPermission permissions)
    {
        _context.PromotionPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicPromotion() => WithPromotionPermissions(PromotionPermission.BasicPromotion);
    public PermissionContextBuilder WithVisualPromotion() => WithPromotionPermissions(PromotionPermission.VisualPromotion);
    
    // Quality control permissions
    public PermissionContextBuilder WithQualityPermissions(QualityControlPermission permissions)
    {
        _context.QualityPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicQuality() => WithQualityPermissions(QualityControlPermission.BasicQuality);
    public PermissionContextBuilder WithQualityAnalytics() => WithQualityPermissions(QualityControlPermission.QualityAnalytics);
    
    // Predefined role builders
    public static PermissionContextBuilder CreateForReader()
    {
        return new PermissionContextBuilder()
            .WithBasicInteraction();
    }
    
    public static PermissionContextBuilder CreateForContributor()
    {
        return new PermissionContextBuilder()
            .WithBasicInteraction()
            .WithBasicCuration()
            .WithBasicLifecycle();
    }
    
    public static PermissionContextBuilder CreateForEditor()
    {
        return new PermissionContextBuilder()
            .WithAllInteractions()
            .WithAdvancedCuration()
            .WithBasicModeration()
            .WithAdvancedLifecycle()
            .WithBasicPublishing()
            .WithBasicEditorial();
    }
    
    public static PermissionContextBuilder CreateForModerator()
    {
        return new PermissionContextBuilder()
            .WithBasicModeration()
            .WithContentControl()
            .WithQualityControl();
    }
    
    public static PermissionContextBuilder CreateForAdmin()
    {
        return new PermissionContextBuilder()
            .WithInteractionPermissions(ContentInteractionPermission.All)
            .WithCurationPermissions(ContentCurationPermission.All)
            .WithModerationPermissions(ModerationPermission.All)
            .WithLifecyclePermissions(ContentLifecyclePermission.All)
            .WithPublishingPermissions(PublishingPermission.All)
            .WithMonetizationPermissions(MonetizationPermission.All)
            .WithEditorialPermissions(EditorialPermission.All)
            .WithPromotionPermissions(PromotionPermission.All)
            .WithQualityPermissions(QualityControlPermission.All);
    }
    
    public UnifiedPermissionContext Build() => _context;
}