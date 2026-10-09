using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     External logins API - list, link, and unlink external identity providers
///     (Google, Discord) for the authenticated user.
/// </summary>
/// <remarks>
///     Rate limited under the Authentication policy like all auth endpoints.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth")]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
[Authorize]
public sealed class ExternalLoginsController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     List the external logins linked to the current user, newest first.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Metadata-only response: providers reported via the X-Linked-Providers header, no body</returns>
    [HttpHead("v{version:apiVersion}/auth/external-logins")]
    [EndpointSummary("List linked external logins")]
    [EndpointDescription("HEAD request per Google REST guidance: safe, metadata-only response with no body. Linked providers and their linked-at timestamps are conveyed in the X-Linked-Providers response header as comma-separated 'provider=iso8601-timestamp' pairs, newest first. The header is omitted when no providers are linked. Granted OAuth scopes (issue #250) are conveyed in the X-Granted-Scopes header as comma-separated 'provider=iso8601-consent-timestamp|consent-version|url-encoded-space-separated-scopes' triples, one entry per link with a recorded consent (consent-version 0 marks a legacy row); the header is omitted when no consents are recorded.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetExternalLogins(CancellationToken ct)
    {
        try
        {
            var query = new GetExternalLoginsQuery { UserId = GetUserId() };
            var result = await sender.Send(query, ct).ConfigureAwait(false);

            if (result.Count > 0)
            {
                Response.Headers["X-Linked-Providers"] = string.Join(
                    ",",
                    result.Select(l => $"{l.Provider}={DateTime.SpecifyKind(l.CreatedAt, DateTimeKind.Utc):O}"));
            }

            var consented = result
                .Where(l => l.ConsentedAt is not null)
                .Select(l => $"{l.Provider}={DateTime.SpecifyKind(l.ConsentedAt!.Value, DateTimeKind.Utc):O}|{l.ConsentVersion}|{Uri.EscapeDataString(string.Join(" ", l.GrantedScopes))}")
                .ToList();
            if (consented.Count > 0)
            {
                Response.Headers["X-Granted-Scopes"] = string.Join(",", consented);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    /// <summary>
    ///     Preview the scopes a provider link flow will request (authorization-time consent screen data).
    /// </summary>
    /// <param name="provider">Provider name (e.g. google, discord)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Provider and the exact scope tokens the authorization request will carry</returns>
    [HttpGet("v{version:apiVersion}/auth/external-logins/{provider}/link-preview")]
    [EndpointSummary("Preview link consent scopes")]
    [EndpointDescription("Returns the exact OAuth scopes the provider link flow will request, resolved the same way the authorize step embeds them in the authorization URL. Powers the authorization-time consent surface shown before linking (issue #250).")]
    [ProducesResponseType<ExternalLoginLinkPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LinkPreview(string provider, CancellationToken ct)
    {
        try
        {
            var query = new GetExternalLoginLinkPreviewQuery { Provider = provider };
            var result = await sender.Send(query, ct).ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    /// <summary>
    ///     Link the current user's Google account from a Google ID token.
    /// </summary>
    /// <param name="body">Google ID token from a GIS credential flow</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/auth/external-logins/google")]
    [EndpointSummary("Link Google account")]
    [EndpointDescription("Verifies a Google ID token and links the Google identity to the authenticated user. Idempotent when already linked to the same user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkGoogle([FromBody] LinkGoogleAccountRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var command = new LinkGoogleAccountCommand { UserId = GetUserId(), IdToken = body.IdToken };
            await sender.Send(command, ct).ConfigureAwait(false);

            return NoContent();
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    /// <summary>
    ///     Start the Discord link flow for the current user.
    /// </summary>
    /// <param name="body">Redirect URI to return to after Discord authorization</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Discord authorization URL and the per-request state parameter</returns>
    [HttpPost("v{version:apiVersion}/auth/external-logins/discord:link-authorize")]
    [EndpointSummary("Start Discord account link")]
    [EndpointDescription("Returns the Discord OAuth authorization URL plus the state parameter to validate at the callback.")]
    [ProducesResponseType<DiscordLinkAuthorizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DiscordLinkAuthorize([FromBody] DiscordLinkAuthorizeRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var command = new DiscordLinkAuthorizeCommand { RedirectUri = body.RedirectUri };
            var result = await sender.Send(command, ct).ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    /// <summary>
    ///     Complete the Discord link flow for the current user.
    /// </summary>
    /// <param name="body">Authorization code, state, and the redirect URI used at authorize time</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/auth/external-logins/discord:link-callback")]
    [EndpointSummary("Complete Discord account link")]
    [EndpointDescription("Exchanges the Discord authorization code for the user profile and links the Discord identity to the authenticated user. Idempotent when already linked to the same user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DiscordLinkCallback([FromBody] DiscordLinkCallbackRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var command = new LinkDiscordAccountCommand
            {
                UserId = GetUserId(),
                Code = body.Code,
                State = body.State,
                RedirectUri = body.RedirectUri
            };
            await sender.Send(command, ct).ConfigureAwait(false);

            return NoContent();
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    /// <summary>
    ///     Revoke individual OAuth scope grants on a linked provider.
    /// </summary>
    /// <param name="provider">Provider name (e.g. google, discord)</param>
    /// <param name="body">Scope tokens to revoke</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The remaining grant state of the link</returns>
    [HttpPost("v{version:apiVersion}/auth/external-logins/{provider}/scopes:revoke")]
    [EndpointSummary("Revoke granted OAuth scopes")]
    [EndpointDescription("Revokes individual scope grants on a linked provider without unlinking it; the remaining grant state is returned. Idempotent for scopes that are not currently granted. Revoking every remaining scope leaves the link in place with an empty grant list — whole-provider revocation remains the unlink endpoint (DELETE /external-logins/{provider}), which removes the link entirely.")]
    [ProducesResponseType<RevokeExternalLoginScopesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeScopes(string provider, [FromBody] RevokeExternalLoginScopesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            var command = new RevokeExternalLoginScopesCommand
            {
                UserId = GetUserId(),
                Provider = provider,
                Scopes = body.Scopes
            };
            var result = await sender.Send(command, ct).ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    /// <summary>
    ///     Unlink an external provider from the current user.
    /// </summary>
    /// <param name="provider">Provider name (e.g. google, discord)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/auth/external-logins/{provider}")]
    [EndpointSummary("Unlink external login")]
    [EndpointDescription("Removes the external login link for the given provider — this is the whole-provider full revocation of every granted scope and the recorded consent. Refused with 400 when it is the user's last sign-in method and no password is set.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlink(string provider, CancellationToken ct)
    {
        try
        {
            var command = new UnlinkExternalLoginCommand { UserId = GetUserId(), Provider = provider };
            await sender.Send(command, ct).ConfigureAwait(false);

            return NoContent();
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex);
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user context");
        }

        return userId;
    }

    private IActionResult ProblemFrom(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException e => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = e.Message
            }),
            ExternalLoginConflictException e => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = e.Message
            }),
            LastSignInMethodException e => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request",
                Detail = e.Message
            }),
            ExternalLoginNotFoundException e => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = e.Message
            }),
            InvalidOAuthScopeException e => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request",
                Detail = e.Message
            }),
            NotSupportedException e => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request",
                Detail = e.Message
            }),
            InvalidOperationException e => new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "External login provider not configured",
                Detail = e.Message
            })
            { StatusCode = StatusCodes.Status503ServiceUnavailable },
            _ => throw ex
        };
    }
}

/// <summary>
///     Response for the Discord link-authorize endpoint.
/// </summary>
public sealed record DiscordLinkAuthorizeResponse
{
    /// <summary>
    ///     Discord OAuth authorization URL (state embedded).
    /// </summary>
    public required string AuthUrl { get; init; }

    /// <summary>
    ///     Per-request state parameter to validate at the callback.
    /// </summary>
    public required string State { get; init; }
}
