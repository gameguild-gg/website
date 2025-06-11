using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using cms.Common.Services;
using cms.Common.Entities;

namespace cms.Common.Attributes;

/// <summary>
/// Attribute for tenant-level permission checks. Validates that the user has the specified 
/// permission at the tenant level based on their JWT token.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireTenantPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly PermissionType _requiredPermission;

    public RequireTenantPermissionAttribute(PermissionType requiredPermission)
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

        // Check tenant-level permission
        var hasPermission = await permissionService.HasTenantPermissionAsync(userId, tenantId, _requiredPermission);
        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}
