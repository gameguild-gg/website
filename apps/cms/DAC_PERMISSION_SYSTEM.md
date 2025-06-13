# Discretionary Access Control (DAC) Permission System

## Overview

This document describes the implementation of a 3-layer Discretionary Access Control (DAC) permission system for the multi-tenant CMS. The system provides hierarchical permission checks across three layers: **Tenant**, **Content-Type**, and **Resource**, using `PermissionType` flags for granular access control.

## Architecture

### Permission Layers

The DAC system operates on three distinct layers with hierarchical permission checking:

1. **Tenant Level** - Permissions granted to users within a specific tenant
2. **Content-Type Level** - Permissions granted for specific entity types (tables) within a tenant
3. **Resource Level** - Permissions granted for individual resource instances

### Hierarchical Permission Logic

Permission checks follow a hierarchical fallback pattern:
- **Resource-level** checks: Resource → Content-Type → Tenant
- **Content-type-level** checks: Content-Type → Tenant  
- **Tenant-level** checks: Tenant only

If a permission is not found at a specific layer, the system automatically checks the next higher layer.

### Permission Types

The system uses `PermissionType` flags (not roles) for granular permission control:

```csharp
public enum PermissionType
{
    None = 0,
    Create = 1,
    Read = 2,
    Update = 3,
    Delete = 4,
    Review = 5,
    Approve = 6,
    Publish = 7,
    Moderate = 8,
    Export = 9,
    Import = 10,
    ...
}
```

The PermissionType is converted in multiple integer

## Data Model

### Base Permission Entity

```csharp
public abstract class WithPermissions
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public PermissionType PermissionType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

### Generic Resource Permission

```csharp
public class ResourcePermission<T> : WithPermissions where T : class
{
    public Guid ResourceId { get; set; }
    public string ResourceType { get; set; } = typeof(T).Name;
}
```

### Concrete Implementation Example

```csharp
public class CommentPermission : ResourcePermission<Comment>
{
    // Inherits all properties from ResourcePermission<Comment>
    // ResourceType automatically set to "Comment"
}
```

## Authorization Attributes

### 1. Tenant-Level Permission Attribute

For operations that require tenant-wide permissions:

```csharp
[RequireTenantPermission(PermissionType.Create)]
public async Task<ActionResult> CreateTenantSettings([FromBody] TenantSettingsDto dto)
{
    // Implementation
}
```

**Usage Pattern:**
- Used for tenant-wide operations
- Extracts user and tenant context from JWT token
- Checks only tenant-level permissions

### 2. Content-Type Permission Attribute

For operations on specific entity types (tables):

```csharp
[RequireContentTypePermission<Comment>(PermissionType.Read)]
public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments()
{
    // Implementation
}

[RequireContentTypePermission<Comment>(PermissionType.Create)]
public async Task<ActionResult<CommentDto>> CreateComment([FromBody] CreateCommentDto dto)
{
    // Implementation
}

[RequireContentTypePermission<Comment>(PermissionType.Review)]
public async Task<ActionResult<IEnumerable<CommentDto>>> GetCommentsForReview()
{
    // Implementation
}
```

**Usage Pattern:**
- Used for operations on entity collections or types
- Generic template parameter specifies the entity type
- Checks content-type permissions with tenant fallback
- Extracts user and tenant context from JWT token

### 3. Resource-Level Permission Attribute

For operations on specific resource instances:

```csharp
[RequireResourcePermission<Comment>(PermissionType.Read)]
public async Task<ActionResult<CommentDto>> GetComment(Guid id)
{
    // Implementation
}

[RequireResourcePermission<Comment>(PermissionType.Update)]
public async Task<ActionResult<CommentDto>> UpdateComment(Guid id, [FromBody] UpdateCommentDto dto)
{
    // Implementation
}

[RequireResourcePermission<Comment>(PermissionType.Delete)]
public async Task<ActionResult> DeleteComment(Guid id)
{
    // Implementation
}

[RequireResourcePermission<Comment>(PermissionType.Approve)]
public async Task<ActionResult<CommentDto>> ApproveComment(Guid id)
{
    // Implementation
}
```

**Usage Pattern:**
- Used for operations on specific resource instances
- Generic template parameter specifies the entity type
- Resource ID extracted from route parameter (default: "id")
- Checks resource permissions with content-type and tenant fallback
- Extracts user and tenant context from JWT token

### Custom Resource ID Parameter

For routes with non-standard parameter names:

```csharp
[RequireResourcePermission<Comment>(PermissionType.Update, "commentId")]
public async Task<ActionResult<CommentDto>> UpdateCommentById(Guid commentId, [FromBody] UpdateCommentDto dto)
{
    // Implementation
}
```

## Permission Service Interface

The `IPermissionService` provides methods for each permission layer:

```csharp
public interface IPermissionService
{
    // Tenant-level permissions
    Task<bool> HasTenantPermissionAsync(Guid userId, Guid tenantId, PermissionType permission);
    Task GrantTenantPermissionAsync(Guid userId, Guid tenantId, PermissionType permission);
    Task RevokeTenantPermissionAsync(Guid userId, Guid tenantId, PermissionType permission);

    // Content-type permissions with hierarchical fallback
    Task<bool> HasContentTypePermissionAsync<T>(Guid userId, Guid tenantId, PermissionType permission) where T : class;
    Task GrantContentTypePermissionAsync<T>(Guid userId, Guid tenantId, PermissionType permission) where T : class;
    Task RevokeContentTypePermissionAsync<T>(Guid userId, Guid tenantId, PermissionType permission) where T : class;

    // Resource permissions with hierarchical fallback
    Task<bool> HasResourcePermissionAsync<T>(Guid userId, Guid tenantId, Guid resourceId, PermissionType permission) where T : class;
    Task GrantResourcePermissionAsync<T>(Guid userId, Guid tenantId, Guid resourceId, PermissionType permission) where T : class;
    Task RevokeResourcePermissionAsync<T>(Guid userId, Guid tenantId, Guid resourceId, PermissionType permission) where T : class;
}
```

## Controller Implementation Examples

### Tenant Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    [HttpPost("settings")]
    [RequireTenantPermission(PermissionType.Create)]
    public async Task<ActionResult> CreateTenantSettings([FromBody] TenantSettingsDto dto)
    {
        // Implementation
    }

    [HttpGet("users")]
    [RequireTenantPermission(PermissionType.Read)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetTenantUsers()
    {
        // Implementation
    }
}
```

### Comment Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class CommentController : ControllerBase
{
    // Content-type level operations
    [HttpGet]
    [RequireContentTypePermission<Comment>(PermissionType.Read)]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments()
    {
        // Implementation
    }

    [HttpPost]
    [RequireContentTypePermission<Comment>(PermissionType.Create)]
    public async Task<ActionResult<CommentDto>> CreateComment([FromBody] CreateCommentDto dto)
    {
        // Implementation
    }

    [HttpGet("pending-review")]
    [RequireContentTypePermission<Comment>(PermissionType.Review)]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetCommentsForReview()
    {
        // Implementation
    }

    // Resource-level operations
    [HttpGet("{id}")]
    [RequireResourcePermission<Comment>(PermissionType.Read)]
    public async Task<ActionResult<CommentDto>> GetComment(Guid id)
    {
        // Implementation
    }

    [HttpPut("{id}")]
    [RequireResourcePermission<Comment>(PermissionType.Update)]
    public async Task<ActionResult<CommentDto>> UpdateComment(Guid id, [FromBody] UpdateCommentDto dto)
    {
        // Implementation
    }

    [HttpDelete("{id}")]
    [RequireResourcePermission<Comment>(PermissionType.Delete)]
    public async Task<ActionResult> DeleteComment(Guid id)
    {
        // Implementation
    }

    [HttpPost("{id}/approve")]
    [RequireResourcePermission<Comment>(PermissionType.Approve)]
    public async Task<ActionResult<CommentDto>> ApproveComment(Guid id)
    {
        // Implementation
    }
}
```

## Best Practices

### 1. Choose the Right Permission Level

- **Tenant-level**: Use for operations that affect the entire tenant (user management, tenant settings)
- **Content-type-level**: Use for operations on entity collections (list all comments, create new comment)
- **Resource-level**: Use for operations on specific instances (get/update/delete specific comment)

### 2. Permission Granularity

Use specific permission types rather than broad permissions:

```csharp
// Good - Specific permission for the operation
[RequireContentTypePermission<Comment>(PermissionType.Review)]
public async Task<ActionResult> GetCommentsForReview() { }

// Avoid - Overly broad permission
[RequireContentTypePermission<Comment>(PermissionType.All)]
public async Task<ActionResult> GetCommentsForReview() { }
```

### 3. Generic Type Safety

Always use the correct generic type parameter:

```csharp
// Good - Type matches the entity being operated on
[RequireContentTypePermission<Comment>(PermissionType.Read)]
public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments() { }

// Wrong - Type doesn't match the entity
[RequireContentTypePermission<User>(PermissionType.Read)]
public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments() { }
```

### 4. Context from JWT Token

The system automatically extracts user and tenant context from JWT tokens:
- `ClaimTypes.NameIdentifier` for user ID
- `"tenant_id"` claim for tenant ID
- Resource ID comes from route parameters, not JWT

### 5. Hierarchical Permission Design

Design permissions with hierarchy in mind:
- Grant broader permissions at higher levels for administrative users
- Grant specific permissions at lower levels for regular users
- The system will automatically check fallback layers

## Implementation Notes

### Database Schema

The system uses a single `WithPermissions` base class with concrete implementations for each resource type:

- `TenantPermissions` table for tenant-level permissions
- `ContentTypePermissions` table for content-type permissions  
- `CommentPermissions` table for comment resource permissions
- Additional `{Entity}Permissions` tables for other resource types

### Performance Considerations

- Permission checks are cached at the service level
- Hierarchical queries are optimized with proper indexing
- Generic type information is resolved at compile time

### Security Considerations

- All permission checks use user/tenant context from JWT tokens
- Resource IDs are validated before permission checks
- Failed permission checks return appropriate HTTP status codes (401/403)
- No sensitive information is leaked in error responses

## Future Extensions

### Adding New Resource Types

1. Create a new permission entity:
```csharp
public class ArticlePermission : ResourcePermission<Article>
{
    // Automatically inherits all required properties
}
```

2. Add to DbContext:
```csharp
public DbSet<ArticlePermission> ArticlePermissions { get; set; }
```

3. Use in controllers:
```csharp
[RequireResourcePermission<Article>(PermissionType.Read)]
public async Task<ActionResult<ArticleDto>> GetArticle(Guid id) { }
```

### Custom Permission Types

Add new permission types to the `PermissionType` enum:

```csharp
[Flags]
public enum PermissionType
{
    // ... existing permissions ...
    CustomPermission = 1024,
    All = Create | Read | Update | Delete | Review | Approve | Publish | Moderate | Export | Import | CustomPermission
}
```

This DAC system provides a clean, type-safe, and hierarchical approach to permission management while maintaining excellent developer experience through generic templates and minimal configuration requirements.
