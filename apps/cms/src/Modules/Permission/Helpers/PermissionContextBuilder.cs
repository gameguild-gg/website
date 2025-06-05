using cms.Common.Entities;

namespace cms.Modules.Permission.Helpers;

/// <summary>
/// Builder pattern for creating UnifiedPermissionContext instances
/// </summary>
public class PermissionContextBuilder
{
    private readonly UnifiedPermissionContext _context = new();
    
    // Core permissions
    public PermissionContextBuilder WithCorePermissions(CorePermissionType permissions)
    {
        _context.CorePermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithRead() => WithCorePermissions(_context.CorePermissions | CorePermissionType.Read);
    public PermissionContextBuilder WithWrite() => WithCorePermissions(_context.CorePermissions | CorePermissionType.ReadWrite);
    public PermissionContextBuilder WithFullCrud() => WithCorePermissions(CorePermissionType.All);
    
    // Content interaction permissions
    public PermissionContextBuilder WithInteractionPermissions(ContentInteractionPermissionType permissions)
    {
        _context.InteractionPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicInteraction() => WithInteractionPermissions(ContentInteractionPermissionType.BasicInteraction);
    public PermissionContextBuilder WithSocialInteraction() => WithInteractionPermissions(ContentInteractionPermissionType.SocialInteraction);
    public PermissionContextBuilder WithAllInteractions() => WithInteractionPermissions(ContentInteractionPermissionType.All);
    
    // Content curation permissions
    public PermissionContextBuilder WithCurationPermissions(ContentCurationPermissionType permissions)
    {
        _context.CurationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicCuration() => WithCurationPermissions(ContentCurationPermissionType.BasicCuration);
    public PermissionContextBuilder WithAdvancedCuration() => WithCurationPermissions(ContentCurationPermissionType.AdvancedCuration);
    
    // Moderation permissions
    public PermissionContextBuilder WithModerationPermissions(ModerationPermissionType permissions)
    {
        _context.ModerationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicModeration() => WithModerationPermissions(ModerationPermissionType.BasicModeration);
    public PermissionContextBuilder WithContentControl() => WithModerationPermissions(ModerationPermissionType.ContentControl);
    public PermissionContextBuilder WithUserControl() => WithModerationPermissions(ModerationPermissionType.UserControl);
    
    // Lifecycle permissions
    public PermissionContextBuilder WithLifecyclePermissions(ContentLifecyclePermissionType permissions)
    {
        _context.LifecyclePermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicLifecycle() => WithLifecyclePermissions(ContentLifecyclePermissionType.BasicLifecycle);
    public PermissionContextBuilder WithAdvancedLifecycle() => WithLifecyclePermissions(ContentLifecyclePermissionType.AdvancedLifecycle);
    
    // Publishing permissions
    public PermissionContextBuilder WithPublishingPermissions(PublishingPermissionType permissions)
    {
        _context.PublishingPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicPublishing() => WithPublishingPermissions(PublishingPermissionType.BasicPublishing);
    public PermissionContextBuilder WithExternalDistribution() => WithPublishingPermissions(PublishingPermissionType.ExternalDistribution);
    
    // Monetization permissions
    public PermissionContextBuilder WithMonetizationPermissions(MonetizationPermissionType permissions)
    {
        _context.MonetizationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicMonetization() => WithMonetizationPermissions(MonetizationPermissionType.BasicMonetization);
    public PermissionContextBuilder WithAdvancedMonetization() => WithMonetizationPermissions(MonetizationPermissionType.AdvancedMonetization);
    
    // Editorial permissions
    public PermissionContextBuilder WithEditorialPermissions(EditorialPermissionType permissions)
    {
        _context.EditorialPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicEditorial() => WithEditorialPermissions(EditorialPermissionType.BasicEditorial);
    public PermissionContextBuilder WithQualityControl() => WithEditorialPermissions(EditorialPermissionType.QualityControl);
    
    // Promotion permissions
    public PermissionContextBuilder WithPromotionPermissions(PromotionPermissionType permissions)
    {
        _context.PromotionPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicPromotion() => WithPromotionPermissions(PromotionPermissionType.BasicPromotion);
    public PermissionContextBuilder WithVisualPromotion() => WithPromotionPermissions(PromotionPermissionType.VisualPromotion);
    
    // Quality control permissions
    public PermissionContextBuilder WithQualityPermissions(QualityControlPermissionType permissions)
    {
        _context.QualityPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicQuality() => WithQualityPermissions(QualityControlPermissionType.BasicQuality);
    public PermissionContextBuilder WithQualityAnalytics() => WithQualityPermissions(QualityControlPermissionType.QualityAnalytics);
    
    // Predefined role builders
    public static PermissionContextBuilder CreateForReader()
    {
        return new PermissionContextBuilder()
            .WithRead()
            .WithBasicInteraction();
    }
    
    public static PermissionContextBuilder CreateForContributor()
    {
        return new PermissionContextBuilder()
            .WithWrite()
            .WithBasicInteraction()
            .WithBasicCuration()
            .WithBasicLifecycle();
    }
    
    public static PermissionContextBuilder CreateForEditor()
    {
        return new PermissionContextBuilder()
            .WithFullCrud()
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
            .WithRead()
            .WithBasicModeration()
            .WithContentControl()
            .WithQualityControl();
    }
    
    public static PermissionContextBuilder CreateForAdmin()
    {
        return new PermissionContextBuilder()
            .WithCorePermissions(CorePermissionType.All)
            .WithInteractionPermissions(ContentInteractionPermissionType.All)
            .WithCurationPermissions(ContentCurationPermissionType.All)
            .WithModerationPermissions(ModerationPermissionType.All)
            .WithLifecyclePermissions(ContentLifecyclePermissionType.All)
            .WithPublishingPermissions(PublishingPermissionType.All)
            .WithMonetizationPermissions(MonetizationPermissionType.All)
            .WithEditorialPermissions(EditorialPermissionType.All)
            .WithPromotionPermissions(PromotionPermissionType.All)
            .WithQualityPermissions(QualityControlPermissionType.All);
    }
    
    public UnifiedPermissionContext Build() => _context;
}