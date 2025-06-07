using cms.Common.Entities;

namespace cms.Modules.Permission.Helpers;

/// <summary>
/// Builder pattern for creating UnifiedPermissionContext instances
/// </summary>
public class PermissionContextBuilder
{
    private readonly UnifiedPermissionContext _context = new();
    
    // Content interaction permissions
    public PermissionContextBuilder WithInteractionPermissions(InteractionPerm permissions)
    {
        _context.InteractionPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicInteraction() => WithInteractionPermissions(InteractionPerm.BasicInteraction);
    public PermissionContextBuilder WithSocialInteraction() => WithInteractionPermissions(InteractionPerm.SocialInteraction);
    public PermissionContextBuilder WithAllInteractions() => WithInteractionPermissions(InteractionPerm.All);
    
    // Content curation permissions
    public PermissionContextBuilder WithCurationPermissions(CurationPerm permissions)
    {
        _context.CurationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicCuration() => WithCurationPermissions(CurationPerm.BasicCuration);
    public PermissionContextBuilder WithAdvancedCuration() => WithCurationPermissions(CurationPerm.AdvancedCuration);
    
    // Moderation permissions
    public PermissionContextBuilder WithModerationPermissions(ModerationPerm permissions)
    {
        _context.ModerationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicModeration() => WithModerationPermissions(ModerationPerm.BasicModeration);
    public PermissionContextBuilder WithContentControl() => WithModerationPermissions(ModerationPerm.ContentControl);
    public PermissionContextBuilder WithUserControl() => WithModerationPermissions(ModerationPerm.UserControl);
    
    // Lifecycle permissions
    public PermissionContextBuilder WithLifecyclePermissions(LifecyclePerm permissions)
    {
        _context.LifecyclePermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicLifecycle() => WithLifecyclePermissions(LifecyclePerm.BasicLifecycle);
    public PermissionContextBuilder WithAdvancedLifecycle() => WithLifecyclePermissions(LifecyclePerm.AdvancedLifecycle);
    
    // Publishing permissions
    public PermissionContextBuilder WithPublishingPermissions(PublishingPerm permissions)
    {
        _context.PublishingPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicPublishing() => WithPublishingPermissions(PublishingPerm.BasicPublishing);
    public PermissionContextBuilder WithExternalDistribution() => WithPublishingPermissions(PublishingPerm.ExternalDistribution);
    
    // Monetization permissions
    public PermissionContextBuilder WithMonetizationPermissions(MonetizationPerm permissions)
    {
        _context.MonetizationPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicMonetization() => WithMonetizationPermissions(MonetizationPerm.BasicMonetization);
    public PermissionContextBuilder WithAdvancedMonetization() => WithMonetizationPermissions(MonetizationPerm.AdvancedMonetization);
    
    // Editorial permissions
    public PermissionContextBuilder WithEditorialPermissions(EditorialPerm permissions)
    {
        _context.EditorialPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicEditorial() => WithEditorialPermissions(EditorialPerm.BasicEditorial);
    public PermissionContextBuilder WithQualityControl() => WithEditorialPermissions(EditorialPerm.QualityControl);
    
    // Promotion permissions
    public PermissionContextBuilder WithPromotionPermissions(PromotionPerm permissions)
    {
        _context.PromotionPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicPromotion() => WithPromotionPermissions(PromotionPerm.BasicPromotion);
    public PermissionContextBuilder WithVisualPromotion() => WithPromotionPermissions(PromotionPerm.VisualPromotion);
    
    // Quality control permissions
    public PermissionContextBuilder WithQualityPermissions(QualityControlPerm permissions)
    {
        _context.QualityPermissions = permissions;
        return this;
    }
    
    public PermissionContextBuilder WithBasicQuality() => WithQualityPermissions(QualityControlPerm.BasicQuality);
    public PermissionContextBuilder WithQualityAnalytics() => WithQualityPermissions(QualityControlPerm.QualityAnalytics);
    
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
            .WithInteractionPermissions(InteractionPerm.All)
            .WithCurationPermissions(CurationPerm.All)
            .WithModerationPermissions(ModerationPerm.All)
            .WithLifecyclePermissions(LifecyclePerm.All)
            .WithPublishingPermissions(PublishingPerm.All)
            .WithMonetizationPermissions(MonetizationPerm.All)
            .WithEditorialPermissions(EditorialPerm.All)
            .WithPromotionPermissions(PromotionPerm.All)
            .WithQualityPermissions(QualityControlPerm.All);
    }
    
    public UnifiedPermissionContext Build() => _context;
}