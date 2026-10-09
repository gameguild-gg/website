using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Authorization policy provider that resolves dynamic <c>apikey-scope:{scope}</c> policies
///     (see <see cref="ApiKeyScopePolicies"/>) and delegates every other name to the database-backed
///     provider, preserving existing policy resolution unchanged.
/// </summary>
public sealed class ApiKeyScopePolicyProvider(
    DbAuthorizationPolicyProvider fallback,
    ILogger<ApiKeyScopePolicyProvider>? logger = null) : IAuthorizationPolicyProvider
{
    private readonly DbAuthorizationPolicyProvider _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (ApiKeyScopePolicies.TryGetScope(policyName, out var scope))
        {
            logger?.LogDebug("Resolving API-key scope policy '{PolicyName}'", policyName);
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ApiKeyScopeRequirement(scope))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
