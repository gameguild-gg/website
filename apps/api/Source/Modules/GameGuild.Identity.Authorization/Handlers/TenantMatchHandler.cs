using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Handles tenant match requirements by validating the token's tenant claim
///     matches the resolved tenant context.
/// </summary>
public sealed class TenantMatchHandler : AuthorizationHandler<TenantMatchRequirement>
{
    private readonly IAuthorizationTenantContext _tenantContext;
    private readonly IAuthorizationTenantResolver _tenantResolver;
    private readonly TenancyOptions _options;
    private readonly AuthorizationTokenOptions _tokenOptions;
    private readonly ILogger<TenantMatchHandler> _logger;

    /// <summary>
    ///     Initializes a new instance of <see cref="TenantMatchHandler"/>.
    /// </summary>
    public TenantMatchHandler(
        IAuthorizationTenantContext tenantContext,
        IAuthorizationTenantResolver tenantResolver,
        IOptions<TenancyOptions> options,
        IOptions<AuthorizationTokenOptions> tokenOptions,
        ILogger<TenantMatchHandler> logger)
    {
        _tenantContext = tenantContext;
        _tenantResolver = tenantResolver;
        _options = options.Value;
        _tokenOptions = tokenOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantMatchRequirement requirement)
    {
        var resolvedTenantId = GetResolvedTenantId(context.User);

        if (string.IsNullOrEmpty(resolvedTenantId))
        {
            _logger.LogWarning("No tenant resolved for authorization check");
            context.Fail(new AuthorizationFailureReason(this, "No tenant context available"));
            return Task.CompletedTask;
        }

        var tokenTenantId = context.User.FindFirstValue(_tokenOptions.TenantClaimType);

        // If no tenant claim in token, check if we can use user's default tenant
        if (string.IsNullOrEmpty(tokenTenantId))
        {
            if (requirement.StrictMatch)
            {
                _logger.LogWarning("Strict tenant match required but no tenant claim in token");
                context.Fail(new AuthorizationFailureReason(this, "Token does not contain tenant claim"));
                return Task.CompletedTask;
            }

            var userDefaultTenant = _tenantResolver.GetUserDefaultTenant(context.User);
            if (userDefaultTenant == resolvedTenantId)
            {
                _logger.LogDebug("Tenant match succeeded via user's default tenant");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        // Check if token's tenant matches resolved tenant
        if (tokenTenantId == resolvedTenantId)
        {
            _logger.LogDebug("Tenant match succeeded: {TenantId}", resolvedTenantId);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if we can fall back to base tenant
        if (!requirement.StrictMatch && resolvedTenantId == _options.DefaultTenantId)
        {
            _logger.LogDebug("Tenant match succeeded via base tenant fallback");
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Tenant mismatch: token={TokenTenant}, resolved={ResolvedTenant}",
            tokenTenantId,
            resolvedTenantId);

        context.Fail(new AuthorizationFailureReason(this, "Tenant mismatch"));
        return Task.CompletedTask;
    }

    private string? GetResolvedTenantId(ClaimsPrincipal user)
    {
        // First check context - convert Guid to string for comparison
        if (_tenantContext.HasTenant && _tenantContext.TenantId.HasValue)
        {
            return _tenantContext.TenantId.Value.ToString();
        }

        // Fall back to claims
        return _tenantResolver.ResolveFromClaims(user);
    }
}
