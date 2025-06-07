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
            PermissionOperation.CommentOnContent => context.InteractionPermissions.HasFlag(InteractionPerm.Comment),
            PermissionOperation.VoteOnContent => context.InteractionPermissions.HasFlag(InteractionPerm.Vote),
            PermissionOperation.ShareContent => context.InteractionPermissions.HasFlag(InteractionPerm.Share),
            
            PermissionOperation.ModerateContent => context.ModerationPermissions.HasFlag(ModerationPerm.Review),
            PermissionOperation.BanUser => context.ModerationPermissions.HasFlag(ModerationPerm.Ban),
            
            PermissionOperation.PublishContent => context.PublishingPermissions.HasFlag(PublishingPerm.Publish),
            PermissionOperation.ScheduleContent => context.PublishingPermissions.HasFlag(PublishingPerm.Schedule),
            
            PermissionOperation.FeatureContent => context.PromotionPermissions.HasFlag(PromotionPerm.Feature),
            PermissionOperation.PinContent => context.PromotionPermissions.HasFlag(PromotionPerm.Pin),
            
            PermissionOperation.MonetizeContent => context.MonetizationPermissions.HasFlag(MonetizationPerm.Monetize),
            PermissionOperation.ViewRevenue => context.MonetizationPermissions.HasFlag(MonetizationPerm.Revenue),
            
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
                if (!context.PublishingPermissions.HasFlag(PublishingPerm.Publish))
                    missing.Add("PublishingPerm.Publish");
                break;
                
            case PermissionOperation.ModerateContent:
                if (!context.ModerationPermissions.HasFlag(ModerationPerm.Review))
                    missing.Add("ModerationPerm.Review");
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