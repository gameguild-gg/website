using System.Security.Claims;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.Security;

/// <summary>
/// Persists the result of endpoint authorization through the shared audit service.
/// </summary>
internal sealed class AuditingAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditingAuthorizationMiddlewareResultHandler> _logger;
    private readonly IAuthorizationMiddlewareResultHandler _inner;

    public AuditingAuthorizationMiddlewareResultHandler(
        IAuditService auditService,
        ILogger<AuditingAuthorizationMiddlewareResultHandler> logger)
        : this(auditService, logger, new AuthorizationMiddlewareResultHandler())
    {
    }

    internal AuditingAuthorizationMiddlewareResultHandler(
        IAuditService auditService,
        ILogger<AuditingAuthorizationMiddlewareResultHandler> logger,
        IAuthorizationMiddlewareResultHandler inner)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        try
        {
            await _auditService.LogAsync(CreateAuditRequest(context, policy, authorizeResult)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit transport failures must not change the authorization result.
            _logger.LogError(exception, "Could not record an authorization decision audit event");
        }

        await _inner.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
    }

    private static CreateAuditLogRequest CreateAuditRequest(
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var succeeded = authorizeResult.Succeeded;
        var endpoint = context.GetEndpoint();
        var routeTemplate = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
        var authorizeMetadata = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? [];
        var failure = authorizeResult.AuthorizationFailure;
        var requiredRequirements = policy.Requirements
            .Select(requirement => requirement.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var failedRequirements = failure?.FailedRequirements
            .Select(requirement => requirement.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
        var failureHandlerTypes = failure?.FailureReasons
            .Select(reason => reason.Handler.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];

        return new CreateAuditLogRequest
        {
            ActionType = succeeded ? AuditActionTypes.PermissionGranted : AuditActionTypes.PermissionDenied,
            ResourceType = routeTemplate ?? endpoint?.DisplayName ?? "HTTP endpoint",
            ResourceId = GetResourceId(context.Request),
            UserId = GetGuidClaim(context.User, ClaimTypes.NameIdentifier, "sub"),
            TenantId = GetGuidClaim(context.User, JwtClaimTypes.TenantId),
            SessionId = GetGuidClaim(context.User, JwtClaimTypes.SessionId, "session_id"),
            // Use the effective remote peer after trusted-proxy middleware, never raw forwarding headers.
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Description = succeeded
                ? "Authorization middleware granted access to an endpoint."
                : "Authorization middleware denied or challenged access to an endpoint.",
            Metadata = new
            {
                HttpMethod = context.Request.Method,
                RouteTemplate = routeTemplate,
                EndpointName = endpoint?.DisplayName,
                Policies = authorizeMetadata.Select(item => item.Policy).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                Roles = authorizeMetadata.Select(item => item.Roles).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                AuthenticationSchemes = authorizeMetadata.Select(item => item.AuthenticationSchemes).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                RequiredRequirements = requiredRequirements,
                FailedRequirements = failedRequirements,
                FailureHandlerTypes = failureHandlerTypes,
                Result = succeeded ? "Granted" : authorizeResult.Forbidden ? "Forbidden" : "Challenged"
            },
            Success = succeeded,
            ErrorMessage = succeeded ? null : "One or more authorization requirements were not satisfied.",
            RiskLevel = succeeded ? AuditRiskLevel.Medium : AuditRiskLevel.High,
            Category = AuditCategory.Permission,
            CorrelationId = context.TraceIdentifier
        };
    }

    private static Guid? GetGuidClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static string? GetResourceId(HttpRequest request)
    {
        var routeId = request.RouteValues
            .Where(routeValue => string.Equals(routeValue.Key, "id", StringComparison.OrdinalIgnoreCase) ||
                                 routeValue.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            .OrderBy(routeValue => string.Equals(routeValue.Key, "id", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(routeValue => routeValue.Key, StringComparer.OrdinalIgnoreCase)
            .Select(routeValue => routeValue.Value?.ToString())
            .FirstOrDefault(value => Guid.TryParse(value, out _));

        return Guid.TryParse(routeId, out var id) ? id.ToString("D") : null;
    }
}
