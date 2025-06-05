# Permission System Usage Examples

This document provides practical examples of how to use the new modular permission system.

## Quick Start

### Creating Permission Contexts

```csharp
using cms.Common.Entities;
using cms.Modules.Permission.Helpers;

// Method 1: Using builder pattern
var editorContext = PermissionContextBuilder
    .CreateForEditor()
    .WithAdvancedMonetization()
    .Build();

// Method 2: Using presets
var moderatorContext = PermissionPresets.Moderator;

// Method 3: Manual construction
var customContext = new UnifiedPermissionContext
{
    CorePermissions = CorePermissionType.ReadWrite,
    InteractionPermissions = ContentInteractionPermissionType.BasicInteraction,
    ModerationPermissions = ModerationPermissionType.BasicModeration
};
```

### Permission Validation

```csharp
// Check if user can perform specific operations
bool canPublish = PermissionValidator.CanPerformOperation(
    userContext, 
    PermissionOperation.PublishContent
);

// Role-based validation
bool canBanUsers = PermissionValidator.CanPerformRoleOperation(
    userContext, 
    UserRole.Moderator, 
    PermissionOperation.BanUser
);

// Get missing permissions
var missingPermissions = PermissionValidator.GetMissingPermissions(
    userContext, 
    PermissionOperation.MonetizeContent
);
```

## Real-World Scenarios

### 1. News Website

#### Content Creator Role
```csharp
var journalistContext = new UnifiedPermissionContext
{
    CorePermissions = CorePermissionType.ReadWrite,
    InteractionPermissions = ContentInteractionPermissionType.BasicInteraction,
    LifecyclePermissions = ContentLifecyclePermissionType.BasicLifecycle,
    EditorialPermissions = EditorialPermissionType.Edit | EditorialPermissionType.FactCheck
};
```

#### Chief Editor Role
```csharp
var chiefEditorContext = PermissionContextBuilder
    .CreateForEditor()
    .WithPromotionPermissions(PromotionPermissionType.All)
    .WithMonetizationPermissions(MonetizationPermissionType.BasicMonetization)
    .Build();
```

### 2. Gaming Community Platform

#### Regular Player
```csharp
var playerContext = PermissionPresets.Gaming.Player;

// Check if player can comment on strategies
if (playerContext.InteractionPermissions.HasFlag(ContentInteractionPermissionType.Comment))
{
    // Allow commenting
}
```

#### Guild Leader
```csharp
var guildLeaderContext = PermissionPresets.Gaming.GuildLeader;

// Validate guild management operations
bool canModerateGuild = PermissionValidator.CanPerformOperation(
    guildLeaderContext,
    PermissionOperation.ModerateContent
);
```

### 3. E-commerce Content Hub

#### SEO Specialist
```csharp
var seoContext = new UnifiedPermissionContext
{
    CorePermissions = CorePermissionType.Read | CorePermissionType.Update,
    CurationPermissions = ContentCurationPermissionType.All,
    EditorialPermissions = EditorialPermissionType.SEO | EditorialPermissionType.Guidelines,
    QualityPermissions = QualityControlPermissionType.QualityAnalytics
};
```

#### Marketing Manager
```csharp
var marketingContext = PermissionContextBuilder
    .CreateForContributor()
    .WithPromotionPermissions(PromotionPermissionType.All)
    .WithPublishingPermissions(PublishingPermissionType.ExternalDistribution)
    .WithMonetizationPermissions(MonetizationPermissionType.AdvancedMonetization)
    .Build();
```

## Migration Examples

### Migrating from Legacy PermissionType

```csharp
using cms.Modules.Permission.Services;

var migrationService = new PermissionMigrationService();

// Legacy permission
var legacyPermission = PermissionType.Read | PermissionType.Create | PermissionType.Update;

// Migrate to new system
var newContext = migrationService.MigrateFromLegacy(legacyPermission);

// Generate migration report
var report = migrationService.GenerateMigrationReport(legacyPermission);
if (report.HasIssues)
{
    foreach (var issue in report.ValidationIssues)
    {
        Console.WriteLine($"Migration issue: {issue}");
    }
}
```

## Advanced Usage Patterns

### Dynamic Permission Assignment

```csharp
public class UserPermissionService
{
    public UnifiedPermissionContext GetUserPermissions(User user)
    {
        var builder = new PermissionContextBuilder();
        
        // Base permissions based on user level
        switch (user.Level)
        {
            case UserLevel.Beginner:
                builder.WithRead().WithBasicInteraction();
                break;
            case UserLevel.Intermediate:
                builder.WithWrite().WithAllInteractions().WithBasicCuration();
                break;
            case UserLevel.Advanced:
                builder.WithFullCrud().WithAllInteractions().WithAdvancedCuration();
                break;
        }
        
        // Add role-specific permissions
        foreach (var role in user.Roles)
        {
            var rolePermissions = PermissionPresets.GetForRole(role);
            builder = MergePermissions(builder, rolePermissions);
        }
        
        // Add subscription-based permissions
        if (user.HasPremiumSubscription)
        {
            builder.WithBasicMonetization();
        }
        
        return builder.Build();
    }
}
```

### Content-Based Permission Checks

```csharp
public class ContentService
{
    public bool CanUserEditContent(UnifiedPermissionContext userPermissions, Content content)
    {
        // Basic edit permission check
        if (!userPermissions.CorePermissions.HasFlag(CorePermissionType.Update))
            return false;
        
        // Content state checks
        if (content.State == ContentState.Published && 
            !userPermissions.EditorialPermissions.HasFlag(EditorialPermissionType.Edit))
            return false;
        
        // Monetized content checks
        if (content.IsMonetized && 
            !userPermissions.MonetizationPermissions.HasFlag(MonetizationPermissionType.Monetize))
            return false;
        
        return true;
    }
}
```

### Permission-Based UI Rendering

```csharp
public class UIPermissionHelper
{
    public bool ShouldShowEditButton(UnifiedPermissionContext permissions, Content content)
    {
        return permissions.CorePermissions.HasFlag(CorePermissionType.Update) &&
               CanEditContentType(permissions, content.Type);
    }
    
    public bool ShouldShowModerateMenu(UnifiedPermissionContext permissions)
    {
        return permissions.ModerationPermissions != ModerationPermissionType.None;
    }
    
    public bool ShouldShowMonetizationOptions(UnifiedPermissionContext permissions)
    {
        return permissions.MonetizationPermissions.HasFlag(MonetizationPermissionType.Monetize);
    }
}
```

## Best Practices

### 1. Use Builder Pattern for Complex Permissions
```csharp
// Good: Clear and readable
var permissions = PermissionContextBuilder
    .CreateForEditor()
    .WithAdvancedMonetization()
    .WithQualityAnalytics()
    .Build();

// Avoid: Hard to read and maintain
var permissions = new UnifiedPermissionContext
{
    CorePermissions = CorePermissionType.All,
    MonetizationPermissions = MonetizationPermissionType.AdvancedMonetization,
    // ... many more lines
};
```

### 2. Use Presets for Common Roles
```csharp
// Good: Use existing presets
var moderatorPermissions = PermissionPresets.Moderator;

// Good: Extend presets when needed
var seniorModeratorPermissions = PermissionContextBuilder
    .CreateForModerator()
    .WithBasicMonetization()
    .Build();
```

### 3. Validate Permissions Early
```csharp
public async Task<IActionResult> PublishContent(int contentId)
{
    var userPermissions = GetCurrentUserPermissions();
    
    if (!PermissionValidator.CanPerformOperation(userPermissions, PermissionOperation.PublishContent))
    {
        return Forbid("Insufficient permissions to publish content");
    }
    
    // Proceed with publishing logic
    // ...
}
```

### 4. Log Permission Checks for Auditing
```csharp
public class AuditablePermissionValidator
{
    private readonly ILogger<AuditablePermissionValidator> _logger;
    
    public bool ValidateAndLog(UnifiedPermissionContext context, PermissionOperation operation, string userId)
    {
        var result = PermissionValidator.CanPerformOperation(context, operation);
        
        _logger.LogInformation(
            "Permission check: User {UserId} {Result} for operation {Operation}",
            userId,
            result ? "ALLOWED" : "DENIED",
            operation
        );
        
        return result;
    }
}
```

This modular permission system provides the flexibility to handle complex permission scenarios while maintaining clean, readable code.
