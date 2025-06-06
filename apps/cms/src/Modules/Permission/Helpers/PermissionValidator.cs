using cms.Common.Entities;

namespace cms.Modules.Permission.Helpers;

/// <summary>
/// Helper class for validating permissions in various contexts
/// </summary>
public static class PermissionValidator
{
    /// <summary>
    /// Validates if the context has sufficient permissions for a given operation
    /// </summary>
    public static bool CanPerformOperation(UnifiedPermissionContext context, PermissionOperation operation)
    {
        return operation switch
        {
            PermissionOperation.CommentOnContent => context.InteractionPermissions.HasFlag(ContentInteractionPermission.Comment),
            PermissionOperation.VoteOnContent => context.InteractionPermissions.HasFlag(ContentInteractionPermission.Vote),
            PermissionOperation.ShareContent => context.InteractionPermissions.HasFlag(ContentInteractionPermission.Share),
            
            PermissionOperation.ModerateContent => context.ModerationPermissions.HasFlag(ModerationPermission.Review),
            PermissionOperation.BanUser => context.ModerationPermissions.HasFlag(ModerationPermission.Ban),
            
            PermissionOperation.PublishContent => context.PublishingPermissions.HasFlag(PublishingPermission.Publish),
            PermissionOperation.ScheduleContent => context.PublishingPermissions.HasFlag(PublishingPermission.Schedule),
            
            PermissionOperation.FeatureContent => context.PromotionPermissions.HasFlag(PromotionPermission.Feature),
            PermissionOperation.PinContent => context.PromotionPermissions.HasFlag(PromotionPermission.Pin),
            
            PermissionOperation.MonetizeContent => context.MonetizationPermissions.HasFlag(MonetizationPermission.Monetize),
            PermissionOperation.ViewRevenue => context.MonetizationPermissions.HasFlag(MonetizationPermission.Revenue),
            
            _ => false
        };
    }
    
    /// <summary>
    /// Gets missing permissions for a given operation
    /// </summary>
    public static IEnumerable<string> GetMissingPermissions(UnifiedPermissionContext context, PermissionOperation operation)
    {
        var missing = new List<string>();
        
        switch (operation)
        {                
            case PermissionOperation.PublishContent:
                if (!context.PublishingPermissions.HasFlag(PublishingPermission.Publish))
                    missing.Add("PublishingPermission.Publish");
                break;
                
            case PermissionOperation.ModerateContent:
                if (!context.ModerationPermissions.HasFlag(ModerationPermission.Review))
                    missing.Add("ModerationPermission.Review");
                break;
                
            // Add more cases as needed
        }
        
        return missing;
    }
}

/// <summary>
/// Enumeration of common permission operations
/// </summary>
public enum PermissionOperation
{
    // Content interaction operations
    CommentOnContent,
    VoteOnContent,
    ShareContent,
    ReportContent,
    
    // Moderation operations
    ModerateContent,
    HideContent,
    BanUser,
    SuspendUser,
    
    // Publishing operations
    PublishContent,
    UnpublishContent,
    ScheduleContent,
    
    // Promotion operations
    FeatureContent,
    PinContent,
    RecommendContent,
    
    // Monetization operations
    MonetizeContent,
    ViewRevenue,
    SetPricing,
    
    // Editorial operations
    EditContent,
    ProofreadContent,
    FactCheckContent,
    
    // Quality control operations
    ScoreContent,
    AuditContent,
    SetStandards
}