using System.Security.Claims;
using System.Net;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Default implementation of authorization tenant resolver.
/// </summary>
public sealed class AuthorizationTenantResolver : IAuthorizationTenantResolver
{
    private readonly TenancyOptions _options;
    private readonly AuthorizationTokenOptions _tokenOptions;

    /// <summary>
    ///     Initializes a new instance of <see cref="AuthorizationTenantResolver"/>.
    /// </summary>
    public AuthorizationTenantResolver(
        IOptions<TenancyOptions> options,
        IOptions<AuthorizationTokenOptions> tokenOptions)
    {
        _options = options.Value;
        _tokenOptions = tokenOptions.Value;
    }

    /// <inheritdoc />
    public string? ResolveFromRequest(HttpContext context)
    {
        // Try header first
        if (_options.Resolution.EnableHeader)
        {
            var headerValue = context.Request.Headers[_options.Resolution.HeaderName].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerValue))
            {
                return headerValue;
            }
        }

        // Try subdomain
        if (_options.Resolution.EnableSubdomain)
        {
            var host = context.Request.Host.Host;
            var subdomain = ExtractSubdomain(host);
            if (!string.IsNullOrEmpty(subdomain) &&
                !_options.Resolution.SubdomainIgnoreList.Contains(subdomain, StringComparer.OrdinalIgnoreCase))
            {
                return subdomain;
            }
        }

        // Try query string
        if (_options.Resolution.EnableQueryString)
        {
            var queryValue = context.Request.Query[_options.Resolution.QueryStringKey].FirstOrDefault();
            if (!string.IsNullOrEmpty(queryValue))
            {
                return queryValue;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public string? ResolveFromClaims(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(_tokenOptions.TenantClaimType);
    }

    /// <inheritdoc />
    public Task<string?> ResolveTenantIdAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            ResolveFromMiddlewareContext(context)
            ?? ResolveFromRequest(context)
            ?? ResolveFromClaims(context.User)
            ?? GetUserDefaultTenant(context.User));
    }

    private static string? ResolveFromMiddlewareContext(HttpContext context)
    {
        return context.Items.TryGetValue(HttpContextKeys.AuthorizationTenantId, out var value)
               && value is Guid tenantId
               && tenantId != Guid.Empty
            ? tenantId.ToString()
            : null;
    }

    /// <inheritdoc />
    public string? GetUserDefaultTenant(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(_tokenOptions.UserDefaultTenantClaimType);
    }

    private static string? ExtractSubdomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out _))
        {
            return null;
        }

        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            // Return first part as subdomain (e.g., "tenant" from "tenant.example.com")
            return parts[0];
        }

        return null;
    }
}
