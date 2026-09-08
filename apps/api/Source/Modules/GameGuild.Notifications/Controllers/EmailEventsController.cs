using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.SimpleNotificationService.Util;
using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.Controllers;

/// <summary>
/// Public SES → SNS email-delivery-events webhook. Every message must carry a valid AWS SNS
/// signature verified by <see cref="ISnsMessageVerifier"/> (signing cert host pinned, topic
/// allow-listed). Handles the SubscriptionConfirmation handshake and transactionally ingests
/// delivery events (SnsMessageId unique → idempotent under SNS redelivery). Raw bodies are
/// never logged; ipAddress/userAgent are stripped from stored payloads.
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("notifications")]
[Route("api")]
[AllowAnonymous]
public class EmailEventsController(ISender sender) : BaseApiController
{
    public const string SubscriptionConfirmationClientName = "SnsSubscriptionConfirmation";

    /// <summary>
    /// Receives SNS notifications for SES delivery events (send, delivery, bounce, complaint, open)
    /// </summary>
    [HttpPost("v{version:apiVersion}/notifications/email-events")]
    [EndpointSummary("SES email delivery events webhook (public, SNS signature-verified)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return await sender.Send(new ReceiveEmailEventsCommand(body), cancellationToken).ConfigureAwait(false);
    }
}
