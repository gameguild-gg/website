using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Multi-Factor Authentication API Controller - RESTful API for MFA configuration and verification
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/multi-factor")]
[Authorize]
public sealed class MfaController(IMfaService mfaService, ISender sender) : AuthControllerBase
{
    #region Configuration Operations - /v1/auth/mfa

    /// <summary>
    ///     Get current user's MFA configuration
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>MFA configuration details including enabled methods and status</returns>
    [HttpGet("v{version:apiVersion}/auth/mfa")]
    [EndpointSummary("Get MFA configuration")]
    [EndpointDescription("Retrieves the current user's multi-factor authentication configuration and enabled methods.")]
    [ProducesResponseType<MfaConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMfaConfiguration(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        MfaConfigurationResponse configuration = await mfaService.GetMfaConfigurationAsync(userId).ConfigureAwait(false);

        return Ok(configuration);
    }

    #endregion

    #region TOTP Setup Operations - /v1/auth/mfa/totp

    /// <summary>
    ///     Initiate TOTP MFA setup
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Setup response with secret key and QR code URI</returns>
    [HttpPost("v{version:apiVersion}/auth/mfa/totp:setup")]
    [EndpointSummary("Initiate TOTP setup")]
    [EndpointDescription("Initiates Time-based One-Time Password (TOTP) setup, returning a secret key and QR code URI for authenticator apps.")]
    [ProducesResponseType<MfaSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> InitiateTotpSetup(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var userEmail = GetCurrentUserEmail();
        var result = await sender.Send(new InitiateMfaSetupCommand(userId, userEmail), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(new MfaErrorResponse { Error = string.IsNullOrEmpty(result.Message) ? "Failed to initiate MFA setup" : result.Message });
        }

        return Ok(new MfaSetupResponse
        {
            SecretKey = result.SecretKey,
            QrCodeUri = result.QrCodeUrl,
            BackupCodes = []
        });
    }

    /// <summary>
    ///     Complete TOTP MFA setup by verifying the code
    /// </summary>
    /// <param name="body">Verification code from authenticator app</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("v{version:apiVersion}/auth/mfa/totp:complete")]
    [EndpointSummary("Complete TOTP setup")]
    [EndpointDescription("Completes TOTP setup by verifying a code from the user's authenticator app.")]
    [ProducesResponseType<MfaSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteTotpSetup([FromBody] CompleteMfaSetupRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var userId = GetCurrentUserId();
        var result = await sender.Send(new CompleteMfaSetupCommand(userId, body.Code), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(new MfaErrorResponse { Error = result.Message ?? "Failed to complete MFA setup" });
        }

        return Ok(new MfaSuccessResponse { Message = "MFA setup completed successfully" });
    }

    #endregion

    #region Verification Operations - /v1/auth/mfa/verify

    /// <summary>
    ///     Verify MFA code during authentication
    /// </summary>
    /// <param name="body">MFA verification request with code and method</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Verification result</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/mfa/verify")]
    [EndpointSummary("Verify MFA code")]
    [EndpointDescription("Verifies an MFA code during the authentication flow. Used after initial sign-in when MFA is required.")]
    [ProducesResponseType<MfaVerificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new VerifyMfaCodeCommand(body.UserId, body.Code, body.Method), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(new MfaErrorResponse { Error = result.Message ?? "MFA verification failed" });
        }

        return Ok(new MfaVerificationResponse { IsValid = result.Success });
    }

    #endregion

    #region Backup Codes Operations - /v1/auth/mfa/backup-codes

    /// <summary>
    ///     Get backup codes (masked for security)
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Masked backup codes with usage status</returns>
    [HttpGet("v{version:apiVersion}/auth/mfa/backup-codes")]
    [EndpointSummary("Get backup codes")]
    [EndpointDescription("Retrieves the user's backup codes status. Codes are not returned for security; use regenerate to get new codes.")]
    [ProducesResponseType<BackupCodesStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBackupCodes(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var configuration = await mfaService.GetMfaConfigurationAsync(userId, ct).ConfigureAwait(false);

        return Ok(new BackupCodesStatusResponse
        {
            TotalCount = 10, // Standard backup code count
            RemainingCount = configuration.BackupCodesRemaining,
            UsedCount = 10 - configuration.BackupCodesRemaining,
            HasBackupCodes = configuration.BackupCodesRemaining > 0
        });
    }

    /// <summary>
    ///     Generate new backup codes (invalidates existing ones)
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>New backup codes</returns>
    [HttpPost("v{version:apiVersion}/auth/mfa/backup-codes:regenerate")]
    [EndpointSummary("Regenerate backup codes")]
    [EndpointDescription("Generates a new set of backup codes, invalidating any previously generated codes.")]
    [ProducesResponseType<BackupCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegenerateBackupCodes(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var backupCodes = await sender.Send(new RegenerateMfaBackupCodesCommand(userId), ct).ConfigureAwait(false);

        return Ok(new BackupCodesResponse
        {
            Codes = [.. backupCodes],
            GeneratedAt = SystemClock.UtcNow
        });
    }

    #endregion

    #region SMS MFA Operations - /v1/auth/mfa/sms

    /// <summary>
    ///     Initiate SMS-based MFA setup
    /// </summary>
    /// <param name="body">Phone number for SMS verification</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Setup initiation result</returns>
    [HttpPost("v{version:apiVersion}/auth/mfa/sms:setup")]
    [UnavailableEndpoint("SMS MFA is disabled until an SMS provider is configured.")]
    [EndpointSummary("Setup SMS MFA")]
    [EndpointDescription("Initiates SMS-based MFA setup by sending a verification code to the provided phone number.")]
    [ProducesResponseType<SmsMfaSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MfaErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> InitiateSmsSetup([FromBody] SmsMfaSetupRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status503ServiceUnavailable, new MfaErrorResponse
        {
            Error = "SMS MFA is not configured. Use authenticator-app TOTP or backup codes, or configure an SMS provider before enabling SMS MFA."
        }));
    }

    /// <summary>
    ///     Complete SMS MFA setup by verifying the code
    /// </summary>
    /// <param name="body">Verification code received via SMS</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Setup completion result</returns>
    [HttpPost("v{version:apiVersion}/auth/mfa/sms:complete")]
    [UnavailableEndpoint("SMS MFA is disabled until an SMS provider is configured.")]
    [EndpointSummary("Complete SMS MFA setup")]
    [EndpointDescription("Completes SMS MFA setup by verifying the code sent to the user's phone.")]
    [ProducesResponseType<MfaSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MfaErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> CompleteSmsSetup([FromBody] CompleteMfaSetupRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status503ServiceUnavailable, new MfaErrorResponse
        {
            Error = "SMS MFA is not configured. Use authenticator-app TOTP or backup codes, or configure an SMS provider before completing SMS MFA."
        }));
    }

    #endregion

    #region MFA Methods Operations - /v1/auth/mfa/methods

    /// <summary>
    ///     List available MFA methods
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of available MFA methods with their status</returns>
    [HttpGet("v{version:apiVersion}/auth/mfa/methods")]
    [EndpointSummary("List MFA methods")]
    [EndpointDescription("Returns all available MFA methods and their configuration status for the current user.")]
    [ProducesResponseType<MfaMethodsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListMfaMethods(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var configuration = await mfaService.GetMfaConfigurationAsync(userId, ct).ConfigureAwait(false);

        var enabledMethods = configuration.EnabledMethods ?? [];

        var methods = new List<MfaMethodInfo>
        {
            new()
            {
                Method = MfaMethod.Totp,
                Name = "Authenticator App (TOTP)",
                Description = "Use an authenticator app like Google Authenticator or Authy",
                IsEnabled = enabledMethods.Contains("totp", StringComparer.OrdinalIgnoreCase),
                IsAvailable = true,
                Priority = 1
            },
            new()
            {
                Method = MfaMethod.Sms,
                Name = "SMS",
                Description = "Receive verification codes via text message",
                IsEnabled = enabledMethods.Contains("sms", StringComparer.OrdinalIgnoreCase),
                IsAvailable = false,
                Priority = 2
            },
            new()
            {
                Method = MfaMethod.Email,
                Name = "Email",
                Description = "Receive verification codes via email",
                IsEnabled = enabledMethods.Contains("email", StringComparer.OrdinalIgnoreCase),
                IsAvailable = true,
                Priority = 3
            },
            new()
            {
                Method = MfaMethod.BackupCode,
                Name = "Backup Codes",
                Description = "One-time use codes for emergency access",
                IsEnabled = configuration.BackupCodesRemaining > 0,
                IsAvailable = true,
                Priority = 4
            }
        };

        var defaultMethod = enabledMethods.Contains("totp", StringComparer.OrdinalIgnoreCase) ? MfaMethod.Totp :
                           enabledMethods.Contains("sms", StringComparer.OrdinalIgnoreCase) ? MfaMethod.Sms :
                           enabledMethods.Contains("email", StringComparer.OrdinalIgnoreCase) ? MfaMethod.Email : (MfaMethod?)null;

        return Ok(new MfaMethodsResponse
        {
            Methods = methods,
            DefaultMethod = defaultMethod
        });
    }

    #endregion

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
            return "****";

        return $"***-***-{phoneNumber[^4..]}";
    }

    #region Disable Operations - /v1/auth/mfa:disable

    /// <summary>
    ///     Disable MFA for the current user
    /// </summary>
    /// <param name="body">Disable request with password confirmation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("v{version:apiVersion}/auth/mfa:disable")]
    [EndpointSummary("Disable MFA")]
    [EndpointDescription("Disables multi-factor authentication for the current user after password verification.")]
    [ProducesResponseType<MfaSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var userId = GetCurrentUserId();
        var result = await sender.Send(new DisableMfaCommand(userId, body.Password), ct).ConfigureAwait(false);

        if (!result)
        {
            return BadRequest(new MfaErrorResponse { Error = "Failed to disable MFA" });
        }

        return Ok(new MfaSuccessResponse { Message = "MFA disabled successfully" });
    }

    #endregion
}

/// <summary>
///     Success response for MFA operations
/// </summary>
public sealed record MfaSuccessResponse
{
    /// <summary>
    ///     Success message
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
///     Error response for MFA operations
/// </summary>
public sealed record MfaErrorResponse
{
    /// <summary>
    ///     Error message
    /// </summary>
    public required string Error { get; init; }
}

/// <summary>
///     Response containing backup codes status
/// </summary>
public sealed record BackupCodesStatusResponse
{
    /// <summary>
    ///     Total number of backup codes generated
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    ///     Number of codes remaining (unused)
    /// </summary>
    public required int RemainingCount { get; init; }

    /// <summary>
    ///     Number of codes that have been used
    /// </summary>
    public required int UsedCount { get; init; }

    /// <summary>
    ///     Whether the user has any backup codes
    /// </summary>
    public required bool HasBackupCodes { get; init; }
}

/// <summary>
///     Request to setup SMS MFA
/// </summary>
public sealed record SmsMfaSetupRequest
{
    /// <summary>
    ///     Phone number to receive SMS codes
    /// </summary>
    public required string PhoneNumber { get; init; }
}

/// <summary>
///     Response for SMS MFA setup initiation
/// </summary>
public sealed record SmsMfaSetupResponse
{
    /// <summary>
    ///     Status message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Masked phone number for confirmation
    /// </summary>
    public required string PhoneNumberMasked { get; init; }

    /// <summary>
    ///     Time in seconds until code expires
    /// </summary>
    public required int ExpiresInSeconds { get; init; }
}

/// <summary>
///     Information about an MFA method
/// </summary>
public sealed record MfaMethodInfo
{
    /// <summary>
    ///     The MFA method type
    /// </summary>
    public required MfaMethod Method { get; init; }

    /// <summary>
    ///     Display name of the method
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Description of the method
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///     Whether this method is enabled for the user
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    ///     Whether this method is available (e.g., SMS requires phone)
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    ///     Priority order for this method (lower = higher priority)
    /// </summary>
    public required int Priority { get; init; }
}

/// <summary>
///     Response listing available MFA methods
/// </summary>
public sealed record MfaMethodsResponse
{
    /// <summary>
    ///     List of all MFA methods
    /// </summary>
    public required List<MfaMethodInfo> Methods { get; init; }

    /// <summary>
    ///     The default/preferred MFA method for the user
    /// </summary>
    public MfaMethod? DefaultMethod { get; init; }
}
