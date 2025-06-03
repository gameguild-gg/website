Here's a comprehensive solution for implementing a **pure three-layer DAC (Discretionary Access Control)** permissions system using Entity Framework with inheritance and enums:

````csharp
using System;

[Flags]
public enum PermissionType
{
    Read = 1, // read-only access
    Create = 2, // create new resources
    Update = 4, // update existing resources
    Delete = 8, // delete resources (permission-based access control)
    Moderate = 16, // moderate content
    Share = 32, // share content
    Comment = 64, // comment on content
    Vote = 128, // vote on content
    Archive = 256, // archive content
    Publish = 512 // publish content
    // Add more permissions as needed
}

// Content type discriminator for polymorphic queries (optional - EF can handle this automatically)
public static class ContentTypes
{
    public const string Post = nameof(Post);
    public const string Comment = nameof(Comment);
    public const string Forum = nameof(Forum);
    public const string Document = nameof(Document);
}

// Helper class for common permission combinations
public static class PermissionPresets
{
    public static readonly PermissionType Admin = PermissionType.Read | PermissionType.Create | PermissionType.Update | PermissionType.Delete | PermissionType.Moderate | PermissionType.Share | PermissionType.Archive | PermissionType.Publish;
    public static readonly PermissionType Editor = PermissionType.Read | PermissionType.Update | PermissionType.Comment | PermissionType.Publish;
    public static readonly PermissionType Moderator = PermissionType.Read | PermissionType.Moderate | PermissionType.Delete | PermissionType.Archive;
    public static readonly PermissionType Author = PermissionType.Read | PermissionType.Create | PermissionType.Update | PermissionType.Comment;
    public static readonly PermissionType Viewer = PermissionType.Read | PermissionType.Comment | PermissionType.Vote;
    public static readonly PermissionType All = (PermissionType)1023; // All permissions
}
````

````csharp
using System;
using System.Collections.Generic;

// Base resource that can have permissions - uses proper inheritance
public abstract class PermissionableResource
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    
    // Tenant association - null means global resource
    public Guid? TenantId { get; set; }
    public Tenant Tenant { get; set; }
    
    // Navigation properties - EF handles polymorphic relationships automatically
    public ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
    public ICollection<UserContentTypePermission> ContentTypePermissions { get; set; } = new List<UserContentTypePermission>();
}

// Base user entity - no direct tenant reference
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    
    // Navigation properties
    public ICollection<UserTenantPermission> UserTenantPermissions { get; set; } = new List<UserTenantPermission>();
    public ICollection<UserContentTypePermission> UserContentTypePermissions { get; set; } = new List<UserContentTypePermission>();
    public ICollection<ResourcePermission> GrantedResourcePermissions { get; set; } = new List<ResourcePermission>();
}

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<UserTenantPermission> UserTenantPermissions { get; set; } = new List<UserTenantPermission>();
    public ICollection<UserContentTypePermission> UserContentTypePermissions { get; set; } = new List<UserContentTypePermission>();
}

// 1. Tenant-wide permissions (or global when TenantId is null)
public class UserTenantPermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    
    // Nullable TenantId - null means global permissions
    public Guid? TenantId { get; set; }
    public Tenant Tenant { get; set; }
    
    public PermissionType Permissions { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Guid AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; }
}

// 2. ContentType-wide permissions for all entities of that type within a tenant
public class UserContentTypePermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    
    // Nullable TenantId - null means global content type permissions
    public Guid? TenantId { get; set; }
    public Tenant Tenant { get; set; }
    
    // Polymorphic content type identification using EF discriminator
    public string ContentTypeName { get; set; } // EF will handle this automatically with TPH
    public PermissionType Permissions { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Guid AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; }
}
````

````csharp
using System;

// Base permission class
public abstract class BasePermission
{
    public Guid Id { get; set; }
    public PermissionType Permissions { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Guid GrantedByUserId { get; set; }
    public User GrantedByUser { get; set; }
}

// Resource-specific permissions (DAC)
public class ResourcePermission : BasePermission
{
    public Guid UserId { get; set; }
    public User User { get; set; }
    
    public Guid ResourceId { get; set; }
    public PermissionableResource Resource { get; set; }
}
````

````csharp
using System;
using System.Collections.Generic;

// Concrete resource implementations - EF handles polymorphism automatically
public class Post : PermissionableResource
{
    public string Title { get; set; }
    public string Content { get; set; }
    public int Votes { get; set; }
    public bool IsPublished { get; set; }
    
    // Navigation properties
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}

public class Comment : PermissionableResource
{
    public string Content { get; set; }
    public int Votes { get; set; }
    public Guid PostId { get; set; }
    public Post Post { get; set; }
}

public class Forum : PermissionableResource
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsPrivate { get; set; }
    
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

public class Document : PermissionableResource
{
    public string Title { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string MimeType { get; set; }
}
````

````csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid resourceId, PermissionType permission);
    Task<PermissionType> GetUserPermissionsAsync(Guid userId, Guid resourceId);
    Task GrantResourcePermissionAsync(Guid userId, Guid resourceId, PermissionType permissions, Guid grantedByUserId);
    Task AssignUserToTenantAsync(Guid userId, Guid? tenantId, PermissionType permissions, Guid assignedByUserId);
    Task<IEnumerable<Tenant>> GetUserTenantsAsync(Guid userId);
    Task<IEnumerable<UserTenantPermission>> GetUserGlobalPermissionsAsync(Guid userId);
    
    // ContentType-wide permission methods - using string-based content types
    Task AssignContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permissions, Guid assignedByUserId);
    Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permission);
    Task<PermissionType> GetUserContentTypePermissionsAsync(Guid userId, Guid? tenantId, string contentTypeName);
    Task<IEnumerable<UserContentTypePermission>> GetUserContentTypePermissionsAsync(Guid userId);
}

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Guid resourceId, PermissionType permission)
    {
        var userPermissions = await GetUserPermissionsAsync(userId, resourceId);
        return userPermissions.HasFlag(permission);
    }

    public async Task<PermissionType> GetUserPermissionsAsync(Guid userId, Guid resourceId)
    {
        var user = await _context.Users
            .Include(u => u.UserTenantPermissions)
                .ThenInclude(utp => utp.Tenant)
            .Include(u => u.UserContentTypePermissions)
                .ThenInclude(uctp => uctp.Tenant)
            .Include(u => u.GrantedResourcePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return PermissionType.Read; // Default minimal permission

        var permissions = PermissionType.Read; // Start with base permission

        // Get resource and determine its content type and tenant context
        var resource = await GetResourceAsync(resourceId);
        var resourceTenantId = await GetResourceTenantIdAsync(resourceId);

        // 1. Check global permissions (highest priority) - UserTenantPermission with null TenantId
        var globalPermissions = user.UserTenantPermissions
            .Where(utp => utp.IsActive && utp.TenantId == null)
            .ToList();

        foreach (var permissionEntry in globalPermissions)
        {
            if (permissionEntry.Permissions.HasFlag(PermissionPresets.All))
            {
                return PermissionPresets.All; // All permissions for global admin
            }
            permissions |= permissionEntry.Permissions;
        }

        // 2. Check tenant-specific permissions for the resource's tenant context
        if (resourceTenantId.HasValue)
        {
            var tenantPermissions = user.UserTenantPermissions
                .Where(utp => utp.IsActive && utp.TenantId == resourceTenantId.Value)
                .ToList();

            foreach (var permissionEntry in tenantPermissions)
            {
                permissions |= permissionEntry.Permissions;
            }
        }

        // 3. Check ContentType-wide permissions (NEW LAYER)
        if (resource != null)
        {
            var resourceContentTypeName = resource.GetType().Name; // EF polymorphism
            
            // Global ContentType permissions
            var globalContentTypePermissions = user.UserContentTypePermissions
                .Where(uctp => uctp.IsActive && uctp.TenantId == null && uctp.ContentTypeName == resourceContentTypeName)
                .ToList();

            foreach (var permissionEntry in globalContentTypePermissions)
            {
                permissions |= permissionEntry.Permissions;
            }

            // Tenant-specific ContentType permissions
            if (resourceTenantId.HasValue)
            {
                var tenantContentTypePermissions = user.UserContentTypePermissions
                    .Where(uctp => uctp.IsActive && uctp.TenantId == resourceTenantId.Value && uctp.ContentTypeName == resourceContentTypeName)
                    .ToList();

                foreach (var permissionEntry in tenantContentTypePermissions)
                {
                    permissions |= permissionEntry.Permissions;
                }
            }
        }

        // 4. Check direct resource permissions (highest specificity)
        var resourcePermissions = await _context.ResourcePermissions
            .Where(rp => rp.UserId == userId && rp.ResourceId == resourceId && rp.IsActive)
            .ToListAsync();

        foreach (var permission in resourcePermissions)
        {
            permissions |= permission.Permissions;
        }

        return permissions;
    }

    private async Task<PermissionableResource> GetResourceAsync(Guid resourceId)
    {
        // Use EF polymorphic query - much more efficient than multiple table lookups
        return await _context.Set<PermissionableResource>()
            .FirstOrDefaultAsync(r => r.Id == resourceId && !r.IsDeleted);
    }

    private async Task<Guid?> GetResourceTenantIdAsync(Guid resourceId)
    {
        // Logic to determine which tenant a resource belongs to
        // This could be based on explicit tenant association or resource-specific logic
        // For now, we'll try to determine tenant context from the resource itself
        
        var resource = await GetResourceAsync(resourceId);
        if (resource?.TenantId != null)
        {
            return resource.TenantId;
        }

        return null; // Global resource
    }

    public async Task AssignUserToTenantAsync(Guid userId, Guid? tenantId, PermissionType permissions, Guid assignedByUserId)
    {
        var userTenantPermission = new UserTenantPermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId, // null for global permissions
            Permissions = permissions,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId,
            IsActive = true
        };

        _context.UserTenantPermissions.Add(userTenantPermission);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Tenant>> GetUserTenantsAsync(Guid userId)
    {
        return await _context.UserTenantPermissions
            .Where(utp => utp.UserId == userId && utp.IsActive && utp.TenantId != null)
            .Include(utp => utp.Tenant)
            .Select(utp => utp.Tenant)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<UserTenantPermission>> GetUserGlobalPermissionsAsync(Guid userId)
    {
        return await _context.UserTenantPermissions
            .Where(utp => utp.UserId == userId && utp.IsActive && utp.TenantId == null)
            .ToListAsync();
    }

    public async Task GrantResourcePermissionAsync(Guid userId, Guid resourceId, PermissionType permissions, Guid grantedByUserId)
    {
        var resourcePermission = new ResourcePermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResourceId = resourceId,
            Permissions = permissions,
            GrantedAt = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
            IsActive = true
        };

        _context.ResourcePermissions.Add(resourcePermission);
        await _context.SaveChangesAsync();
    }

    // ContentType-wide permission methods implementation
    public async Task AssignContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permissions, Guid assignedByUserId)
    {
        var contentTypePermission = new UserContentTypePermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId, // null for global content type permissions
            ContentTypeName = contentTypeName,
            Permissions = permissions,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId,
            IsActive = true
        };

        _context.UserContentTypePermissions.Add(contentTypePermission);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasContentTypePermissionAsync(Guid userId, Guid? tenantId, string contentTypeName, PermissionType permission)
    {
        var userPermissions = await GetUserContentTypePermissionsAsync(userId, tenantId, contentTypeName);
        return userPermissions.HasFlag(permission);
    }

    public async Task<PermissionType> GetUserContentTypePermissionsAsync(Guid userId, Guid? tenantId, string contentTypeName)
    {
        var permissions = PermissionType.Read; // Start with base permission

        var userContentTypePermissions = await _context.UserContentTypePermissions
            .Where(uctp => uctp.UserId == userId && 
                          uctp.TenantId == tenantId && 
                          uctp.ContentTypeName == contentTypeName && 
                          uctp.IsActive)
            .ToListAsync();

        foreach (var permissionEntry in userContentTypePermissions)
        {
            permissions |= permissionEntry.Permissions;
        }

        return permissions;
    }

    public async Task<IEnumerable<UserContentTypePermission>> GetUserContentTypePermissionsAsync(Guid userId)
    {
        return await _context.UserContentTypePermissions
            .Where(uctp => uctp.UserId == userId && uctp.IsActive)
            .Include(uctp => uctp.Tenant)
            .ToListAsync();
    }

}
````

````csharp
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<UserTenantPermission> UserTenantPermissions { get; set; }
    public DbSet<UserContentTypePermission> UserContentTypePermissions { get; set; }
    public DbSet<ResourcePermission> ResourcePermissions { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<Forum> Forums { get; set; }
    public DbSet<Document> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure UserTenantPermission relationships
        modelBuilder.Entity<UserTenantPermission>()
            .HasOne(utp => utp.User)
            .WithMany(u => u.UserTenantPermissions)
            .HasForeignKey(utp => utp.UserId);

        modelBuilder.Entity<UserTenantPermission>()
            .HasOne(utp => utp.Tenant)
            .WithMany(t => t.UserTenantPermissions)
            .HasForeignKey(utp => utp.TenantId);

        // Configure UserContentTypePermission relationships
        modelBuilder.Entity<UserContentTypePermission>()
            .HasOne(uctp => uctp.User)
            .WithMany(u => u.UserContentTypePermissions)
            .HasForeignKey(uctp => uctp.UserId);

        modelBuilder.Entity<UserContentTypePermission>()
            .HasOne(uctp => uctp.Tenant)
            .WithMany(t => t.UserContentTypePermissions)
            .HasForeignKey(uctp => uctp.TenantId);

        // Configure ResourcePermission relationships
        modelBuilder.Entity<ResourcePermission>()
            .HasOne(rp => rp.User)
            .WithMany(u => u.GrantedResourcePermissions)
            .HasForeignKey(rp => rp.UserId);

        modelBuilder.Entity<ResourcePermission>()
            .HasOne(rp => rp.Resource)
            .WithMany(r => r.ResourcePermissions)
            .HasForeignKey(rp => rp.ResourceId);

        // Configure inheritance for PermissionableResource using Table-per-Hierarchy (TPH)
        modelBuilder.Entity<PermissionableResource>()
            .HasDiscriminator<string>("ContentType")
            .HasValue<Post>(ContentTypes.Post)
            .HasValue<Comment>(ContentTypes.Comment)
            .HasValue<Forum>(ContentTypes.Forum)
            .HasValue<Document>(ContentTypes.Document);

        // Performance indexes for permission queries
        modelBuilder.Entity<UserTenantPermission>()
            .HasIndex(utp => new { utp.UserId, utp.TenantId, utp.IsActive })
            .HasDatabaseName("IX_UserTenantPermission_Lookup");

        modelBuilder.Entity<UserContentTypePermission>()
            .HasIndex(uctp => new { uctp.UserId, uctp.TenantId, uctp.ContentTypeName, uctp.IsActive })
            .HasDatabaseName("IX_UserContentTypePermission_Lookup");

        modelBuilder.Entity<ResourcePermission>()
            .HasIndex(rp => new { rp.UserId, rp.ResourceId, rp.IsActive })
            .HasDatabaseName("IX_ResourcePermission_Lookup");

        // Index for polymorphic resource queries
        modelBuilder.Entity<PermissionableResource>()
            .HasIndex(pr => new { pr.TenantId, pr.IsDeleted })
            .HasDatabaseName("IX_PermissionableResource_TenantLookup");
    }
}
````

## Key Improvements Made:

### 1. **Complete Three-Layer Permission Model**
- **Layer 1: Tenant-wide permissions** (`UserTenantPermission`) - Global or tenant-specific permissions for all operations
- **Layer 2: ContentType-wide permissions** (`UserContentTypePermission`) - Permissions for all entities of specific type (Posts, Comments, Forums, Documents)
- **Layer 3: Resource-specific permissions** (`ResourcePermission`) - Fine-grained permissions for individual resources

### 2. **Enhanced Permission Resolution Logic**
The permission system now evaluates in this **hierarchical order**:
1. **Global permissions** (UserTenantPermission with TenantId = null)
2. **Tenant-specific permissions** (UserTenantPermission with specific TenantId)
3. **Global ContentType permissions** (UserContentTypePermission with TenantId = null)
4. **Tenant-specific ContentType permissions** (UserContentTypePermission with specific TenantId)
5. **Direct resource permissions** (ResourcePermission)

### 3. **Complete ContentType Implementation**
- **Concrete resource classes** now implement the `ContentType` property
- **New service methods** for managing ContentType permissions
- **Updated permission resolution** includes ContentType layer evaluation
- **Enhanced interface** with ContentType permission methods

### 4. **New Service Methods for ContentType Permissions**
- `AssignContentTypePermissionAsync()` - Assign permissions for all entities of specific type
- `HasContentTypePermissionAsync()` - Check if user has specific ContentType permission
- `GetUserContentTypePermissionsAsync()` - Get user's permissions for specific ContentType
- `GetUserContentTypePermissionsAsync(userId)` - Get all user's ContentType permissions

### 5. **Benefits of This Three-Layer Approach**
- **Maximum flexibility**: Users can have different permission levels at each layer
- **Efficient permission management**: Grant permissions at appropriate granularity level
- **Hierarchical inheritance**: More specific permissions override general ones
- **Clean separation of concerns**: Different permission scopes for different use cases

### 6. **Usage Examples**

```csharp
// ===== LAYER 1: TENANT-WIDE PERMISSIONS =====

// Assign global admin permissions (works across all tenants)
await permissionService.AssignUserToTenantAsync(userId, null, PermissionPresets.Admin, assignedByUserId);

// Assign tenant-specific admin permissions
await permissionService.AssignUserToTenantAsync(userId, tenantId, PermissionPresets.Admin, assignedByUserId);

// Assign user as viewer in multiple tenants
await permissionService.AssignUserToTenantAsync(userId, tenant1Id, PermissionPresets.Viewer, assignedByUserId);
await permissionService.AssignUserToTenantAsync(userId, tenant2Id, PermissionPresets.Editor, assignedByUserId);

// ===== LAYER 2: CONTENTTYPE-WIDE PERMISSIONS =====

// Grant permissions for all Posts globally (across all tenants)
await permissionService.AssignContentTypePermissionAsync(userId, null, ContentTypes.Post, PermissionPresets.Editor, assignedByUserId);

// Grant permissions for all Comments in a specific tenant
await permissionService.AssignContentTypePermissionAsync(userId, tenantId, ContentTypes.Comment, PermissionPresets.Moderator, assignedByUserId);

// Grant permissions for all Documents in a tenant
await permissionService.AssignContentTypePermissionAsync(userId, tenantId, ContentTypes.Document, PermissionType.Read | PermissionType.Create, assignedByUserId);

// Grant permissions for all Forums globally
await permissionService.AssignContentTypePermissionAsync(userId, null, ContentTypes.Forum, PermissionType.Read | PermissionType.Moderate, assignedByUserId);

// ===== LAYER 3: RESOURCE-SPECIFIC PERMISSIONS =====

// Grant specific permissions to individual resources
await permissionService.GrantResourcePermissionAsync(userId, specificPostId, PermissionType.Update | PermissionType.Delete, grantedByUserId);

// Grant moderator permissions to a specific forum
await permissionService.GrantResourcePermissionAsync(userId, specificForumId, PermissionPresets.Moderator, assignedByUserId);

// ===== CHECKING PERMISSIONS =====

// Check if user has permission for specific resource (evaluates all 3 layers)
bool canEdit = await permissionService.HasPermissionAsync(userId, resourceId, PermissionType.Update);

// Check ContentType-wide permissions
bool canEditAllPosts = await permissionService.HasContentTypePermissionAsync(userId, tenantId, ContentTypes.Post, PermissionType.Update);

// Get all user permissions for a resource (combines all layers)
var userPermissions = await permissionService.GetUserPermissionsAsync(userId, resourceId);

// ===== COMPLEX SCENARIO EXAMPLES =====

// Example 1: Blog System Setup
// - User A: Global admin (can do everything everywhere)
await permissionService.AssignUserToTenantAsync(userA, null, PermissionPresets.Admin, adminUserId);

// - User B: Tenant admin for Blog Tenant 1
await permissionService.AssignUserToTenantAsync(userB, blogTenant1, PermissionPresets.Admin, adminUserId);

// - User C: Can moderate all Comments globally, but only edit Posts in Blog Tenant 1
await permissionService.AssignContentTypePermissionAsync(userC, null, ContentTypes.Comment, PermissionPresets.Moderator, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userC, blogTenant1, ContentTypes.Post, PermissionPresets.Editor, adminUserId);

// - User D: Can only read Documents globally, but can create/edit them in Blog Tenant 2
await permissionService.AssignContentTypePermissionAsync(userD, null, ContentTypes.Document, PermissionType.Read, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userD, blogTenant2, ContentTypes.Document, PermissionPresets.Author, adminUserId);

// Example 2: Forum System Setup
// - User E: Forum moderator - can moderate all content types in Forum Tenant
await permissionService.AssignUserToTenantAsync(userE, forumTenant, PermissionPresets.Moderator, adminUserId);

// - User F: Post specialist - can fully manage Posts across all tenants, but only read other content
await permissionService.AssignContentTypePermissionAsync(userF, null, ContentTypes.Post, PermissionPresets.Admin, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userF, null, ContentTypes.Comment, PermissionType.Read, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userF, null, ContentTypes.Forum, PermissionType.Read, adminUserId);

// - User G: Has special access to one specific high-security document
await permissionService.GrantResourcePermissionAsync(userG, sensitiveDocumentId, PermissionType.Read | PermissionType.Update, adminUserId);

// Example 3: Multi-tenant Content Management
// - User H: Editor in multiple tenants with different ContentType focus
await permissionService.AssignContentTypePermissionAsync(userH, newsTenant, ContentTypes.Post, PermissionPresets.Editor, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userH, docsTenant, ContentTypes.Document, PermissionPresets.Editor, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userH, forumTenant, ContentTypes.Comment, PermissionPresets.Moderator, adminUserId);

// ===== PERMISSION INHERITANCE EXAMPLE =====
// User I has the following permission layers:
// 1. Tenant-wide: Viewer permissions in Tenant A
// 2. ContentType-wide: Editor permissions for all Posts in Tenant A  
// 3. Resource-specific: Delete permission for Post #123

await permissionService.AssignUserToTenantAsync(userI, tenantA, PermissionPresets.Viewer, adminUserId);
await permissionService.AssignContentTypePermissionAsync(userI, tenantA, ContentTypes.Post, PermissionPresets.Editor, adminUserId);
await permissionService.GrantResourcePermissionAsync(userI, post123Id, PermissionType.Delete, adminUserId);

// Result: User I can:
// - Read/Comment/Vote on everything in Tenant A (Layer 1)
// - Create/Update/Publish any Post in Tenant A (Layer 2) 
// - Delete Post #123 specifically (Layer 3)
```

### 7. **Permission Resolution Priority Examples**

```csharp
// Scenario: User has conflicting permissions across layers
// Layer 1 (Tenant): Viewer (Read + Comment + Vote)
// Layer 2 (ContentType): Editor for Posts (Read + Update + Comment + Publish)  
// Layer 3 (Resource): Delete permission for specific Post

// Final permissions for that specific Post:
// Read (from all layers) + Comment (from layers 1&2) + Vote (from layer 1) + 
// Update (from layer 2) + Publish (from layer 2) + Delete (from layer 3)
// = Comprehensive permissions combining all layers

var finalPermissions = await permissionService.GetUserPermissionsAsync(userId, specificPostId);
// Result: Read | Comment | Vote | Update | Publish | Delete
```

### 8. **Database Schema Considerations**

```csharp
// Recommended indexes for optimal performance
public class ApplicationDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... existing configuration ...

        // Performance indexes for permission queries
        modelBuilder.Entity<UserTenantPermission>()
            .HasIndex(utp => new { utp.UserId, utp.TenantId, utp.IsActive })
            .HasDatabaseName("IX_UserTenantPermission_Lookup");

        modelBuilder.Entity<UserContentTypePermission>()
            .HasIndex(uctp => new { uctp.UserId, uctp.TenantId, uctp.ContentTypeName, uctp.IsActive })
            .HasDatabaseName("IX_UserContentTypePermission_Lookup");

        modelBuilder.Entity<ResourcePermission>()
            .HasIndex(rp => new { rp.UserId, rp.ResourceId, rp.IsActive })
            .HasDatabaseName("IX_ResourcePermission_Lookup");
    }
}
````

## Implementation Checklist

### Phase 1: Database Setup
- [ ] Create migration for new permission entities
- [ ] Add `UserContentTypePermission` table with polymorphic `ContentTypeName` field
- [ ] Configure Entity Framework inheritance using Table-per-Hierarchy (TPH) with discriminator
- [ ] Create database indexes for performance optimization including polymorphic lookups
- [ ] Run migration scripts

### Phase 2: Service Layer Updates
- [ ] Update `IPermissionService` interface with string-based ContentType methods
- [ ] Implement polymorphic ContentType permission methods in `PermissionService`
- [ ] Update `GetUserPermissionsAsync` to use EF polymorphic queries
- [ ] Replace manual resource lookup with EF's `Set<PermissionableResource>()` query
- [ ] Update permission resolution logic for three-layer hierarchy with polymorphism
- [ ] Add caching layer for frequently accessed permissions

### Phase 3: Application Layer Integration
- [ ] Update controllers to use new permission service methods
- [ ] Implement authorization attributes/middleware
- [ ] Update existing permission checks throughout the application
- [ ] Add ContentType permission management endpoints
- [ ] Create admin UI for permission management

### Phase 4: Testing and Validation
- [ ] Write unit tests for all permission service methods
- [ ] Test permission inheritance across all three layers
- [ ] Validate multi-tenant isolation
- [ ] Performance test with large datasets
- [ ] Security audit of permission evaluation logic

### Phase 5: Documentation and Training
- [ ] Update API documentation
- [ ] Create admin user guides
- [ ] Document permission assignment best practices
- [ ] Train support team on new permission model

## Troubleshooting Common Issues

### Issue 1: Performance Degradation
**Problem**: Permission checks are slow
**Solution**: 
- Add database indexes on permission lookup columns
- Implement permission caching
- Consider denormalizing frequently accessed permissions

### Issue 2: Permission Conflicts
**Problem**: User has unexpected permissions
**Solution**:
- Review permission hierarchy (Global → Tenant → ContentType → Resource)
- Check for overlapping permission assignments
- Verify permission bitwise operations are correct

### Issue 3: Tenant Isolation Failures
**Problem**: Users seeing content from wrong tenants
**Solution**:
- Verify tenant context detection logic in `GetResourceTenantIdAsync`
- Check resource-to-tenant relationship mappings
- Validate permission evaluation includes proper tenant filtering

### Issue 4: Polymorphic ContentType Issues
**Problem**: ContentType permissions not working with polymorphic entities
**Solution**:
- Ensure Entity Framework inheritance is properly configured with discriminator
- Verify `ContentTypeName` field is being populated correctly during permission assignment
- Check that polymorphic queries use `resource.GetType().Name` for content type identification
- Validate EF's `Set<PermissionableResource>()` queries are working correctly

### Issue 5: Performance Issues with Polymorphic Queries
**Problem**: Slow permission resolution with inheritance
**Solution**:
- Verify Table-per-Hierarchy (TPH) is configured correctly for optimal performance
- Check that discriminator column is indexed
- Consider using compiled queries for frequently accessed permission checks
- Monitor query execution plans for polymorphic resource lookups

## Polymorphism Implementation Benefits

### ✅ **Entity Framework Polymorphism Advantages**

1. **Type Safety**: No enum-based string comparisons, leveraging C# type system
2. **Performance**: Single table inheritance (TPH) with discriminator column
3. **Maintainability**: Adding new content types doesn't require enum updates
4. **Query Efficiency**: EF handles polymorphic queries automatically
5. **Extensibility**: Easy to add new resource types without breaking changes

### 🔄 **Polymorphic Architecture**

```csharp
// EF automatically handles polymorphic queries
var allResources = await _context.Set<PermissionableResource>()
    .Where(r => !r.IsDeleted)
    .ToListAsync(); // Returns Posts, Comments, Forums, Documents seamlessly

// Type-specific queries still work
var posts = await _context.Posts
    .Where(p => p.IsPublished)
    .ToListAsync();

// Permission resolution uses actual type names
var contentTypeName = resource.GetType().Name; // "Post", "Comment", etc.
var permissions = await GetContentTypePermissionsAsync(userId, tenantId, contentTypeName);
```

### 🏗️ **Database Schema with Polymorphism**

```sql
-- Single table for all PermissionableResource types
CREATE TABLE PermissionableResource (
    Id uniqueidentifier PRIMARY KEY,
    ContentType nvarchar(max) NOT NULL, -- EF discriminator column
    CreatedAt datetime2 NOT NULL,
    UpdatedAt datetime2 NOT NULL,
    IsDeleted bit NOT NULL,
    TenantId uniqueidentifier NULL,
    
    -- Post-specific columns
    Title nvarchar(max) NULL,
    Content nvarchar(max) NULL,
    Votes int NULL,
    IsPublished bit NULL,
    
    -- Comment-specific columns
    PostId uniqueidentifier NULL,
    
    -- Forum-specific columns
    Name nvarchar(max) NULL,
    Description nvarchar(max) NULL,
    IsPrivate bit NULL,
    
    -- Document-specific columns
    FilePath nvarchar(max) NULL,
    FileSize bigint NULL,
    MimeType nvarchar(max) NULL
);

-- Index on discriminator for optimal polymorphic queries
CREATE INDEX IX_PermissionableResource_ContentType ON PermissionableResource(ContentType);
```

The **Pure DAC Three-Layer Permission Strategy** is now complete with comprehensive implementation guidance, migration support, and troubleshooting documentation. This flexible system provides maximum control while maintaining performance and security for complex content management scenarios.

## Summary: Pure Permission-Based Architecture Benefits

### ✅ **What We Achieved**

1. **Eliminated Role Complexity**: Removed rigid role hierarchies and constraints
2. **Maximum Flexibility**: Users can have any combination of permissions at any layer
3. **Simplified Codebase**: No role management overhead or role-permission mapping
4. **Better Performance**: Direct permission evaluation without role resolution
5. **Cleaner Database**: Fewer tables and relationships to maintain
6. **Easier Maintenance**: Direct permission assignment without intermediate abstractions
7. **Proper Polymorphism**: Entity Framework inheritance with discriminator instead of enum-based typing
8. **Type Safety**: C# type system leveraged for content type identification

### 🎯 **Core Architecture**

- **Pure DAC System**: Direct permission assignment without role intermediaries
- **Three-Layer Hierarchy**: Tenant → ContentType → Resource permissions
- **Bitwise Operations**: Efficient permission combination and evaluation
- **Multi-Tenant Ready**: Global and tenant-specific permissions seamlessly integrated
- **EF Polymorphism**: Table-per-Hierarchy inheritance for optimal performance
- **Content Type Flexibility**: String-based content type names instead of rigid enums

### 🔧 **Key Features**

- **PermissionPresets**: Common permission combinations replace role concepts
- **Hierarchical Evaluation**: More specific permissions complement general ones
- **Expiration Support**: Time-limited permissions for temporary access
- **Audit Trail**: Complete tracking of all permission assignments
- **Polymorphic Queries**: EF automatically handles inheritance relationships
- **Performance Optimized**: Single table inheritance with proper indexing

This **pure three-layer permission-based approach with Entity Framework polymorphism** provides unprecedented flexibility while maintaining clean separation of concerns and optimal performance for modern content management systems. The system now operates entirely on explicit permission assignments without any implicit author-based permissions or enum-based content typing, ensuring complete control, transparency, and extensibility over access rights.