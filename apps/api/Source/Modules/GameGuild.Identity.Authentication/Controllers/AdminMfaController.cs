using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Admin-assisted MFA management. The authorization decision is enforced inside
///     the command handler (tenant-admin of the target user's tenant, or SystemAdmin);
///     the controller-level <see cref="AuthorizeAttribute"/> is the outer gate only.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/multi-factor")]
[Authorize]
public sealed class AdminMfaController(ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Reset a user's multi-factor authentication: clears MFA secrets, revokes all
    ///     issued tokens, terminates sessions, and cancels any live lost-device recovery request.
    /// </summary>
    /// <param name="userId">The user whose MFA is reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Outcome of the reset, including whether MFA was enabled and a recovery was cancelled</returns>
    [HttpPost("v{version:apiVersion}/admin/users/{userId:guid}/mfa:reset")]
    [EndpointSummary("Admin-assisted MFA reset")]
    [EndpointDescription("Disables a user's multi-factor authentication through the audited shared disable path and cancels any live recovery request. Requires system administration or tenant administration of the user's tenant.")]
    [ProducesResponseType<AdminResetUserMfaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetUserMfa(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new AdminResetUserMfaCommand { UserId = userId }, ct).ConfigureAwait(false);
        return Ok(new AdminResetUserMfaResponse
        {
            Success = result.Success,
            Message = result.Message,
            MfaWasEnabled = result.MfaWasEnabled,
            LiveRecoveryCancelled = result.LiveRecoveryCancelled
        });
    }
}

public sealed record AdminResetUserMfaResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Whether MFA was enabled and has now been disabled.</summary>
    public required bool MfaWasEnabled { get; init; }

    /// <summary>Whether a live (Pending/AwaitingConfirmation) recovery request was cancelled.</summary>
    public required bool LiveRecoveryCancelled { get; init; }
}
