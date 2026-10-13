using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Resolves the provisioning actor (tenant + token) for a SCIM request. The tenant
///     is taken exclusively from the authenticated provisioning-token claims; route or
///     header tenants are never consulted, so a token can never be aimed at another
///     tenant. Fails closed when the claims are absent or malformed.
/// </summary>
public interface IScimProvisioningContext
{
    ScimProvisioningActor RequireActor();
}

/// <summary>The provisioning actor derived from the token claims.</summary>
public sealed record ScimProvisioningActor(Guid TenantId, Guid TokenId, IReadOnlyList<string> Scopes)
{
    public string Subject => $"scim_token:{TokenId:N}";

    public bool HasScope(string scope)
        => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase) || Scopes.Contains("*");
}

public sealed class HttpScimProvisioningContext(IHttpContextAccessor httpContextAccessor) : IScimProvisioningContext
{
    public ScimProvisioningActor RequireActor()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal is null || principal.Identity is not { IsAuthenticated: true })
        {
            throw new ScimException(401, null, "A valid SCIM provisioning token is required.");
        }

        var tenantClaim = principal.FindFirst("tenant_id")?.Value
                          ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tokenClaim = principal.FindFirst("scim_token_id")?.Value;

        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty ||
            !Guid.TryParse(tokenClaim, out var tokenId) || tokenId == Guid.Empty)
        {
            throw new ScimException(401, null, "The provisioning token claims are invalid.");
        }

        var scopes = principal.FindAll("scope").Select(claim => claim.Value).ToArray();
        return new ScimProvisioningActor(tenantId, tokenId, scopes);
    }
}
