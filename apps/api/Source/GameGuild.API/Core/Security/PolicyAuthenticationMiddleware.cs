using System.Security.Claims;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Core.Security;

/// <summary>
/// Selects only statically declared authentication schemes before revocation, tenant
/// membership and actor capture. Tenant-dependent authorization remains later in the pipeline.
/// </summary>
public sealed class PolicyAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<AuthorizationOptions> options,
        IPolicyEvaluator evaluator, IAuthenticationSchemeProvider schemes)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null)
        {
            var policy = await AuthorizationPolicy.CombineAsync(
                new StaticSchemePolicyProvider(options.Value),
                endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? [],
                endpoint?.Metadata.GetOrderedMetadata<AuthorizationPolicy>() ?? []).ConfigureAwait(false);
            if (policy is { AuthenticationSchemes.Count: > 0 })
            {
                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    if (await schemes.GetSchemeAsync(scheme).ConfigureAwait(false) is null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }
                }

                // Authentication only: requirements are evaluated by UseAuthorization after
                // the actual tenant membership and immutable actor have been established.
                await evaluator.AuthenticateAsync(policy, context).ConfigureAwait(false);
                if (context.User.Identities.Count(identity => identity.IsAuthenticated) > 1)
                {
                    // Do not merge one credential's owner with another credential's scopes.
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
        }

        context.Features.Set(new AuthorizationPrincipalSnapshot(context.User));
        await next(context).ConfigureAwait(false);
    }

    private sealed class StaticSchemePolicyProvider(AuthorizationOptions options) : IAuthorizationPolicyProvider
    {
        private static readonly AuthorizationPolicy Deferred =
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
            Task.FromResult<AuthorizationPolicy?>(
                Policies.IsValid(policyName) || ApiKeyScopePolicies.TryGetScope(policyName, out _)
                    ? Deferred
                    : options.GetPolicy(policyName) ?? Deferred);

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(options.DefaultPolicy);
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult(options.FallbackPolicy);
    }
}

/// <summary>Immutable values used to reject later policy authentication with a different identity.</summary>
internal sealed class AuthorizationPrincipalSnapshot(ClaimsPrincipal principal)
{
    private readonly IdentitySnapshot[] _identities = Capture(principal);

    public bool Matches(ClaimsPrincipal current)
    {
        var identities = Capture(current);
        return _identities.Length == identities.Length && _identities.Zip(identities).All(pair =>
            pair.First.AuthenticationType == pair.Second.AuthenticationType &&
            pair.First.NameClaimType == pair.Second.NameClaimType &&
            pair.First.RoleClaimType == pair.Second.RoleClaimType &&
            pair.First.Claims.SequenceEqual(pair.Second.Claims));
    }

    private static IdentitySnapshot[] Capture(ClaimsPrincipal user) => user.Identities.Select(identity =>
        new IdentitySnapshot(identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType,
            identity.Claims.Select(claim => new ClaimSnapshot(claim.Type, claim.Value, claim.ValueType,
                claim.Issuer, claim.OriginalIssuer)).ToArray())).ToArray();

    private sealed record IdentitySnapshot(string? AuthenticationType, string NameClaimType,
        string RoleClaimType, ClaimSnapshot[] Claims);
    private sealed record ClaimSnapshot(string Type, string Value, string ValueType, string Issuer, string OriginalIssuer);
}

/// <summary>Prevents tenant-dependent policies from silently replacing the established actor.</summary>
public sealed class ActorBoundPolicyEvaluator(IAuthorizationService authorization) : PolicyEvaluator(authorization)
{
    public override Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy,
        AuthenticateResult authenticationResult, HttpContext context, object? resource)
    {
        var snapshot = context.Features.Get<AuthorizationPrincipalSnapshot>();
        if (snapshot is not null && !snapshot.Matches(context.User))
        {
            return Task.FromResult(authenticationResult.Succeeded
                ? PolicyAuthorizationResult.Forbid()
                : PolicyAuthorizationResult.Challenge());
        }
        return base.AuthorizeAsync(policy, authenticationResult, context, resource);
    }
}
