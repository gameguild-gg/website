using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Authorization filter that processes DAC permission attributes on controller actions
///     (issues #300/#320/#321). This filter intercepts requests and validates permissions
///     before the action executes.
/// </summary>
/// <remarks>
///     <para>
///         <b>Scoped-store resolution (issues #360/#365).</b> Content-type and resource
///         permission markers no longer resolve tenant-composite permission names through
///         the tenant layer: every scoped requirement is resolved through
///         <see cref="IDacPermissionResolver"/>, which routes the content-type layer
///         through the engine's content-type grant store and the resource layer through
///         the resource grant context (activating owner override and temporal precedence,
///         issues #372/#363). One canonical engine pass — DENY-WINS, deny-by-default,
///         fail-closed.
///     </para>
///     <para>
///         <b>Attribute family.</b> Attributes implementing
///         <see cref="IDacPermissionAttribute"/> (including the extensible
///         <see cref="DacPermissionAttributeBase"/> for custom attributes) are enforced
///         generically; the legacy marker interfaces keep working and are mapped onto the
///         same resolver paths. Simple tenant permission attributes
///         (<see cref="RequiresPermissionAttribute"/> /
///         <see cref="RequirePermissionAttribute"/>) keep their engine-backed tenant
///         check. Multiple attributes on one action compose with AND logic — action-level
///         and controller-level requirements all apply (most-specific wins: the stricter
///         conjunction always governs).
///     </para>
/// </remarks>
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
        if (endpoint == null)
        {
            return;
        }

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return;
        }

        // Get action descriptor for attribute discovery
        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        if (actionDescriptor == null)
        {
            return;
        }

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
            context.Result = new ForbidResult();
            return;
        }

        var actor = actorContextAccessor.ActorContext;

        // Check if user is authenticated
        if (!actor.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // System admin bypass
        if (actor.IsSystemAdmin)
        {
            return;
        }

        // Get all permission attributes from action and controller (action first, then
        // controller: AND across everything, most-specific requirement governs).
        var attributes = GetPermissionAttributes(actionDescriptor);

        foreach (var attr in attributes)
        {
            // Conditional application (issue #321): an attribute whose ShouldApply returns
            // false does not contribute its requirement for this request. Only the
            // extensible IDacPermissionAttribute family supports the hook.
            if (attr is DacPermissionAttributeBase declarative && !declarative.ShouldApply(context.HttpContext))
            {
                continue;
            }

            var check = await CheckPermissionAsync(attr, context.HttpContext, actor, permissionQueryService).ConfigureAwait(false);

            // Durable decision audit for endpoint-level permission checks (issue #346):
            // granted AND denied evaluations are recorded in the permission audit trail so
            // access to sensitive surfaces (monetization, analytics) is traceable.
            if (attr is RequiresPermissionAttribute or RequirePermissionAttribute)
            {
                await AuditSimplePermissionDecisionAsync(context, actionDescriptor, actor, attr, check.Authorized).ConfigureAwait(false);
            }

            if (!check.Authorized)
            {
                _logger.LogWarning(
                    "Authorization failed for user {UserId} on {Controller}.{Action}: {Reason}",
                    actor.SubjectIdAsGuid,
                    actionDescriptor.ControllerName,
                    actionDescriptor.ActionName,
                    check.Reason ?? "permission_denied");
                context.Result = CreateDenialResult(context.HttpContext, check, attr);
                return;
            }
        }
    }

    /// <summary>
    ///     Builds the fail-closed denial result (issue #321: structured error responses
    /// with specific permission failure details; diagnostic information for development
    /// environments only — production responses never leak which permission was missing
    /// beyond the stable machine reason).
    /// </summary>
    private static IActionResult CreateDenialResult(HttpContext httpContext, PermissionCheckOutcome check, Attribute attr)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Permission denied",
            Detail = "The authenticated actor does not satisfy the required permission.",
            Type = "https://gameguild.dev/problems/permission-denied"
        };
        problem.Extensions["reason"] = check.Reason ?? "permission_denied";

        var environment = httpContext.RequestServices.GetService<IHostEnvironment>();
        if (environment?.IsDevelopment() == true)
        {
            // Development diagnostics only: name the failing attribute and its declared
            // requirements so declarative rules can be debugged. Never in production.
            problem.Extensions["attribute"] = attr.GetType().Name;
            if (attr is IDacPermissionAttribute dac)
            {
                problem.Extensions["requiredPermissions"] = dac.RequiredPermissions;
                problem.Extensions["layer"] = dac.Layer.ToString();
            }
        }

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }

    /// <summary>
    ///     Records a <see cref="PermissionOperationType.Check"/> entry in the permission audit
    ///     trail for an endpoint-level permission decision. Best-effort: an audit failure must
    ///     never change the authorization outcome.
    /// </summary>
    private async Task AuditSimplePermissionDecisionAsync(
        AuthorizationFilterContext context,
        ControllerActionDescriptor actionDescriptor,
        ActorContext actor,
        Attribute attr,
        bool authorized)
    {
        var auditService = context.HttpContext.RequestServices.GetService<IPermissionAuditService>();
        if (auditService == null)
        {
            return;
        }

        var permissionName = attr switch
        {
            RequiresPermissionAttribute simple => simple.PermissionName,
            RequirePermissionAttribute alias => alias.PermissionName,
            _ => null
        };
        if (string.IsNullOrEmpty(permissionName))
        {
            return;
        }

        var actorUserId = actor.SubjectIdAsGuid;
        try
        {
            await auditService.LogPermissionChangeAsync(
                operationType: PermissionOperationType.Check,
                userId: actorUserId,
                performedBy: actorUserId ?? Guid.Empty,
                tenantId: actor.TenantId,
                permissionType: permissionName,
                resourceType: $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName}",
                reason: authorized ? "Endpoint permission check granted." : "Endpoint permission check denied.",
                success: authorized,
                errorMessage: authorized ? null : $"Required permission '{permissionName}' was not granted.",
                ipAddress: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: context.HttpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken: context.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Request aborted - the audit entry is not required for a discarded request.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to audit permission decision for permission {Permission} on {Controller}.{Action}",
                permissionName,
                actionDescriptor.ControllerName,
                actionDescriptor.ActionName);
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
        return attr is IDacPermissionAttribute
            || attr is IResourcePermissionMarker
            || attr is IContentTypePermissionMarker
            || attr is RequireTenantPermissionAttribute
            || attr is RequiresPermissionAttribute
            || attr is RequirePermissionAttribute;
    }

    private async Task<PermissionCheckOutcome> CheckPermissionAsync(
        Attribute attr,
        HttpContext httpContext,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Handle the declarative DAC attribute contract (issues #320/#321): one generic
        // enforcement path through IDacPermissionResolver — also what custom attributes
        // derived from DacPermissionAttributeBase plug into.
        if (attr is IDacPermissionAttribute dacAttribute)
        {
            return await CheckDacAttributeAsync(dacAttribute, httpContext, actor).ConfigureAwait(false);
        }

        // Handle resource permission attributes
        if (attr is IResourcePermissionMarker resourceAttr)
        {
            return await CheckResourcePermissionAsync(resourceAttr, httpContext, actor, permissionQueryService).ConfigureAwait(false);
        }

        // Handle content-type permission attributes
        if (attr is IContentTypePermissionMarker contentTypeAttr)
        {
            return await CheckContentTypePermissionAsync(contentTypeAttr, httpContext, actor, permissionQueryService).ConfigureAwait(false);
        }

        // Handle tenant permission attributes
        if (attr is RequireTenantPermissionAttribute tenantAttr)
        {
            return await CheckTenantPermissionAsync(tenantAttr.Permission, actor, permissionQueryService).ConfigureAwait(false);
        }

        // Handle simple permission attributes
        if (attr is RequiresPermissionAttribute simpleAttr)
        {
            return await CheckTenantPermissionAsync(simpleAttr.PermissionName, actor, permissionQueryService).ConfigureAwait(false);
        }
        if (attr is RequirePermissionAttribute simpleAttr2)
        {
            return await CheckTenantPermissionAsync(simpleAttr2.PermissionName, actor, permissionQueryService).ConfigureAwait(false);
        }

        return PermissionCheckOutcome.Granted();
    }

    /// <summary>
    ///     Validates the structural declaration of a <see cref="DacPermissionAttributeBase"/>
    ///     (its own <see cref="DacPermissionAttributeBase.Validate"/> is wrapped so a
    ///     throwing validation denies instead of faulting the pipeline).
    /// </summary>
    private static bool IsValidDeclaration(DacPermissionAttributeBase attribute)
    {
        try
        {
            attribute.Validate();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Enforces one <see cref="IDacPermissionAttribute"/> through
    ///     <see cref="IDacPermissionResolver"/>. All listed permissions must be granted
    /// (AND logic). An invalid attribute declaration fails closed (denied), never skipped.
    /// </summary>
    private async Task<PermissionCheckOutcome> CheckDacAttributeAsync(
        IDacPermissionAttribute attr,
        HttpContext httpContext,
        ActorContext actor)
    {
        // Structural validation of the declaration (extensible attributes that derive
        // from DacPermissionAttributeBase get the same checks from Validate(); an
        // invalid declaration fails closed — the request is denied, never skipped).
        var declarationError = attr switch
        {
            DacPermissionAttributeBase declarative when !IsValidDeclaration(declarative) => "attribute_declaration_invalid",
            _ when attr.Layer == PermissionLayer.ContentType && string.IsNullOrWhiteSpace(attr.ContentType) => "attribute_declaration_invalid",
            _ when attr.Layer == PermissionLayer.Resource
                && (string.IsNullOrWhiteSpace(attr.ContentType) || string.IsNullOrWhiteSpace(attr.ResourceIdParameterName)) => "attribute_declaration_invalid",
            _ => null
        };
        if (declarationError is not null)
        {
            _logger.LogError(
                "Invalid DAC permission attribute declaration {Attribute} (layer {Layer}) - denying request (fail-closed).",
                attr.GetType().Name,
                attr.Layer);
            return PermissionCheckOutcome.Denied(declarationError);
        }

        var resolver = httpContext.RequestServices.GetService<IDacPermissionResolver>();
        if (resolver is null)
        {
            _logger.LogError("IDacPermissionResolver not available - denying request (fail-closed)");
            return PermissionCheckOutcome.Denied("authorization_pipeline_unavailable");
        }

        if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty || actor.TenantId is not { } tenantId)
        {
            return PermissionCheckOutcome.Denied("actor_context_unavailable");
        }

        string? resourceId = null;
        if (attr.Layer == PermissionLayer.Resource)
        {
            resourceId = ExtractResourceId(httpContext, attr.ResourceIdParameterName!, attr.ResourceIdSource);
            if (resourceId == null)
            {
                _logger.LogWarning(
                    "Resource ID parameter '{Parameter}' (source {Source}) not found in request",
                    attr.ResourceIdParameterName,
                    attr.ResourceIdSource);
                return PermissionCheckOutcome.Denied("resource_id_unavailable");
            }
        }

        var query = new DacPermissionQuery
        {
            UserId = userId,
            TenantId = tenantId,
            Layer = attr.Layer,
            ContentType = attr.ContentType,
            ResourceType = attr.Layer == PermissionLayer.Resource ? attr.ContentType : null,
            ResourceId = resourceId
        };

        foreach (var permission in attr.RequiredPermissions)
        {
            var granted = await resolver.HasPermissionAsync(query, permission, httpContext.RequestAborted).ConfigureAwait(false);
            if (!granted)
            {
                _logger.LogDebug(
                    "DAC attribute check: User {UserId}, Layer {Layer}, ContentType {ContentType}, Resource {ResourceType}/{ResourceId}, Permission {Permission} = denied",
                    userId,
                    attr.Layer,
                    attr.ContentType ?? "<none>",
                    attr.ContentType ?? "<none>",
                    resourceId ?? "<none>",
                    permission);
                return PermissionCheckOutcome.Denied("permission_denied");
            }
        }

        return PermissionCheckOutcome.Granted();
    }

    private async Task<PermissionCheckOutcome> CheckResourcePermissionAsync(
        IResourcePermissionMarker attr,
        HttpContext httpContext,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Scoped-store resolution (issues #360/#365/#321): route the resource marker
        // through IDacPermissionResolver's resource context instead of a tenant-composite
        // name, so resource grants, content-type grants of the resource's type and the
        // owner-override engine all contribute through one canonical engine pass.
        var resolver = httpContext.RequestServices.GetService<IDacPermissionResolver>();
        if (resolver is null)
        {
            _logger.LogError("IDacPermissionResolver not available - denying request (fail-closed)");
            return PermissionCheckOutcome.Denied("authorization_pipeline_unavailable");
        }

        if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty || actor.TenantId is not { } tenantId)
        {
            return PermissionCheckOutcome.Denied("actor_context_unavailable");
        }

        // Extract resource ID from route/query
        var resourceId = ExtractResourceId(httpContext, attr.ResourceIdParameterName, DacResourceIdSource.RouteOrQuery);
        if (resourceId == null)
        {
            _logger.LogWarning(
                "Resource ID parameter '{Parameter}' not found in request",
                attr.ResourceIdParameterName);
            return PermissionCheckOutcome.Denied("resource_id_unavailable");
        }

        var query = new DacPermissionQuery
        {
            UserId = userId,
            TenantId = tenantId,
            Layer = PermissionLayer.Auto,
            ResourceType = attr.ResourceType.Name,
            ResourceId = resourceId
        };

        var hasPermission = await resolver.HasPermissionAsync(query, attr.RequiredPermission.ToString()!, httpContext.RequestAborted).ConfigureAwait(false);

        _logger.LogDebug(
            "Resource permission check: User {UserId}, Resource {ResourceType}:{ResourceId}, Permission {Permission} = {Result}",
            userId,
            attr.ResourceType.Name,
            resourceId,
            attr.RequiredPermission,
            hasPermission);

        return hasPermission
            ? PermissionCheckOutcome.Granted()
            : PermissionCheckOutcome.Denied("permission_denied");
    }

    private async Task<PermissionCheckOutcome> CheckContentTypePermissionAsync(
        IContentTypePermissionMarker attr,
        HttpContext httpContext,
        ActorContext actor,
        IPermissionQueryService permissionQueryService)
    {
        // Scoped-store resolution (issues #360/#365/#321): route the content-type marker
        // through IDacPermissionResolver's content-type layer instead of a tenant-composite
        // name, so content-type grants (and tenant grants under Auto inheritance) decide.
        var resolver = httpContext.RequestServices.GetService<IDacPermissionResolver>();
        if (resolver is null)
        {
            _logger.LogError("IDacPermissionResolver not available - denying request (fail-closed)");
            return PermissionCheckOutcome.Denied("authorization_pipeline_unavailable");
        }

        if (actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty || actor.TenantId is not { } tenantId)
        {
            return PermissionCheckOutcome.Denied("actor_context_unavailable");
        }

        var query = new DacPermissionQuery
        {
            UserId = userId,
            TenantId = tenantId,
            Layer = PermissionLayer.Auto,
            ContentType = attr.ResourceType.Name
        };

        var hasPermission = await resolver.HasPermissionAsync(query, attr.Permission.ToString() ?? string.Empty, httpContext.RequestAborted).ConfigureAwait(false);

        _logger.LogDebug(
            "Content-type permission check: User {UserId}, ContentType {ContentType}, Permission {Permission} = {Result}",
            userId,
            attr.ResourceType.Name,
            attr.Permission,
            hasPermission);

        return hasPermission
            ? PermissionCheckOutcome.Granted()
            : PermissionCheckOutcome.Denied("permission_denied");
    }

    private async Task<PermissionCheckOutcome> CheckTenantPermissionAsync(
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

        return hasPermission
            ? PermissionCheckOutcome.Granted()
            : PermissionCheckOutcome.Denied("permission_denied");
    }

    /// <summary>
    ///     Extracts the resource id from the declared binding sources (issue #321: route,
    /// query, header or claims). Guids and non-empty strings are accepted (resource ids
    /// are strings in the engine); anything else fails closed (null).
    /// </summary>
    private static string? ExtractResourceId(HttpContext httpContext, string parameterName, DacResourceIdSource source)
    {
        IEnumerable<string?> ReadValues()
        {
            if (source is DacResourceIdSource.Route or DacResourceIdSource.RouteOrQuery
                && httpContext.Request.RouteValues.TryGetValue(parameterName, out var routeValue)
                && routeValue is not null)
            {
                yield return routeValue.ToString();
            }

            if (source is DacResourceIdSource.Query or DacResourceIdSource.RouteOrQuery
                && httpContext.Request.Query.TryGetValue(parameterName, out var queryValue))
            {
                yield return queryValue.FirstOrDefault();
            }

            if (source is DacResourceIdSource.Header
                && httpContext.Request.Headers.TryGetValue(parameterName, out var headerValue))
            {
                yield return headerValue.FirstOrDefault();
            }

            if (source is DacResourceIdSource.Claims)
            {
                var claim = httpContext.User.FindFirst(parameterName);
                if (claim is not null)
                {
                    yield return claim.Value;
                }
            }
        }

        foreach (var candidate in ReadValues())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var trimmed = candidate.Trim();

            // Guid-shaped values are normalized to the canonical dotted form so engine
            // resource ids match the GraphQL middleware's formatting.
            if (Guid.TryParse(trimmed, out var guid))
            {
                return guid.ToString("D");
            }

            return trimmed;
        }

        return null;
    }

    /// <summary>Outcome of one attribute's permission check (issue #321: structured errors).</summary>
    private sealed record PermissionCheckOutcome(bool Authorized, string? Reason = null)
    {
        public static PermissionCheckOutcome Granted() => new(true);

        public static PermissionCheckOutcome Denied(string reason) => new(false, reason);
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
