using Microsoft.AspNetCore.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Well-known scope values understood by the API-key authorization surface.
/// </summary>
public static class ApiKeyScopes
{
    /// <summary>
    ///     Wildcard scope granted to unrestricted keys; satisfies any scope requirement.
    /// </summary>
    public const string Wildcard = "*";

    /// <summary>
    ///     Scope required to manage (create/rotate/revoke) API keys when authenticating with an API key.
    /// </summary>
    public const string ManageApiKeys = "api_keys:manage";
}

/// <summary>
///     Names and helpers for the dynamic API-key scope policies
///     (<c>apikey-scope:{scope}</c>), resolved by <see cref="ApiKeyScopePolicyProvider"/>.
/// </summary>
public static class ApiKeyScopePolicies
{
    /// <summary>
    ///     Prefix that marks a policy name as an API-key scope policy.
    /// </summary>
    public const string Prefix = "apikey-scope:";

    /// <summary>
    ///     Builds the policy name that constrains API-key-authenticated callers to <paramref name="scope"/>.
    /// </summary>
    public static string For(string scope) => Prefix + scope;

    /// <summary>
    ///     Whether <paramref name="policyName"/> is an API-key scope policy name,
    ///     and with which required scope.
    /// </summary>
    public static bool TryGetScope(string policyName, out string scope)
    {
        scope = string.Empty;
        if (string.IsNullOrWhiteSpace(policyName) || !policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        scope = policyName[Prefix.Length..];
        return scope.Length > 0;
    }
}

/// <summary>
///     Authorization requirement that constrains API-key-authenticated requests to the
///     scopes declared by their key. Requests authenticated through any other scheme
///     (interactive users, service accounts) are not affected by this requirement.
/// </summary>
public sealed class ApiKeyScopeRequirement(string scope) : IAuthorizationRequirement
{
    /// <summary>
    ///     Gets the scope the API key must declare (case-insensitive; <c>*</c> satisfies any scope).
    /// </summary>
    public string Scope { get; } = !string.IsNullOrWhiteSpace(scope)
        ? scope
        : throw new ArgumentException("Scope must not be empty.", nameof(scope));
}
