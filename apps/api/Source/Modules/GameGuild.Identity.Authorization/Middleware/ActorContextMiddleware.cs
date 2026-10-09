using System.Security.Claims;
using GameGuild.Identity.Authorization.Utilities;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Middleware that builds and populates the <see cref="ActorContext"/> from the current HTTP request.
/// </summary>
/// <remarks>
///     <para>
///         This middleware should run early in the pipeline, after authentication but before
///         authorization decisions are made. It extracts claims, resolves tenant context,
///         and builds an immutable ActorContext for the request.
///     </para>
///     <para>
///         For tenant-scoped permissions, this middleware can optionally fetch effective
///         permissions from the database (via IAuthorizationPermissionService).
///     </para>
///     <para>
///         SECURITY: This middleware implements fail-closed error handling. If permission fetching
///         fails, the ActorContext is set to Anonymous and a 500 error is returned to prevent
///         potential privilege escalation from stale token permissions.
///     </para>
/// </remarks>
public sealed class ActorContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ActorContextMiddleware> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ActorContextMiddleware"/> class.
    /// </summary>
    public ActorContextMiddleware(RequestDelegate next, ILogger<ActorContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    ///     Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        IActorContextAccessor actorContextAccessor,
        IAuthorizationTenantResolver tenantResolver,
        IClaimsPrincipalAccessor claimsPrincipalAccessor,
        IAuthorizationPermissionService? permissionService = null)
    {
        try
        {
            var actorContext = await BuildActorContextAsync(
                context, 
                claimsPrincipalAccessor,
                tenantResolver, 
                permissionService,
                context.RequestAborted).ConfigureAwait(false);

            actorContextAccessor.SetActorContext(actorContext);

            await _next(context).ConfigureAwait(false);
        }
        catch (PermissionFetchException ex)
        {
            // SECURITY: Fail-closed on permission fetch errors
            // Set context to Anonymous to deny all permissions
            actorContextAccessor.SetActorContext(ActorContext.Anonymous);
            
            _logger.LogError(ex,
                "SECURITY: Permission fetch failed for user {SubjectId} in tenant {TenantId}. " +
                "Request denied with fail-closed policy. RequestId: {RequestId}, Path: {Path}",
                LogRedaction.RedactId(ex.SubjectId, "uid"),
                LogRedaction.RedactId(ex.TenantId, "tid"),
                context.TraceIdentifier,
                LogRedaction.Sanitize(context.Request.Path.Value));

            // Return 500 to indicate server error (don't leak security details to client)
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "Internal Server Error",
                status = 500,
                detail = "An error occurred while processing the security context. Please try again.",
                traceId = context.TraceIdentifier
            }, cancellationToken: context.RequestAborted);
        }
        finally
        {
            // Clear context to prevent leaking to pooled connections
            actorContextAccessor.ClearActorContext();
        }
    }

    private static async Task<ActorContext> BuildActorContextAsync(
        HttpContext httpContext,
        IClaimsPrincipalAccessor claimsPrincipalAccessor,
        IAuthorizationTenantResolver tenantResolver,
        IAuthorizationPermissionService? permissionService,
        CancellationToken cancellationToken)
    {
        var user = claimsPrincipalAccessor.ClaimsPrincipal;
        var isAuthenticated = user != null && ClaimsExtractor.IsAuthenticated(user);

        if (!isAuthenticated || user is null)
        {
            return ActorContext.Anonymous;
        }

        // At this point, user is guaranteed to be non-null
        // Determine actor kind from claims
        var actorKind = DetermineActorKind(user);

        // Extract subject ID
        var subjectId = ClaimsExtractor.GetUserId(user);

        // Resolve tenant ID
        var tenantIdStr = await tenantResolver.ResolveTenantIdAsync(httpContext, cancellationToken).ConfigureAwait(false);
        Guid? tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : null;

        // Token roles describe platform access. Tenant middleware contributes the
        // current membership role only after validating it against persistence.
        var roles = ClaimsExtractor.GetRoles(user);
        if (httpContext.Items.TryGetValue(HttpContextKeys.AuthorizationTenantRole, out var membershipRoleValue) &&
            membershipRoleValue is string membershipRole &&
            !string.IsNullOrWhiteSpace(membershipRole))
        {
            roles.Add(membershipRole);
        }

        // Extract permissions - either from claims or fetch from database
        var permissions = await ExtractOrFetchPermissionsAsync(
            user, 
            subjectId, 
            tenantId, 
            permissionService, 
            cancellationToken).ConfigureAwait(false);

        // Extract attributes from claims
        var attributes = ExtractAttributes(user, tenantId);

        // Determine auth scheme (use user.Identity from the abstraction, not httpContext.User)
        var authScheme = user.Identity?.AuthenticationType;

        var builder = ActorContextBuilder.Create()
            .WithActorKind(actorKind)
            .WithSubjectId(subjectId)
            .WithTenantId(tenantId)
            .WithRoles(roles)
            .WithPermissions(permissions)
            .WithAttributes(attributes)
            .WithAuthScheme(authScheme)
            .AsAuthenticated();

        return builder.Build();
    }

    private static ActorKind DetermineActorKind(ClaimsPrincipal user)
    {
        // OCP-compliant: Uses ActorKindResolver which reads from attributes
        // Adding new ActorKind values with ActorKindIdentifierAttribute works automatically
        var grantType = ClaimsExtractor.GetGrantType(user);
        var actorTypeClaim = ClaimsExtractor.GetActorType(user);
        var subjectId = ClaimsExtractor.GetUserId(user);

        return ActorKindResolver.Resolve(grantType, actorTypeClaim, subjectId);
    }

    private static async Task<HashSet<string>> ExtractOrFetchPermissionsAsync(
        ClaimsPrincipal user,
        string? subjectId,
        Guid? tenantId,
        IAuthorizationPermissionService? permissionService,
        CancellationToken cancellationToken)
    {
        // Extract permissions from claims (if present in token)
        var permissions = ClaimsExtractor.GetPermissions(user);

        // If we have a permission service and a valid subject/tenant, fetch from database
        // This ensures we always have the latest permissions even if not in token
        if (permissionService != null && 
            !string.IsNullOrEmpty(subjectId) && 
            Guid.TryParse(subjectId, out var userId) &&
            tenantId.HasValue)
        {
            try
            {
                var dbPermissions = await permissionService.GetPermissionsAsync(
                    userId, 
                    tenantId.Value, 
                    cancellationToken).ConfigureAwait(false);

                // SECURITY: Replace claim permissions with database permissions
                // Database is the source of truth for current permissions
                permissions.Clear();
                foreach (var perm in dbPermissions)
                {
                    permissions.Add(perm);
                }
            }
            catch (Exception ex)
            {
                // SECURITY: Fail-closed on permission fetch errors
                // Throw custom exception to trigger middleware error handling
                throw new PermissionFetchException(
                    $"Failed to fetch permissions for user {userId} in tenant {tenantId}",
                    userId,
                    tenantId.Value,
                    ex);
            }
        }

        return permissions;
    }

    private static Dictionary<string, string> ExtractAttributes(ClaimsPrincipal user, Guid? tenantId)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Standard claims to extract
        var claimMappings = new[]
        {
            (ClaimTypes.Email, "email"),
            ("email", "email"),
            (ClaimTypes.Name, "name"),
            ("name", "name"),
            (ClaimTypes.GivenName, "given_name"),
            ("given_name", "given_name"),
            (ClaimTypes.Surname, "family_name"),
            ("family_name", "family_name"),
            ("preferred_username", "preferred_username"),
            ("picture", "picture"),
            ("email_verified", "email_verified"),
            ("mfa_verified", "mfa_verified"),
            ("mfa_method", "mfa_method"),
            (ClaimNames.Amr, "amr"),
            ("ip_address", "ip_address"),
            ("user_agent", "user_agent"),
            ("device_fingerprint", "device_fingerprint"),
            ("trusted_device", "trusted_device"),
            ("session_id", "session_id"),
            ("jti", "jti"),
            ("auth_time", "auth_time"),
            ("exp", "exp"),
            ("tenant_name", "tenant_name"),
            ("tenant_active", "tenant_active"),
            ("tenant_role", "tenant_role"),
            ("tenant_joined_at", "tenant_joined_at"),
            ("tenant_membership_status", "tenant_membership_status"),
            ("subscription_plan", "subscription_plan"),
            ("department", "department"),
            ("job_title", "job_title"),
            ("manager_id", "manager_id"),
            ("org_unit", "org_unit"),
            ("employee_id", "employee_id"),
            ("cost_center", "cost_center"),
            ("idp", "idp"),
            ("external_sub", "external_sub"),
            ("locale", "locale"),
            ("zoneinfo", "zoneinfo")
        };

        foreach (var (claimType, attrKey) in claimMappings)
        {
            var claimValue = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(claimValue) && !attributes.ContainsKey(attrKey))
            {
                attributes[attrKey] = claimValue;
            }
        }

        // Extract tenant settings
        foreach (var claim in user.Claims)
        {
            if (claim.Type.StartsWith("tenant_setting:", StringComparison.Ordinal))
            {
                attributes[claim.Type] = claim.Value;
            }
        }

        // Add tenant ID as attribute for reference
        if (tenantId.HasValue)
        {
            attributes["tenant_id"] = tenantId.Value.ToString();
        }

        return attributes;
    }
}
