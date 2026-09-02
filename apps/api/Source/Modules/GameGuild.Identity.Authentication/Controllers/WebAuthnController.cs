using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for WebAuthn/FIDO2 passwordless authentication operations.
/// </summary>
/// <remarks>
///     Rate limited to 10 requests per minute per client to prevent abuse of authentication endpoints.
/// </remarks>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/webauthn")]
[Microsoft.AspNetCore.Http.Tags("auth/webauthn")]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
public class WebAuthnController(
    IWebAuthnService webAuthnService,
    IJwtTokenService? jwtTokenService = null,
    IUserRepository? userRepository = null,
    IConfiguration? configuration = null,
    ISender? sender = null) : BaseApiController
{
    #region Registration Endpoints

    /// <summary>
    ///     Begin WebAuthn credential registration.
    /// </summary>
    /// <param name="request">Registration request with optional authenticator preferences.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Options to pass to navigator.credentials.create().</returns>
    [HttpPost("registration:begin")]
    [Authorize]
    [ProducesResponseType(typeof(WebAuthnRegistrationOptionsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebAuthnRegistrationOptionsResult>> BeginRegistration(
        [FromBody] BeginWebAuthnRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await sender!.Send(new BeginWebAuthnRegistrationCommand(
            userId.Value,
            request.Email,
            request.DisplayName,
            request.PreferredAuthenticatorType),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    ///     Complete WebAuthn credential registration.
    /// </summary>
    /// <param name="request">The attestation response from the browser.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the registration.</returns>
    [HttpPost("registration:complete")]
    [Authorize]
    [ProducesResponseType(typeof(WebAuthnRegistrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebAuthnRegistrationResult>> CompleteRegistration(
        [FromBody] CompleteWebAuthnRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await sender!.Send(new CompleteWebAuthnRegistrationCommand(
            userId.Value,
            request.AttestationResponse,
            request.FriendlyName,
            request.IsPasswordless,
            GetClientIpAddress(),
            Request.Headers.UserAgent.ToString()),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    #endregion

    #region Authentication Endpoints

    /// <summary>
    ///     Begin WebAuthn authentication (passwordless login).
    /// </summary>
    /// <param name="request">Optional email to filter credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Options to pass to navigator.credentials.get().</returns>
    [HttpPost("authentication:begin")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebAuthnAuthenticationOptionsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<WebAuthnAuthenticationOptionsResult>> BeginAuthentication(
        [FromBody] BeginWebAuthnAuthenticationRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender!.Send(
            new BeginWebAuthnAuthenticationCommand(request?.Email),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    ///     Complete WebAuthn authentication (passwordless login).
    /// </summary>
    /// <param name="request">The assertion response from the browser.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result with user info.</returns>
    [HttpPost("authentication:complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebAuthnAuthenticationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WebAuthnAuthenticationResult>> CompleteAuthentication(
        [FromBody] CompleteWebAuthnAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender!.Send(new CompleteWebAuthnAuthenticationCommand(
            request.AssertionResponse,
            GetClientIpAddress(),
            Request.Headers.UserAgent.ToString()),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    #endregion

    #region Credential Management Endpoints

    /// <summary>
    ///     Get all WebAuthn credentials for the current user.
    /// </summary>
    [HttpGet("credentials")]
    [Authorize]
    [ProducesResponseType(typeof(List<WebAuthnCredentialInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<WebAuthnCredentialInfo>>> GetCredentials(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var credentials = await webAuthnService.GetUserCredentialsAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        return Ok(credentials);
    }

    /// <summary>
    ///     Get a single WebAuthn credential by ID.
    /// </summary>
    /// <param name="credentialId">The credential ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("credentials/{credentialId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(WebAuthnCredentialInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebAuthnCredentialInfo>> GetCredential(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var credential = await webAuthnService.GetCredentialByIdAsync(userId.Value, credentialId, cancellationToken).ConfigureAwait(false);
        if (credential == null)
            return NotFound();

        return Ok(credential);
    }

    /// <summary>
    ///     Check if a WebAuthn credential exists.
    /// </summary>
    /// <param name="credentialId">The credential ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpHead("credentials/{credentialId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CredentialExists(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var exists = await webAuthnService.CredentialExistsAsync(userId.Value, credentialId, cancellationToken).ConfigureAwait(false);
        return exists ? Ok() : NotFound();
    }

    /// <summary>
    ///     Verify a WebAuthn credential is valid and can be used for authentication.
    /// </summary>
    /// <param name="credentialId">The credential ID to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("credentials/{credentialId:guid}:verify")]
    [Authorize]
    [ProducesResponseType(typeof(WebAuthnCredentialVerifyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebAuthnCredentialVerifyResult>> VerifyCredential(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await sender!.Send(
            new VerifyWebAuthnCredentialCommand(userId.Value, credentialId),
            cancellationToken).ConfigureAwait(false);
        if (!result.Success && result.Error == "Credential not found")
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    ///     Delete a WebAuthn credential.
    /// </summary>
    /// <param name="credentialId">The credential ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete("credentials/{credentialId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> DeleteCredential(
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await sender!.Send(
            new DeleteWebAuthnCredentialCommand(userId.Value, credentialId),
            cancellationToken).ConfigureAwait(false);
        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    ///     Update a WebAuthn credential's friendly name.
    /// </summary>
    /// <param name="credentialId">The credential ID to update.</param>
    /// <param name="request">The new friendly name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPatch("credentials/{credentialId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> UpdateCredentialName(
        Guid credentialId,
        [FromBody] UpdateCredentialNameRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await sender!.Send(new UpdateWebAuthnCredentialNameCommand(
            userId.Value,
            credentialId,
            request.FriendlyName), cancellationToken).ConfigureAwait(false);

        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    ///     Check if current user has WebAuthn enabled.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(WebAuthnStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebAuthnStatusResponse>> GetWebAuthnStatus(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var isEnabled = await webAuthnService.IsWebAuthnEnabledAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        var credentials = await webAuthnService.GetUserCredentialsAsync(userId.Value, cancellationToken).ConfigureAwait(false);

        return Ok(new WebAuthnStatusResponse
        {
            IsEnabled = isEnabled,
            CredentialCount = credentials.Count,
            HasPasswordlessCredential = credentials.Any(c => c.IsPasswordless),
            HasPlatformAuthenticator = credentials.Any(c => c.AuthenticatorType == WebAuthnAuthenticatorType.Platform),
            HasSecurityKey = credentials.Any(c => c.AuthenticatorType == WebAuthnAuthenticatorType.CrossPlatform)
        });
    }

    #endregion

    #region Helpers

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    #endregion

    private async Task AttachAuthenticationTokensAsync(WebAuthnAuthenticationResult result, CancellationToken cancellationToken)
    {
        if (!result.Success || result.UserId is not { } userId || jwtTokenService is null || userRepository is null)
        {
            return;
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Email,
            [],
            null,
            user.TokenVersion,
            cancellationToken).ConfigureAwait(false);

        var refreshToken = await jwtTokenService.GenerateRefreshTokenAsync(
            user.Id,
            new DeviceInfo
            {
                Fingerprint = $"webauthn:{result.CredentialId?.ToString("N") ?? "unknown"}",
                IpAddress = GetClientIpAddress(),
                UserAgent = Request.Headers.UserAgent.ToString()
            },
            cancellationToken).ConfigureAwait(false);

        var accessTokenMinutes = ParsePositiveInt(configuration?["Jwt:AccessTokenExpirationMinutes"], 60);
        var refreshTokenDays = ParsePositiveInt(
            configuration?["Jwt:RefreshTokenExpirationDays"] ?? configuration?["Jwt:RefreshTokenExpiryInDays"],
            30);

        result.Email = user.Email;
        result.AccessToken = accessToken;
        result.RefreshToken = refreshToken;
        result.AccessTokenExpiresAt = SystemClock.UtcNow.AddMinutes(accessTokenMinutes);
        result.RefreshTokenExpiresAt = SystemClock.UtcNow.AddDays(refreshTokenDays);
        result.ExpiresIn = accessTokenMinutes * 60;
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }
}

#region DTOs

/// <summary>
///     Request to begin WebAuthn registration.
/// </summary>
public class BeginWebAuthnRegistrationRequest
{
    /// <summary>
    ///     User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     Display name for the credential.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Preferred authenticator type (platform or cross-platform).
    /// </summary>
    public WebAuthnAuthenticatorType? PreferredAuthenticatorType { get; set; }
}

/// <summary>
///     Request to complete WebAuthn registration.
/// </summary>
public class CompleteWebAuthnRegistrationRequest
{
    /// <summary>
    ///     The JSON attestation response from navigator.credentials.create().
    /// </summary>
    public string AttestationResponse { get; set; } = string.Empty;

    /// <summary>
    ///     Optional friendly name for the credential.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    ///     Whether this credential can be used for passwordless authentication.
    /// </summary>
    public bool IsPasswordless { get; set; }
}

/// <summary>
///     Request to begin WebAuthn authentication.
/// </summary>
public class BeginWebAuthnAuthenticationRequest
{
    /// <summary>
    ///     Optional email to filter credentials (for username-first flow).
    /// </summary>
    public string? Email { get; set; }
}

/// <summary>
///     Request to complete WebAuthn authentication.
/// </summary>
public class CompleteWebAuthnAuthenticationRequest
{
    /// <summary>
    ///     The JSON assertion response from navigator.credentials.get().
    /// </summary>
    public string AssertionResponse { get; set; } = string.Empty;
}

/// <summary>
///     Request to update a credential's friendly name.
/// </summary>
public class UpdateCredentialNameRequest
{
    /// <summary>
    ///     The new friendly name.
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;
}

/// <summary>
///     Response for WebAuthn status check.
/// </summary>
public class WebAuthnStatusResponse
{
    /// <summary>
    ///     Whether WebAuthn is enabled for this user.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Number of registered credentials.
    /// </summary>
    public int CredentialCount { get; set; }

    /// <summary>
    ///     Whether user has at least one passwordless credential.
    /// </summary>
    public bool HasPasswordlessCredential { get; set; }

    /// <summary>
    ///     Whether user has a platform authenticator (Touch ID, Windows Hello).
    /// </summary>
    public bool HasPlatformAuthenticator { get; set; }

    /// <summary>
    ///     Whether user has a security key.
    /// </summary>
    public bool HasSecurityKey { get; set; }
}

#endregion
