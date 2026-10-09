using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Authorization filter that processes resource permission attributes on controller actions.
///     This filter intercepts requests and validates permissions before the action executes.
/// </summary>
public class ResourcePermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ILogger<ResourcePermissionAuthorizationFilter> _logger;

    public ResourcePermissionAuthorizationFilter(ILogger<ResourcePermissionAuthorizationFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint == null) return;
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) return;

        // Get action descriptor for attribute discovery
        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        if (actionDescriptor == null) return;
        if (actionDescriptor.MethodInfo.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any() ||
            actionDescriptor.ControllerTypeInfo.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var actorContextAccessor = context.HttpContext.RequestServices.GetService<IActorContextAccessor>();
        var permissionQueryService = context.HttpContext.RequestServices.GetService<IPermissionQueryService>();
        if (actorContextAccessor == null || permissionQueryService == null)
        {
            // SECURITY (deny-by-default / fail-safe defaults): missing authorization
            // dependencies are a host misconfiguration, not a bypass route. The request is
            // denied instead of skipped so that no surface can execute without the
            // permission-evaluation pipeline being present.
            _logger.LogError(
                "IActorContextAccessor or IPermissionQueryService not available - denying request (fail-closed)");
            context.Result = new Microsoft.AspNetCore.Mvc.ForbidResult();
            return;
        }

        var actor = actorContextAccessor.ActorContext;

        // Check if user is authenticated
        if (!actor.IsAuthenticated)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        // System admin bypass
        if (actor.IsSystemAdmin)
        {
            return;
        }

        // Get all permission attributes from action and controller
        var attributes = GetPermissionAttributes(actionDescriptor);

        foreach (var attr in attributes)
        {
            var authorized = await CheckPermissionAsync(attr, context.HttpContext, actor, permissionQueryService).ConfigureAwait(false);
            if (!authorized)
            {
                _logger.LogWarning(
                    "Authorization failed for user {UserId} on {Controller}.{Action}",
                    actor.SubjectIdAsGuid,
                    actionDescriptor.ControllerName,
                    actionDescriptor.ActionName);
                context.Result = new Microsoft.AspNetCore.Mvc.ForbidResult();
                return;
            }
        }
    }

    private static IEnumerable<Attribute> GetPermissionAttributes(ControllerActionDescriptor actionDescriptor)
    {
        // Get attributes from action method
        var methodAttributes = actionDescriptor.MethodInfo
            .GetCustomAttributes(true)
            .OfType<Attribute>()
            .Where(IsPermissionAttribute);

        // Get attributes from controller class
        var controllerAttributes = actionDescriptor.ControllerTypeInfo
            .GetCustomAttributes(true)
            .OfType<Attribute>()
            .Where(IsPermissionAttribute);

        return methodAttributes.Concat(controllerAttributes);
    }

    private static bool IsPermissionAttribute(Attribute attr)
    {
        return attr is IResourcePermissionMarker
            || attr is IContentTypePermissionMarker
            || attr is RequireTenantPermissionAttribute
            || attr is RequiresPermissionAttribute
            || attr is RequirePermissionAttribute;
    }

    private async Task<bool> CheckPermissionAsync(
        Attribute attr,
        HttpContext httpContext,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Handle resource permission attributes
        if (attr is IResourcePermissionMarker resourceAttr)
        {
            return await CheckResourcePermissionAsync(resourceAttr, httpContext, actor, permissionQueryService).ConfigureAwait(false);
        }

        // Handle content-type permission attributes
        if (attr is IContentTypePermissionMarker contentTypeAttr)
        {
            return await CheckContentTypePermissionAsync(contentTypeAttr, actor, permissionQueryService).ConfigureAwait(false);
        }

        // Handle tenant permission attributes
        if (attr is RequireTenantPermissionAttribute tenantAttr)
        {
            return await CheckTenantPermissionAsync(tenantAttr.Permission, actor, permissionQueryService).ConfigureAwait(false);
        }

        // Handle simple permission attributes
        if (attr is RequiresPermissionAttribute simpleAttr)
        {
            return actor.TenantId.HasValue && await permissionQueryService.HasTenantPermissionAsync(
                actor.SubjectIdAsGuid!.Value,
                actor.TenantId.Value,
                simpleAttr.PermissionName).ConfigureAwait(false);
        }
        if (attr is RequirePermissionAttribute simpleAttr2)
        {
            return actor.TenantId.HasValue && await permissionQueryService.HasTenantPermissionAsync(
                actor.SubjectIdAsGuid!.Value,
                actor.TenantId.Value,
                simpleAttr2.PermissionName).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<bool> CheckResourcePermissionAsync(
        IResourcePermissionMarker attr,
        HttpContext httpContext,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Extract resource ID from route/query
        var resourceId = ExtractResourceId(httpContext, attr.ResourceIdParameterName);
        if (resourceId == null)
        {
            _logger.LogWarning(
                "Resource ID parameter '{Parameter}' not found in request",
                attr.ResourceIdParameterName);
            return false;
        }

        // Build composite permission name for resource-level check
        var permissionName = $"{attr.ResourceType.Name}.{resourceId}.{attr.RequiredPermission}";
        
        // Check resource-level permission using IPermissionQueryService
        var hasPermission = actor.TenantId.HasValue && await permissionQueryService.HasTenantPermissionAsync(
            actor.SubjectIdAsGuid!.Value,
            actor.TenantId.Value,
            permissionName).ConfigureAwait(false);

        _logger.LogDebug(
            "Resource permission check: User {UserId}, Resource {ResourceType}:{ResourceId}, Permission {Permission} = {Result}",
            actor.SubjectIdAsGuid,
            attr.ResourceType.Name,
            resourceId,
            permissionName,
            hasPermission);

        return hasPermission;
    }

    private async Task<bool> CheckContentTypePermissionAsync(
        IContentTypePermissionMarker attr,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Build permission name from content type and permission
        var permissionName = $"{attr.ResourceType.Name}.{attr.Permission}";
        
        // Check if user has content-type level permission (treated as tenant permission)
        var hasPermission = actor.TenantId.HasValue && await permissionQueryService.HasTenantPermissionAsync(
            actor.SubjectIdAsGuid!.Value,
            actor.TenantId.Value,
            permissionName).ConfigureAwait(false);

        _logger.LogDebug(
            "Content-type permission check: User {UserId}, ContentType {ContentType}, Permission {Permission} = {Result}",
            actor.SubjectIdAsGuid,
            attr.ResourceType.Name,
            attr.Permission,
            hasPermission);

        return hasPermission;
    }

    private async Task<bool> CheckTenantPermissionAsync(
        object permission,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Build permission name
        var permissionName = permission.ToString() ?? "";
        
        // Check if user has tenant-level permission
        var hasPermission = actor.TenantId.HasValue && await permissionQueryService.HasTenantPermissionAsync(
            actor.SubjectIdAsGuid!.Value,
            actor.TenantId.Value,
            permissionName).ConfigureAwait(false);

        _logger.LogDebug(
            "Tenant permission check: User {UserId}, Permission {Permission} = {Result}",
            actor.SubjectIdAsGuid,
            permissionName,
            hasPermission);

        return hasPermission;
    }

    private static Guid? ExtractResourceId(HttpContext httpContext, string parameterName)
    {
        // Try route values first
        if (httpContext.Request.RouteValues.TryGetValue(parameterName, out var routeValue))
        {
            if (Guid.TryParse(routeValue?.ToString(), out var routeGuid))
                return routeGuid;
        }

        // Try query string
        if (httpContext.Request.Query.TryGetValue(parameterName, out var queryValue))
        {
            if (Guid.TryParse(queryValue.FirstOrDefault(), out var queryGuid))
                return queryGuid;
        }

        return null;
    }
}

/// <summary>
///     Extension methods to register the resource permission authorization filter.
/// </summary>
public static class ResourcePermissionAuthorizationExtensions
{
    /// <summary>
    ///     Adds the resource permission authorization filter to MVC.
    /// </summary>
    public static IServiceCollection AddResourcePermissionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<ResourcePermissionAuthorizationFilter>();
        return services;
    }
}
