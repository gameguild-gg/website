using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for OAuth2 client_credentials token issuance for service accounts.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("auth/service-accounts/tokens")]
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/service-accounts")]
[Produces("application/json")]
public class ServiceAccountTokenController(
    ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     OAuth2 client_credentials grant - authenticates a service account and returns a JWT token.
    /// </summary>
    /// <remarks>
    ///     This endpoint implements the OAuth2 client_credentials flow for machine-to-machine authentication.
    ///     The returned access token can be used to authenticate API requests.
    /// </remarks>
    /// <param name="request">The client credentials request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An OAuth2 token response with access token.</returns>
    [HttpPost("/v{version:apiVersion}/oauth/token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(ClientCredentialsTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OAuth2ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OAuth2ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromForm] ClientCredentialsRequest request, CancellationToken cancellationToken)
    {
        // Validate grant type
        if (request.GrantType != "client_credentials")
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "unsupported_grant_type",
                ErrorDescription = "Only 'client_credentials' grant type is supported"
            });
        }

        if (string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "client_id and client_secret are required"
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new IssueServiceAccountTokenCommand(
            request.ClientId,
            request.ClientSecret,
            ipAddress), cancellationToken).ConfigureAwait(false);

        if (result.Account == null)
        {
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Invalid client credentials"
            });
        }

        return Ok(new ClientCredentialsTokenResponse
        {
            AccessToken = result.AccessToken!,
            TokenType = "Bearer",
            ExpiresIn = (int)(result.ExpiresAt!.Value - SystemClock.UtcNow).TotalSeconds,
            Scope = result.Account.Scopes
        });
    }
}
