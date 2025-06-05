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
            PermissionOperation.ReadContent => context.CanRead,
            PermissionOperation.CreateContent => context.CorePermissions.HasFlag(CorePermissionType.Create),
            PermissionOperation.UpdateContent => context.CorePermissions.HasFlag(CorePermissionType.Update),
            PermissionOperation.DeleteContent => context.CorePermissions.HasFlag(CorePermissionType.Delete),
            
            PermissionOperation.CommentOnContent => context.InteractionPermissions.HasFlag(ContentInteractionPermissionType.Comment),
            PermissionOperation.VoteOnContent => context.InteractionPermissions.HasFlag(ContentInteractionPermissionType.Vote),
            PermissionOperation.ShareContent => context.InteractionPermissions.HasFlag(ContentInteractionPermissionType.Share),
            
            PermissionOperation.ModerateContent => context.ModerationPermissions.HasFlag(ModerationPermissionType.Review),
            PermissionOperation.BanUser => context.ModerationPermissions.HasFlag(ModerationPermissionType.Ban),
            
            PermissionOperation.PublishContent => context.PublishingPermissions.HasFlag(PublishingPermissionType.Publish),
            PermissionOperation.ScheduleContent => context.PublishingPermissions.HasFlag(PublishingPermissionType.Schedule),
            
            PermissionOperation.FeatureContent => context.PromotionPermissions.HasFlag(PromotionPermissionType.Feature),
            PermissionOperation.PinContent => context.PromotionPermissions.HasFlag(PromotionPermissionType.Pin),
            
            PermissionOperation.MonetizeContent => context.MonetizationPermissions.HasFlag(MonetizationPermissionType.Monetize),
            PermissionOperation.ViewRevenue => context.MonetizationPermissions.HasFlag(MonetizationPermissionType.Revenue),
            
            _ => false
        };
    }
    
    /// <summary>
    /// Validates permissions for role-based operations
    /// </summary>
    public static bool CanPerformRoleOperation(UnifiedPermissionContext context, UserRole role, PermissionOperation operation)
    {
        // First check if the context has the required permission
        if (!CanPerformOperation(context, operation))
            return false;
            
        // Then check role-specific restrictions
        return operation switch
        {
            PermissionOperation.BanUser => role is UserRole.Admin or UserRole.Moderator,
            PermissionOperation.ViewRevenue => role is UserRole.Admin or UserRole.BusinessManager,
            PermissionOperation.MonetizeContent => role is UserRole.Admin or UserRole.ContentCreator or UserRole.BusinessManager,
            PermissionOperation.FeatureContent => role is UserRole.Admin or UserRole.Editor or UserRole.CommunityManager,
            _ => true // No additional role restrictions for other operations
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
            case PermissionOperation.ReadContent:
                if (!context.CorePermissions.HasFlag(CorePermissionType.Read))
                    missing.Add("CorePermissionType.Read");
                break;
                
            case PermissionOperation.CreateContent:
                if (!context.CorePermissions.HasFlag(CorePermissionType.Create))
                    missing.Add("CorePermissionType.Create");
                break;
                
            case PermissionOperation.UpdateContent:
                if (!context.CorePermissions.HasFlag(CorePermissionType.Update))
                    missing.Add("CorePermissionType.Update");
                break;
                
            case PermissionOperation.DeleteContent:
                if (!context.CorePermissions.HasFlag(CorePermissionType.Delete))
                    missing.Add("CorePermissionType.Delete");
                break;
                
            case PermissionOperation.PublishContent:
                if (!context.PublishingPermissions.HasFlag(PublishingPermissionType.Publish))
                    missing.Add("PublishingPermissionType.Publish");
                break;
                
            case PermissionOperation.ModerateContent:
                if (!context.ModerationPermissions.HasFlag(ModerationPermissionType.Review))
                    missing.Add("ModerationPermissionType.Review");
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
    // Core CRUD operations
    ReadContent,
    CreateContent,
    UpdateContent,
    DeleteContent,
    
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

/// <summary>
/// User roles for role-based permission validation
/// </summary>
public enum UserRole
{
    Guest,
    Reader,
    ContentCreator,
    Contributor,
    Editor,
    Moderator,
    CommunityManager,
    BusinessManager,
    Admin
}
