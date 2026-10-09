using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Enforces <see cref="ApiKeyScopeRequirement"/> for requests authenticated with an API key.
///     <para>
///         Requests that were not authenticated with an API key (no <c>api_key_id</c> claim) are
///         intentionally passed through — this requirement only constrains API-key traffic, so
///         normal user authentication paths keep working unchanged.
///     </para>
///     <para>
///         API-key requests fail closed: a missing or empty <c>scope</c> claim set denies the
///         operation, and only an exact (case-insensitive) scope match or the <c>*</c> wildcard
///         (same semantics as <see cref="ApiKey.HasScope"/>) grants it.
///     </para>
/// </summary>
public sealed class ApiKeyScopeHandler(ILogger<ApiKeyScopeHandler>? logger = null) : AuthorizationHandler<ApiKeyScopeRequirement>
{
    public const string ApiKeyIdClaimType = "api_key_id";
    public const string ScopeClaimType = "scope";

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyScopeRequirement requirement)
    {
        var apiKeyId = context.User.FindFirst(ApiKeyIdClaimType)?.Value;
        if (string.IsNullOrEmpty(apiKeyId))
        {
            // Not an API-key-authenticated request: nothing to constrain.
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var scopes = context.User.FindAll(ScopeClaimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (scopes.Length == 0)
        {
            logger?.LogWarning(
                "API key {ApiKeyId} presented no scope claims - failing closed for scope '{Scope}'",
                apiKeyId,
                requirement.Scope);
            context.Fail(new AuthorizationFailureReason(
                this,
                $"API key '{apiKeyId}' declared no scopes; operation requiring '{requirement.Scope}' is denied"));
            return Task.CompletedTask;
        }

        var granted = scopes.Contains(requirement.Scope, StringComparer.OrdinalIgnoreCase) ||
                      scopes.Contains(ApiKeyScopes.Wildcard, StringComparer.Ordinal);
        if (granted)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        logger?.LogInformation(
            "API key {ApiKeyId} with scopes [{Scopes}] denied operation requiring scope '{Scope}'",
            apiKeyId,
            string.Join(",", scopes),
            requirement.Scope);
        context.Fail(new AuthorizationFailureReason(
            this,
            $"API key '{apiKeyId}' is not authorized for scope '{requirement.Scope}'"));
        return Task.CompletedTask;
    }
}
