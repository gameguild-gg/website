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
///     Authentication API Controller - RESTful API for user authentication and token management
/// </summary>
/// <remarks>
///     Rate limited to 10 requests per minute per client to prevent brute-force attacks.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth")]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
[Authorize]
public sealed class AuthController(ISender sender) : BaseApiController
{
    #region Registration Operations - /v1/auth/sign-up

    /// <summary>
    ///     Register a new user with email and password
    /// </summary>
    /// <param name="body">User registration details including email, password, and optional username</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication response with access and refresh tokens</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/sign-up")]
    [EndpointSummary("Register a new user")]
    [EndpointDescription("Creates a new user account with email and password credentials, returning authentication tokens on success.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LocalSignUp([FromBody] LocalSignUpRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new LocalSignUpCommand
        {
            Email = body.Email,
            Password = body.Password,
            Username = body.Username,
            TenantId = body.TenantId
        };

        SignInResponse result = await sender.Send(command, ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(LocalSignUp), result);
    }

    #endregion

    #region Sign-In Operations - /v1/auth/sign-in

    /// <summary>
    ///     Authenticate a user with email and password
    /// </summary>
    /// <param name="body">User login credentials</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication response with access and refresh tokens</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/sign-in")]
    [EndpointSummary("Sign in with email and password")]
    [EndpointDescription("Authenticates a user with email and password credentials, returning access and refresh tokens.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LocalSignIn([FromBody] LocalSignInRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new LocalSignInCommand
        {
            Email = body.Email,
            Password = body.Password,
            TenantId = body.TenantId
        };

        return await ExecuteAuthCommandAsync(command, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Authenticate a user using Google ID Token
    /// </summary>
    /// <param name="body">Google ID Token validation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication response with access and refresh tokens</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/google:sign-in")]
    [EndpointSummary("Sign in with Google ID Token")]
    [EndpointDescription("Authenticates a user using a Google ID Token (for NextAuth.js integration), returning access and refresh tokens. Account-linking counterpart: POST /v1/auth/external-logins/google.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleIdTokenSignIn([FromBody] GoogleIdTokenRequestDto body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new GoogleIdTokenSignInCommand
        {
            IdToken = body.IdToken,
            TenantId = body.TenantId
        };

        return await ExecuteAuthCommandAsync(command, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Request a passwordless magic sign-in link.
    /// </summary>
    /// <param name="body">Magic-link request with email and optional tenant context</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic confirmation; does not reveal whether the account exists</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/magic-link:request")]
    [EndpointSummary("Request magic sign-in link")]
    [EndpointDescription("Generates a short-lived one-time sign-in token and dispatches the magic-link notification. Always returns a generic success response to prevent user enumeration.")]
    [ProducesResponseType<MagicLinkRequestResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestMagicLink([FromBody] RequestMagicLinkRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new RequestMagicLinkCommand
        {
            Email = body.Email,
            TenantId = body.TenantId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Consume a passwordless magic sign-in link.
    /// </summary>
    /// <param name="body">Magic-link token and optional tenant/device context</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication response with access and refresh tokens</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/magic-link:consume")]
    [EndpointSummary("Consume magic sign-in link")]
    [EndpointDescription("Consumes a short-lived one-time magic-link token and returns access and refresh tokens.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConsumeMagicLink([FromBody] ConsumeMagicLinkRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new ConsumeMagicLinkCommand
        {
            Token = body.Token,
            TenantId = body.TenantId,
            DeviceFingerprint = body.DeviceFingerprint,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        return await ExecuteAuthCommandAsync(command, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Initiate GitHub OAuth sign-in
    /// </summary>
    /// <param name="redirectUri">Redirect URI after authentication</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>GitHub OAuth authorization URL</returns>
    [AllowAnonymous]
    [HttpGet("v{version:apiVersion}/auth/github:authorize")]
    [EndpointSummary("Initiate GitHub OAuth sign-in")]
    [EndpointDescription("Initiates GitHub OAuth authentication flow and returns the authorization URL.")]
    [ProducesResponseType<GitHubSignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GitHubSignIn([FromQuery] string redirectUri, CancellationToken ct)
    {
        var command = new GitHubSignInCommand { RedirectUri = redirectUri };
        var result = await sender.Send(command, ct).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion

    #region Discord OAuth sign-in - /v1/auth/discord

    /// <summary>
    ///     Initiate Discord OAuth sign-in
    /// </summary>
    /// <param name="body">Redirect URI registered for the Discord application</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Discord OAuth authorization URL and state parameter</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/discord:sign-in-authorize")]
    [EndpointSummary("Initiate Discord OAuth sign-in")]
    [EndpointDescription("Initiates the Discord OAuth authorization-code sign-in flow and returns the authorization URL with the CSRF state parameter. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-authorize.")]
    [ProducesResponseType<DiscordSignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DiscordAuthorize([FromBody] DiscordAuthorizeRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new DiscordSignInCommand { RedirectUri = body.RedirectUri };

        try
        {
            var result = await sender.Send(command, ct).ConfigureAwait(false);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return DiscordNotConfigured(ex);
        }
    }

    /// <summary>
    ///     Handle Discord OAuth callback
    /// </summary>
    /// <param name="body">Discord callback with authorization code, state, redirect URI, and optional tenant context</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication tokens on success</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/discord:sign-in-callback")]
    [EndpointSummary("Discord OAuth sign-in callback")]
    [EndpointDescription("Exchanges the Discord OAuth authorization code for access and refresh tokens, applying the same account matching and auto-link policy as Google sign-in. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-callback.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DiscordCallback([FromBody] DiscordCallbackRequestDto body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new DiscordCallbackCommand
        {
            Code = body.Code,
            State = body.State,
            RedirectUri = body.RedirectUri,
            TenantId = body.TenantId
        };

        try
        {
            SignInResponse result = await sender.Send(command, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return DiscordNotConfigured(ex);
        }
    }

    private ObjectResult DiscordNotConfigured(InvalidOperationException ex)
    {
        return Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Discord OAuth is not configured",
            detail: ex.Message);
    }

    #endregion

    #region Token Operations - /v1/auth/tokens

    /// <summary>
    ///     Refresh access token using a valid refresh token
    /// </summary>
    /// <param name="body">Token refresh request with refresh token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>New authentication response with refreshed tokens</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/tokens:refresh")]
    [EndpointSummary("Refresh access token")]
    [EndpointDescription("Exchanges a valid refresh token for a new access token and refresh token pair.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new RefreshTokenCommand
        {
            RefreshToken = body.RefreshToken,
            TenantId = body.TenantId
        };

        return await ExecuteAuthCommandAsync(command, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Revoke a refresh token to invalidate it
    /// </summary>
    /// <param name="body">Token revocation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [Authorize]
    [HttpPost("v{version:apiVersion}/auth/tokens:revoke")]
    [EndpointSummary("Revoke refresh token")]
    [EndpointDescription("Invalidates a refresh token, preventing it from being used to obtain new access tokens.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeRefreshTokenRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new RevokeTokenCommand
        {
            RefreshToken = body.Token,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    private async Task<IActionResult> ExecuteAuthCommandAsync<TCommand>(TCommand command, CancellationToken ct)
        where TCommand : IRequest<SignInResponse>
    {
        try
        {
            SignInResponse result = await sender.Send(command, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Web3 Operations - /v1/auth/web3

    /// <summary>
    ///     Generate Web3 challenge for wallet authentication
    /// </summary>
    /// <param name="body">Web3 challenge request with wallet address</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Challenge string for wallet signing</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/web3/challenge")]
    [EndpointSummary("Generate Web3 authentication challenge")]
    [EndpointDescription("Generates a cryptographic challenge that must be signed by the user's wallet to prove ownership.")]
    [ProducesResponseType<Web3ChallengeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateWeb3Challenge([FromBody] Web3ChallengeRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new GenerateWeb3ChallengeCommand
        {
            WalletAddress = body.WalletAddress,
            ChainId = body.ChainId
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        return Ok(result);
    }

    #endregion

    #region Email Verification - /v1/auth/email

    /// <summary>
    ///     Send email verification to user
    /// </summary>
    /// <param name="body">Email verification request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/email:send-verification")]
    [EndpointSummary("Send email verification")]
    [EndpointDescription("Sends a verification email to the specified email address to confirm ownership.")]
    [ProducesResponseType<EmailVerificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendEmailVerification([FromBody] SendEmailVerificationRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new SendEmailVerificationCommand { Email = body.Email };
        var result = await sender.Send(command, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Verify email address with token
    /// </summary>
    /// <param name="body">Email verification request with token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Verification result</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/email:verify")]
    [EndpointSummary("Verify email with token")]
    [EndpointDescription("Verifies the user's email address using a token received via email.")]
    [ProducesResponseType<EmailVerificationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new VerifyEmailCommand
        {
            Token = body.Token,
            TenantId = body.TenantId
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    #endregion

    #region Password Management - /v1/auth/password

    /// <summary>
    ///     Request password reset
    /// </summary>
    /// <param name="body">Password reset request with email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Confirmation (always succeeds for security)</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/password:reset-request")]
    [EndpointSummary("Request password reset")]
    [EndpointDescription("Sends a password reset link to the specified email address. Always returns success for security.")]
    [ProducesResponseType<PasswordResetRequestResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new RequestPasswordResetCommand
        {
            Email = body.Email,
            TenantId = body.TenantId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Complete password reset with token
    /// </summary>
    /// <param name="body">Password reset with token and new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Reset result</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/password:reset")]
    [EndpointSummary("Complete password reset")]
    [EndpointDescription("Resets the user's password using a token received via email.")]
    [ProducesResponseType<PasswordResetResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] CompletePasswordResetRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new ResetPasswordCommand
        {
            Token = body.Token,
            NewPassword = body.NewPassword,
            ConfirmPassword = body.ConfirmPassword,
            TenantId = body.TenantId
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    ///     Change password for authenticated user
    /// </summary>
    /// <param name="body">Current and new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Change result</returns>
    [Authorize]
    [HttpPost("v{version:apiVersion}/auth/password:change")]
    [EndpointSummary("Change password")]
    [EndpointDescription("Changes the password for the currently authenticated user.")]
    [ProducesResponseType<PasswordChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] PasswordChangeRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        // Get user ID from claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid user context");
        }

        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = body.CurrentPassword,
            NewPassword = body.NewPassword,
            ConfirmPassword = body.ConfirmPassword,
            RevokeOtherSessions = body.RevokeOtherSessions
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    #endregion

    #region GitHub OAuth - /v1/auth/github

    /// <summary>
    ///     Handle GitHub OAuth callback
    /// </summary>
    /// <param name="code">OAuth authorization code</param>
    /// <param name="state">State parameter for CSRF protection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication tokens on success</returns>
    [AllowAnonymous]
    [HttpGet("v{version:apiVersion}/auth/github:callback")]
    [EndpointSummary("GitHub OAuth callback")]
    [EndpointDescription("Handles the GitHub OAuth callback, exchanging the authorization code for tokens.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Authorization code is required");
        }

        var command = new GitHubCallbackCommand
        {
            Code = code,
            State = state ?? string.Empty
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    #endregion

    #region Web3 Verification - /v1/auth/web3

    /// <summary>
    ///     Verify Web3 wallet signature and authenticate
    /// </summary>
    /// <param name="body">Web3 verification request with signature</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Authentication tokens on success</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/web3:verify")]
    [EndpointSummary("Verify Web3 signature")]
    [EndpointDescription("Verifies a Web3 wallet signature against a previously issued challenge and returns authentication tokens.")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyWeb3Signature([FromBody] Web3VerifyRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new VerifyWeb3SignatureCommand
        {
            WalletAddress = body.WalletAddress,
            Signature = body.Signature,
            Nonce = body.Nonce,
            ChainId = body.ChainId,
            TenantId = body.TenantId,
            DeviceFingerprint = body.DeviceFingerprint
        };

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    #endregion
}

/// <summary>
///     Response for GitHub sign-in initiation
/// </summary>
public sealed record GitHubSignInResponse
{
    /// <summary>
    ///     GitHub OAuth authorization URL
    /// </summary>
    public required string AuthUrl { get; init; }
}

/// <summary>
///     Response for email verification request
/// </summary>
public sealed record EmailVerificationResponse
{
    /// <summary>
    ///     Success message
    /// </summary>
    public required string Message { get; init; }
}
