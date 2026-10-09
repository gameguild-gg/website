using System.Security.Claims;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Default implementation of ITenantResolver providing centralized tenant resolution logic.
///     Used by TenantMiddleware, ActorContextMiddleware, and authorization handlers.
/// </summary>
/// <remarks>
///     <para>
///         <b>Resolution Priority:</b>
///         <list type="number">
///             <item>X-Tenant-Id header (explicit tenant selection)</item>
///             <item>Host domain (domain-based multi-tenancy)</item>
///             <item>Query string parameter</item>
///             <item>Route value</item>
///             <item>JWT claims (user's default tenant)</item>
///             <item>System default tenant (fallback)</item>
///         </list>
///     </para>
/// </remarks>
public sealed class TenantResolver(
    IMediator mediator,
    ITenantDomainsRepository tenantDomainsRepository,
    ILogger<TenantResolver> logger
) : ITenantResolver
{
    /// <summary>
    ///     The header name for the tenant ID.
    /// </summary>
    public const string TenantIdHeader = "X-Tenant-Id";

    /// <summary>
    ///     The query string key for the tenant ID.
    /// </summary>
    public const string TenantIdQueryKey = "tenantId";

    /// <summary>
    ///     The claim type for tenant ID in JWT tokens.
    /// </summary>
    public const string TenantIdClaimType = "tenant_id";

    /// <summary>
    ///     The HttpContext.Items key for the resolved tenant.
    /// </summary>
    /// <remarks>
    ///     ⚠️ Prefer using <see cref="HttpContextKeys.CurrentTenant"/> directly.
    /// </remarks>
    [Obsolete("Use HttpContextKeys.CurrentTenant instead for consistency across modules.")]
    public const string TenantItemKey = HttpContextKeys.CurrentTenant;

    /// <summary>
    ///     The HttpContext.Items key for the tenant ID.
    /// </summary>
    /// <remarks>
    ///     ⚠️ Prefer using <see cref="HttpContextKeys.AuthorizationTenantId"/> directly.
    /// </remarks>
    [Obsolete("Use HttpContextKeys.AuthorizationTenantId instead for consistency across modules.")]
    public const string TenantIdItemKey = HttpContextKeys.AuthorizationTenantId;

    public async Task<TenantResolutionResult> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        // 1. Try X-Tenant-Id header (explicit tenant selection)
        var tenantIdFromHeader = TenantIdExtractor.FromHeader(context, TenantIdHeader);
        if (tenantIdFromHeader.HasValue)
        {
            var tenant = await GetTenantByIdAsync(tenantIdFromHeader.Value, cancellationToken).ConfigureAwait(false);
            if (tenant is not null)
            {
                logger.LogDebug(
                    "Tenant resolved from header: {TenantId} ({TenantName})",
                    tenant.Id,
                    tenant.Name);
                return new TenantResolutionResult(tenant, TenantResolutionSource.Header);
            }

            logger.LogWarning(
                "Tenant ID {TenantId} from header not found or inactive",
                tenantIdFromHeader);
        }

        // 2. Try host domain resolution
        var host = TenantIdExtractor.GetHost(context);
        if (!string.IsNullOrWhiteSpace(host) && !TenantIdExtractor.IsLocalhost(host))
        {
            var tenantDomain = await tenantDomainsRepository.GetByDomainAsync(host, cancellationToken).ConfigureAwait(false);
            if (tenantDomain?.Tenant is not null && tenantDomain.Tenant.IsActive)
            {
                logger.LogDebug(
                    "Tenant resolved from domain {Host}: {TenantId} ({TenantName})",
                    host,
                    tenantDomain.Tenant.Id,
                    tenantDomain.Tenant.Name);
                return new TenantResolutionResult(tenantDomain.Tenant, TenantResolutionSource.Domain);
            }
        }

        // 3. Try query string
        var tenantIdFromQuery = TenantIdExtractor.FromQuery(context, TenantIdQueryKey);
        if (tenantIdFromQuery.HasValue)
        {
            var tenant = await GetTenantByIdAsync(tenantIdFromQuery.Value, cancellationToken).ConfigureAwait(false);
            if (tenant is not null)
            {
                logger.LogDebug(
                    "Tenant resolved from query string: {TenantId} ({TenantName})",
                    tenant.Id,
                    tenant.Name);
                return new TenantResolutionResult(tenant, TenantResolutionSource.QueryString);
            }
        }

        // 4. Try route value
        var tenantIdFromRoute = TenantIdExtractor.FromRoute(context, TenantIdQueryKey);
        if (tenantIdFromRoute.HasValue)
        {
            var tenant = await GetTenantByIdAsync(tenantIdFromRoute.Value, cancellationToken).ConfigureAwait(false);
            if (tenant is not null)
            {
                logger.LogDebug(
                    "Tenant resolved from route: {TenantId} ({TenantName})",
                    tenant.Id,
                    tenant.Name);
                return new TenantResolutionResult(tenant, TenantResolutionSource.RouteValue);
            }
        }

        // 5. Try JWT claims (user's default tenant)
        var tenantIdFromClaims = GetTenantIdFromClaims(context.User);
        if (tenantIdFromClaims.HasValue)
        {
            var tenant = await GetTenantByIdAsync(tenantIdFromClaims.Value, cancellationToken).ConfigureAwait(false);
            if (tenant is not null)
            {
                logger.LogDebug(
                    "Tenant resolved from claims: {TenantId} ({TenantName})",
                    tenant.Id,
                    tenant.Name);
                return new TenantResolutionResult(tenant, TenantResolutionSource.Claims);
            }
        }

        // 6. Fall back to default tenant
        var defaultTenant = await mediator.Send(new GetDefaultTenantQuery(), cancellationToken).ConfigureAwait(false);
        if (defaultTenant is not null && defaultTenant.IsActive)
        {
            logger.LogDebug(
                "Tenant resolved as default: {TenantId} ({TenantName})",
                defaultTenant.Id,
                defaultTenant.Name);
            return new TenantResolutionResult(defaultTenant, TenantResolutionSource.Default);
        }

        logger.LogDebug("No tenant could be resolved");
        return TenantResolutionResult.None;
    }

    public async Task<Tenant?> ResolveByIdentifierAsync(
        string tenantIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            return null;
        }

        // Try as GUID first
        // SECURITY (Attack 5): Reject Guid.Empty to prevent type confusion
        if (Guid.TryParse(tenantIdentifier, out var tenantId) && tenantId != Guid.Empty)
        {
            return await GetTenantByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        // Try as slug
        var tenant = await mediator.Send(new GetTenantBySlugQuery(tenantIdentifier), cancellationToken).ConfigureAwait(false);
        return tenant?.IsActive == true ? tenant : null;
    }

    public Guid? GetResolvedTenantId(HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextKeys.AuthorizationTenantId, out var tenantIdObj) && tenantIdObj is Guid tenantId)
        {
            return tenantId;
        }

        return null;
    }

    private async Task<Tenant?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await mediator.Send(new GetTenantByIdQuery(tenantId), cancellationToken).ConfigureAwait(false);
        return tenant?.IsActive == true ? tenant : null;
    }

    private static Guid? GetTenantIdFromClaims(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var tenantClaim = user.FindFirst(TenantIdClaimType);
        // SECURITY (Attack 5): Reject Guid.Empty to prevent type confusion
        if (tenantClaim is not null && Guid.TryParse(tenantClaim.Value, out var tenantId) && tenantId != Guid.Empty)
        {
            return tenantId;
        }

        return null;
    }
}
