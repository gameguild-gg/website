using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using GameGuild.Common.Services;
using GameGuild.Common.Entities;

namespace GameGuild.Common.Attributes;

/// <summary>
/// Generic attribute for resource-level permission checks. This implementation provides
/// hierarchical permission checking: Resource → Content-Type → Tenant
/// For now, it implements the fallback chain until resource-specific checking is fully implemented.
/// </summary>
/// <typeparam name="TResource">The entity type representing the resource</typeparam>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireResourcePermissionAttribute<TResource> : Attribute, IAsyncAuthorizationFilter where TResource : BaseEntity
{
    private readonly PermissionType _requiredPermission;
    private readonly string _resourceIdParameterName;

    public RequireResourcePermissionAttribute(PermissionType requiredPermission, string resourceIdParameterName = "id")
    {
        _requiredPermission = requiredPermission;
        _resourceIdParameterName = resourceIdParameterName;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        
        // Extract user ID and tenant ID from JWT token
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var tenantIdClaim = context.HttpContext.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Extract resource ID from route parameters
        var resourceIdValue = context.RouteData.Values[_resourceIdParameterName]?.ToString();
        if (!Guid.TryParse(resourceIdValue, out var resourceId))
        {
            context.Result = new BadRequestResult();
            return;
        }

        // Hierarchical permission checking: Resource → Content-Type → Tenant
        
        // TODO: Step 1 - Check resource-level permission (when fully implemented)
        // For Comment resources, we can use the specific implementation
        if (typeof(TResource).Name == "Comment")
        {
            try
            {
                // Use the specific Comment resource permission checking
                var hasResourcePermission = await permissionService.HasResourcePermissionAsync<GameGuild.Modules.Comment.Models.CommentPermission, Comment>(
                    userId, tenantId, resourceId, _requiredPermission);
                    
                if (hasResourcePermission)
                {
                    return; // Permission granted at resource level
                }
            }
            catch
            {
                // If resource-level checking fails, continue to content-type fallback
            }
        }
        
        // Step 2 - Check content-type level permission (fallback)
        var contentTypeName = typeof(TResource).Name;
        var hasContentTypePermission = await permissionService.HasContentTypePermissionAsync(
            userId, tenantId, contentTypeName, _requiredPermission);
            
        if (hasContentTypePermission)
        {
            return; // Permission granted at content-type level
        }

        // Step 3 - Check tenant-level permission (final fallback)
        var hasTenantPermission = await permissionService.HasTenantPermissionAsync(
            userId, tenantId, _requiredPermission);
        
        if (!hasTenantPermission)
        {
            context.Result = new ForbidResult();
        }
        
        // If we reach here with tenant permission, access is granted
    }
}
