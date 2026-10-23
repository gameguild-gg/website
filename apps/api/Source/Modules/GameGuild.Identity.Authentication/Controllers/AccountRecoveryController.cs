using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Delegated account recovery with trusted contacts / passkeys (issue #209):
///     request (email, enumeration-safe) → K-of-N approvals (contact codes and/or a
///     surviving passkey) within a window → single-use forced-credential-reset token.
///     Approval and reset actions are anonymous; authorization rests on single-use,
///     expiring, hash-matched tokens and verified WebAuthn assertions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/account-recovery")]
[Tags("auth/account-recovery")]
[Authorize]
public sealed class AccountRecoveryController(ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Starts a delegated account recovery for an email address.
    /// </summary>
    /// <param name="body">Email of the account to recover</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic confirmation; does not reveal whether the account exists</returns>
    [AllowAnonymous]
    [HttpPost("request")]
    [EndpointSummary("Start account recovery")]
    [EndpointDescription("Opens a delegated account recovery for an email address. Confirmed trusted contacts receive single-use recovery codes after a cooling-off window. Always returns a generic success response to prevent user enumeration.")]
    [ProducesResponseType<AccountRecoveryRequestResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestAccountRecovery([FromBody] AccountRecoveryRequestRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new RequestAccountRecoveryCommand
        {
            Email = body.Email,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Cancels the acting user's live recovery request (protective action while the
    ///     owner still has access).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("cancel")]
    [EndpointSummary("Cancel account recovery")]
    [EndpointDescription("Cancels the acting user's live recovery request and kills any minted reset token.")]
    [ProducesResponseType<AccountRecoveryCancelResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelAccountRecovery(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new CancelAccountRecoveryCommand
        {
            OwnerUserId = userId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Submits one trusted contact's single-use recovery code.
    /// </summary>
    /// <param name="body">Recovery code from the contact's email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic outcome; the remaining wait is reported for valid codes still in cooldown</returns>
    [AllowAnonymous]
    [HttpPost("approve")]
    [EndpointSummary("Approve account recovery with a contact code")]
    [EndpointDescription("Redeems a trusted contact's single-use recovery code after the cooling-off window. Denials are generic and never reveal code or request state.")]
    [ProducesResponseType<AccountRecoveryApprovalResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<AccountRecoveryApprovalResult>(425)]
    public async Task<IActionResult> ApproveAccountRecovery([FromBody] ApproveAccountRecoveryRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new ApproveAccountRecoveryCommand
        {
            ApprovalCode = body.ApprovalCode,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        var response = new AccountRecoveryApprovalResponse
        {
            Success = result.Success,
            Message = result.Message,
            ResetUnlocked = result.ResetUnlocked
        };
        return result.RetryAfterSeconds is { } seconds
            ? StatusCode(425, response with { RetryAfterSeconds = seconds })
            : Ok(response);
    }

    /// <summary>
    ///     Begins a passkey assertion for recovery approval: an enrolled WebAuthn
    ///     credential on a surviving device can supply one approval factor.
    /// </summary>
    /// <param name="body">Optional account email to scope the allowed credentials</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Options to pass to navigator.credentials.get()</returns>
    [AllowAnonymous]
    [HttpPost("approve-passkey:begin")]
    [EndpointSummary("Begin passkey recovery approval")]
    [EndpointDescription("Issues WebAuthn assertion options so an enrolled passkey on a surviving device can approve a live recovery request. Issues no sign-in credentials.")]
    [ProducesResponseType(typeof(WebAuthnAuthenticationOptionsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BeginPasskeyApproval([FromBody] BeginPasskeyApprovalRequest? body, CancellationToken ct)
    {
        var result = await sender.Send(new BeginPasskeyRecoveryApprovalCommand
        {
            Email = body?.Email,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    ///     Completes a passkey assertion for recovery approval. The assertion is
    ///     verified cryptographically and never issues sign-in credentials.
    /// </summary>
    /// <param name="body">Assertion response from the browser</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic outcome; the remaining wait is reported for valid proofs still in cooldown</returns>
    [AllowAnonymous]
    [HttpPost("approve-passkey:complete")]
    [EndpointSummary("Complete passkey recovery approval")]
    [EndpointDescription("Verifies the WebAuthn assertion against an enrolled credential and records one approval for the live recovery request. Denials are generic and never reveal request state.")]
    [ProducesResponseType<AccountRecoveryApprovalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<AccountRecoveryApprovalResponse>(425)]
    public async Task<IActionResult> CompletePasskeyApproval([FromBody] CompletePasskeyApprovalRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new ApproveAccountRecoveryWithPasskeyCommand
        {
            AssertionResponse = body.AssertionResponse,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        var response = new AccountRecoveryApprovalResponse
        {
            Success = result.Success,
            Message = result.Message,
            ResetUnlocked = result.ResetUnlocked
        };
        return result.RetryAfterSeconds is { } seconds
            ? StatusCode(425, response with { RetryAfterSeconds = seconds })
            : Ok(response);
    }

    /// <summary>
    ///     Completes the forced credential reset with the token minted at the approval
    ///     threshold: sets a new password, terminates all sessions, revokes all refresh
    ///     tokens and revokes every passkey (mandatory re-enrollment).
    /// </summary>
    /// <param name="body">Reset token and the new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic outcome; does not reveal token state</returns>
    [AllowAnonymous]
    [HttpPost("reset")]
    [EndpointSummary("Complete the forced credential reset")]
    [EndpointDescription("Consumes the single-use reset token, sets the new password, terminates all sessions, revokes all refresh tokens and revokes every passkey. Denials are generic and never reveal token state.")]
    [ProducesResponseType<AccountRecoveryResetResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteReset([FromBody] CompleteAccountRecoveryResetRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new CompleteAccountRecoveryResetCommand
        {
            ResetToken = body.ResetToken,
            NewPassword = body.NewPassword,
            ConfirmPassword = body.ConfirmPassword,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        return Ok(result);
    }
}

public sealed record AccountRecoveryRequestRequest
{
    /// <summary>Email of the account to recover.</summary>
    public required string Email { get; init; }
}

public sealed record ApproveAccountRecoveryRequest
{
    /// <summary>Single-use recovery code from the contact's email.</summary>
    public required string ApprovalCode { get; init; }
}

public sealed record BeginPasskeyApprovalRequest
{
    /// <summary>Optional account email used to scope allowed credentials.</summary>
    public string? Email { get; init; }
}

public sealed record CompletePasskeyApprovalRequest
{
    /// <summary>The browser assertion response for the passkey ceremony.</summary>
    public required string AssertionResponse { get; init; }
}

public sealed record CompleteAccountRecoveryResetRequest
{
    /// <summary>Single-use reset token from the threshold email.</summary>
    public required string ResetToken { get; init; }

    /// <summary>The new password.</summary>
    public required string NewPassword { get; init; }

    /// <summary>Confirmation of the new password.</summary>
    public required string ConfirmPassword { get; init; }
}

public sealed record AccountRecoveryApprovalResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Seconds remaining in the cooling-off window when the request is too early.</summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>True when this approval reached the K-of-N threshold and unlocked the forced reset.</summary>
    public bool ResetUnlocked { get; init; }
}
