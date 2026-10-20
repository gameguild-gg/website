using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Guest session API (issue #213) — anonymous guest access with minimal permissions and
/// an upgrade path. The issuance endpoint is anonymous (rate limited per IP, audited); the
/// introspection endpoint is served on the dedicated guest scheme under the fail-closed
/// <see cref="AuthorizationPolicies.GuestAccess"/> policy. Every other endpoint of the
/// platform keeps its normal <c>[Authorize]</c> semantics and rejects guest credentials
/// outright (the guest audience is invalid on the standard bearer scheme).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/guest")]
public sealed class GuestSessionController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Issue a short-lived anonymous guest session. No credentials are presented or
    /// returned beyond the guest token itself; there is no refresh token by design.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.GuestIssuance)]
    [HttpPost("v{version:apiVersion}/auth/guest-session")]
    [EndpointSummary("Issue an anonymous guest session")]
    [EndpointDescription("Issues a short-lived guest session (guest audience bearer token, no refresh credential) holding only the enumerated public guest permissions. Rate limited per client address and audited through the security-event pipeline.")]
    [ProducesResponseType<GuestSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Issue([FromBody] GuestSessionIssueRequest? body, CancellationToken ct)
    {
        try
        {
            var response = await sender.Send(
                new IssueGuestSessionCommand { DeviceFingerprint = body?.DeviceFingerprint },
                ct).ConfigureAwait(false);
            return Ok(response);
        }
        catch (GuestAccessDisabledException)
        {
            // Fail closed: disabled guest access is a policy decision, not an error state.
            return Forbid();
        }
    }

    /// <summary>
    ///     Introspect the caller's current guest session: guest id, role and the enumerated
    /// permissions it holds. Served only on the guest scheme — a user access token is not a
    /// valid credential here, and a guest token is not valid anywhere else.
    /// </summary>
    [Authorize(AuthenticationSchemes = GuestAccessDefaults.AuthenticationScheme, Policy = AuthorizationPolicies.GuestAccess)]
    [HttpGet("v{version:apiVersion}/auth/guest-session")]
    [EndpointSummary("Introspect the current guest session")]
    [EndpointDescription("Returns the caller's guest session id, role and the enumerated guest permissions. Requires the guest bearer credential.")]
    [ProducesResponseType<GuestSessionStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetCurrent(CancellationToken ct)
    {
        if (!User.TryGetGuestId(out var guestSessionId))
        {
            // Unreachable behind the GuestAccess policy; kept as defense in depth.
            return Forbid();
        }

        return Ok(new GuestSessionStatus
        {
            GuestSessionId = guestSessionId,
            Role = GuestIdentity.RoleName,
            Permissions = User.GetGuestPermissions()
        });
    }
}
