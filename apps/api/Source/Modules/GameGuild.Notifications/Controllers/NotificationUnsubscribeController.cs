using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Notifications.Services;
using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.Controllers;

/// <summary>
/// Public one-click unsubscribe endpoint. The token is a DataProtection-signed payload
/// (userId + scope), so no authentication is required and no userId is exposed in cleartext.
/// Idempotent: repeated clicks on the same token keep returning success.
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("notifications")]
[Route("api")]
[AllowAnonymous]
public class NotificationUnsubscribeController(ISender sender) : BaseApiController
{
    /// <summary>
    /// Processes a one-click unsubscribe: mutes a type, disables a category, or turns off email entirely
    /// </summary>
    [HttpGet("v{version:apiVersion}/notifications/unsubscribe")]
    [EndpointSummary("One-click unsubscribe (public, signed token)")]
    [ProducesResponseType(typeof(UnsubscribeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Unsubscribe([FromQuery(Name = "token")] string? token, CancellationToken cancellationToken = default)
    {
        return await sender.Send(new UnsubscribeNotificationCommand(token), cancellationToken).ConfigureAwait(false);
    }
}

public sealed record UnsubscribeResponse(
    string Status,
    string Scope,
    string? Value,
    string ManageUrl);
