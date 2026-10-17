using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Lost-MFA-device recovery: request (email) → continue (request token, after the
///     cooling-off window) → confirm (confirmation token). All actions are anonymous;
///     authorization rests on single-use, expiring, hash-matched tokens.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/multi-factor")]
[Authorize]
public sealed class MfaRecoveryController(ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Start a lost-MFA-device recovery for an email address.
    /// </summary>
    /// <param name="body">Email of the account to recover</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic confirmation; does not reveal whether the account exists</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/mfa/recovery:request")]
    [EndpointSummary("Start MFA recovery")]
    [EndpointDescription("Opens a lost-MFA-device recovery request for an email address and sends a recovery email with a cooling-off period. Always returns a generic success response to prevent user enumeration.")]
    [ProducesResponseType<MfaRecoveryRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestMfaRecovery([FromBody] MfaRecoveryRequestRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new RequestMfaRecoveryCommand { Email = body.Email }, ct).ConfigureAwait(false);
        return Ok(new MfaRecoveryRequestResponse { Success = result.Success, Message = result.Message });
    }

    /// <summary>
    ///     Continue a recovery after the cooling-off window using the request token from the first email.
    /// </summary>
    /// <param name="body">Request token received by email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic outcome; the remaining wait is reported for valid tokens still in cooldown</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/mfa/recovery:continue")]
    [EndpointSummary("Continue MFA recovery")]
    [EndpointDescription("Exchanges the recovery request token (after the cooling-off window) for a confirmation email. Denials are generic and never reveal token state.")]
    [ProducesResponseType<MfaRecoveryContinueResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MfaRecoveryContinueResponse>(425)]
    public async Task<IActionResult> ContinueMfaRecovery([FromBody] MfaRecoveryContinueRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new ContinueMfaRecoveryCommand { RequestToken = body.RequestToken }, ct).ConfigureAwait(false);
        var response = new MfaRecoveryContinueResponse { Success = result.Success, Message = result.Message };
        return result.RetryAfterSeconds is { } seconds
            ? StatusCode(425, response with { RetryAfterSeconds = seconds })
            : Ok(response);
    }

    /// <summary>
    ///     Complete a recovery using the confirmation token from the second email; disables MFA.
    /// </summary>
    /// <param name="body">Confirmation token received by email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic outcome; does not reveal token state</returns>
    [AllowAnonymous]
    [HttpPost("v{version:apiVersion}/auth/mfa/recovery:confirm")]
    [EndpointSummary("Confirm MFA recovery")]
    [EndpointDescription("Consumes the single-use confirmation token and disables multi-factor authentication for the recovered account. Denials are generic and never reveal token state.")]
    [ProducesResponseType<MfaRecoveryConfirmResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmMfaRecovery([FromBody] MfaRecoveryConfirmRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new ConfirmMfaRecoveryCommand { ConfirmationToken = body.ConfirmationToken }, ct).ConfigureAwait(false);
        return Ok(new MfaRecoveryConfirmResponse { Success = result.Success, Message = result.Message });
    }
}

public sealed record MfaRecoveryRequestRequest
{
    /// <summary>Email of the account to recover.</summary>
    public required string Email { get; init; }
}

public sealed record MfaRecoveryRequestResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }
}

public sealed record MfaRecoveryContinueRequest
{
    /// <summary>Request token from the first recovery email.</summary>
    public required string RequestToken { get; init; }
}

public sealed record MfaRecoveryContinueResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Seconds remaining in the cooling-off window when the request is too early.</summary>
    public int? RetryAfterSeconds { get; init; }
}

public sealed record MfaRecoveryConfirmRequest
{
    /// <summary>Confirmation token from the second recovery email.</summary>
    public required string ConfirmationToken { get; init; }
}

public sealed record MfaRecoveryConfirmResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }
}
