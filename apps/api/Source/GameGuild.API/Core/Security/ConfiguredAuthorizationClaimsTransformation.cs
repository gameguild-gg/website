using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Core.Security;

/// <summary>
///     Applies explicitly configured, allowlisted claim value mappings to authenticated identities.
/// </summary>
public sealed class ConfiguredAuthorizationClaimsTransformation(
    IOptionsMonitor<AuthorizationOptions> options) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var transformations = options.CurrentValue.ClaimTransformations;
        if (transformations.Count == 0)
        {
            return Task.FromResult(principal);
        }

        foreach (var identity in principal.Identities.Where(identity => identity.IsAuthenticated))
        {
            foreach (var transformation in transformations)
            {
                foreach (var sourceClaim in identity.FindAll(transformation.SourceClaimType).ToArray())
                {
                    if (!transformation.ValueMappings.TryGetValue(sourceClaim.Value, out var targetValue) ||
                        identity.HasClaim(transformation.TargetClaimType, targetValue))
                    {
                        continue;
                    }

                    identity.AddClaim(new Claim(
                        transformation.TargetClaimType,
                        targetValue,
                        sourceClaim.ValueType,
                        sourceClaim.Issuer,
                        sourceClaim.OriginalIssuer));
                }
            }
        }

        return Task.FromResult(principal);
    }
}
