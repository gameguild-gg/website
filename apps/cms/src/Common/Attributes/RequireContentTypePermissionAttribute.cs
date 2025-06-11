using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using cms.Common.Services;
using cms.Common.Entities;

namespace cms.Common.Attributes;

/// <summary>
/// Generic attribute for content-type (table) level permission checks. Validates that the user 
/// has the specified permission for the given content type, with hierarchical fallback to tenant-level.
/// </summary>
/// <typeparam name="T">The entity type representing the content-type/table</typeparam>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireContentTypePermissionAttribute<T> : Attribute, IAsyncAuthorizationFilter where T : class
{
    private readonly PermissionType _requiredPermission;

    public RequireContentTypePermissionAttribute(PermissionType requiredPermission)
    {
        _requiredPermission = requiredPermission;
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

        // Get content type name from generic type parameter
        var contentTypeName = typeof(T).Name;

        // Check content-type level permission with hierarchical fallback
        var hasPermission = await permissionService.HasContentTypePermissionAsync(userId, tenantId, contentTypeName, _requiredPermission);
        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}
