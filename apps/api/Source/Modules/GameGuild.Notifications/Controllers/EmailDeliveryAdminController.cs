using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Notifications.Controllers;

/// <summary>
/// Platform-level email deliverability administration: delivery event feed, suppressions,
/// dead letters, requeue and per-notification timeline. Requires the Admin policy.
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("notifications")]
[Route("api")]
[Authorize(Policy = Policies.Admin)]
public class EmailDeliveryAdminController : BaseApiController
{
    private const int PayloadPreviewMaxLength = 500;

    private readonly IEmailDeliveryAdminService _adminService;
    private readonly ISender _sender;

    public EmailDeliveryAdminController(IEmailDeliveryAdminService adminService, ISender sender)
    {
        _adminService = adminService;
        _sender = sender;
    }

    /// <summary>
    /// Gets the delivery event feed (newest first), filterable by event type, recipient email and provider message id
    /// </summary>
    [HttpGet("v{version:apiVersion}/email-delivery/email-events")]
    [ProducesResponseType(typeof(PagedResult<EmailDeliveryEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEmailEvents(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? eventType = null,
        [FromQuery] string? email = null,
        [FromQuery] string? providerMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetEventsAsync(skip, take, eventType, email, providerMessageId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FromError(result.Error);
        }

        return Ok(new PagedResult<EmailDeliveryEventDto>(
            result.Value.Items.Select(MapToEventDto),
            result.Value.TotalCount,
            result.Value.Skip,
            result.Value.Take));
    }

    /// <summary>
    /// Gets suppressions (newest first); active-only unless includeReleased is true
    /// </summary>
    [HttpGet("v{version:apiVersion}/email-delivery/suppressions")]
    [ProducesResponseType(typeof(PagedResult<EmailSuppressionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppressions(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] bool includeReleased = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetSuppressionsAsync(skip, take, includeReleased, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FromError(result.Error);
        }

        return Ok(new PagedResult<EmailSuppressionDto>(
            result.Value.Items.Select(MapToSuppressionDto),
            result.Value.TotalCount,
            result.Value.Skip,
            result.Value.Take));
    }

    /// <summary>
    /// Releases the active suppression for an address (admin unsuppress). Idempotent: returns 200 when no active suppression exists.
    /// </summary>
    [HttpDelete("v{version:apiVersion}/email-delivery/suppressions/{email}")]
    [ProducesResponseType(typeof(UnsuppressResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReleaseSuppression([FromRoute] string email, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ReleaseEmailSuppressionCommand(email), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FromError(result.Error);
        }

        return Ok(new UnsuppressResponse(EmailAddressNormalizer.Normalize(email), result.Value));
    }

    /// <summary>
    /// Gets dead-lettered notifications (newest first), filterable by notification type and recipient email
    /// </summary>
    [HttpGet("v{version:apiVersion}/email-delivery/deadletters")]
    [ProducesResponseType(typeof(PagedResult<DeadLetterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDeadLetters(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? email = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetDeadLettersAsync(skip, take, type, email, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FromError(result.Error);
        }

        return Ok(new PagedResult<DeadLetterDto>(
            result.Value.Items.Select(MapToDeadLetterDto),
            result.Value.TotalCount,
            result.Value.Skip,
            result.Value.Take));
    }

    /// <summary>
    /// Requeues a dead-lettered notification for another delivery attempt
    /// </summary>
    [HttpPost("v{version:apiVersion}/email-delivery/notifications/{id:guid}:requeue")]
    [ProducesResponseType(typeof(RequeueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Requeue([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new RequeueEmailNotificationCommand(id), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FromError(result.Error);
        }

        return Ok(new RequeueResponse(result.Value.Id, result.Value.DeliveryStatus.ToString(), result.Value.RequeueCount));
    }

    /// <summary>
    /// Gets the delivery timeline of a notification (its provider events, oldest first); empty when the row has no provider correlation id
    /// </summary>
    [HttpGet("v{version:apiVersion}/email-delivery/notifications/{id:guid}/timeline")]
    [ProducesResponseType(typeof(NotificationTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetTimelineAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return FromError(result.Error);
        }

        return Ok(new NotificationTimelineDto(id, result.Value.ProviderMessageId, result.Value.Events.Select(MapToEventDto).ToList()));
    }

    // Explicit ProblemDetails (not ControllerBase.Problem) so direct unit invocation works without ProblemDetailsFactory.
    private static ObjectResult FromError(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        return new ObjectResult(new ProblemDetails { Status = status, Title = error.Code, Detail = error.Description })
        {
            StatusCode = status
        };
    }

    private static EmailDeliveryEventDto MapToEventDto(EmailDeliveryEvent deliveryEvent)
    {
        return new EmailDeliveryEventDto(
            deliveryEvent.Id,
            deliveryEvent.ProviderMessageId,
            deliveryEvent.RecipientEmail,
            deliveryEvent.EventType.ToString(),
            deliveryEvent.OccurredAt,
            deliveryEvent.BounceType,
            deliveryEvent.DiagnosticCode,
            PayloadPreview(deliveryEvent.Payload));
    }

    private static EmailSuppressionDto MapToSuppressionDto(EmailSuppression suppression)
    {
        return new EmailSuppressionDto(
            suppression.Id,
            suppression.EmailAddress,
            suppression.Reason.ToString(),
            suppression.BounceType,
            suppression.SourceEventId,
            suppression.SuppressedAt,
            suppression.ReleasedAt,
            suppression.IsActive);
    }

    private static DeadLetterDto MapToDeadLetterDto(Notification notification)
    {
        return new DeadLetterDto(
            notification.Id,
            notification.Type.ToString(),
            notification.Channel.ToString(),
            notification.Title,
            notification.RecipientEmail,
            notification.RecipientId,
            notification.LastError,
            notification.AttemptCount,
            notification.RequeueCount,
            notification.CreatedAt);
    }

    // Never expose the raw payload: parsed BounceType/DiagnosticCode are separate DTO fields,
    // the payload itself is capped at a preview.
    private static string? PayloadPreview(string? payload) =>
        payload is null || payload.Length <= PayloadPreviewMaxLength ? payload : payload[..PayloadPreviewMaxLength];
}

#region DTOs

public sealed record EmailDeliveryEventDto(
    Guid Id,
    string ProviderMessageId,
    string RecipientEmail,
    string EventType,
    DateTime OccurredAt,
    string? BounceType,
    string? DiagnosticCode,
    string? PayloadPreview);

public sealed record EmailSuppressionDto(
    Guid Id,
    string EmailAddress,
    string Reason,
    string? BounceType,
    Guid? SourceEventId,
    DateTime SuppressedAt,
    DateTime? ReleasedAt,
    bool IsActive);

public sealed record DeadLetterDto(
    Guid Id,
    string Type,
    string Channel,
    string Title,
    string? RecipientEmail,
    Guid? RecipientId,
    string? LastError,
    int AttemptCount,
    int RequeueCount,
    DateTime CreatedAt);

public sealed record UnsuppressResponse(
    string EmailAddress,
    bool WasActive);

public sealed record RequeueResponse(
    Guid Id,
    string DeliveryStatus,
    int RequeueCount);

public sealed record NotificationTimelineDto(
    Guid NotificationId,
    string? ProviderMessageId,
    IReadOnlyList<EmailDeliveryEventDto> Events);

#endregion
