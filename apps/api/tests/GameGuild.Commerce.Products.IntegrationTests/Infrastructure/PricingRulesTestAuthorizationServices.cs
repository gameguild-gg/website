using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GameGuild.Commerce.Products.IntegrationTests.Infrastructure;

/// <summary>
/// Header-driven permission service for pricing-engine endpoint tests: the caller declares
/// granted permissions via the <c>X-Test-Permissions</c> header. Implements both the CQRS
/// pipeline service and the endpoint permission-query service (Orders integration-test
/// precedent) so controller-level <c>[RequirePermission]</c> gates resolve deterministically.
/// </summary>
internal sealed class PricingRulesTestAuthorizationPermissionService(IHttpContextAccessor httpContextAccessor)
    : IAuthorizationPermissionService, IPermissionQueryService
{
    public Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Has(permission));
    }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(GetPermissions());
    }

    public Task<PermissionCheckResult> HasAllPermissionsAsync(
        Guid userId,
        Guid tenantId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var requested = permissions.ToList();
        var granted = GetPermissions();
        var present = requested.Where(Has).ToList();
        var missing = requested.Except(present, StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult(missing.Count == 0
            ? PermissionCheckResult.AllPresent(present)
            : PermissionCheckResult.Partial(present, missing));
    }

    public Task<PermissionCheckResult> HasAnyPermissionAsync(
        Guid userId,
        Guid tenantId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var requested = permissions.ToList();
        var present = requested.Where(Has).ToList();
        return Task.FromResult(present.Count > 0
            ? PermissionCheckResult.Partial(present, requested.Except(present, StringComparer.OrdinalIgnoreCase))
            : PermissionCheckResult.NonePresent(requested));
    }

    public Task<bool> HasTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Has(permission));
    }

    public Task<List<string>> GetTenantPermissionsAsync(
        Guid? userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetPermissions().ToList());
    }

    public Task<List<string>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetPermissions().ToList());
    }

    public Task<List<string>> GetGlobalDefaultPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<List<string>> GetTenantDefaultPermissionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<bool> IsUserInTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    private bool Has(string permission) => GetPermissions().Contains(permission, StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<string> GetPermissions()
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null || !headers.TryGetValue("X-Test-Permissions", out var values)) return [];

        return values
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// Tenant resolver for pricing-engine endpoint tests: resolves the default test tenant unless
/// the request opts out via <c>X-Test-No-Tenant</c>.
/// </summary>
internal sealed class PricingRulesTestAuthorizationTenantResolver : IAuthorizationTenantResolver
{
    public string? ResolveFromRequest(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Test-No-Tenant")) return null;
        return context.Request.Headers.TryGetValue("X-Test-Tenant", out var values)
            ? values.ToString()
            : PricingRulesTestAuthHandler.DefaultTenantId.ToString();
    }

    public string? ResolveFromClaims(ClaimsPrincipal principal) =>
        principal.FindFirstValue("tenant_id");

    public string? GetUserDefaultTenant(ClaimsPrincipal principal) =>
        principal.FindFirstValue("tenant_id");

    public Task<string?> ResolveTenantIdAsync(
        HttpContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ResolveFromRequest(context));
}
