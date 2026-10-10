using Asp.Versioning;

using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Event-driven billing monitoring surface (issue #396): read-only feeds over the two
///     existing durable billing event stores — the provider webhook inbox
///     (<see cref="BillingWebhookEvent" /> rows) and the platform outbox read model scoped
///     to Commerce.Billing durable integration events. Mutations stay on their existing
///     endpoints (webhook retry, replay). Requires the SystemAdmin policy, matching the
///     sibling billing webhook inspection and security monitoring surfaces.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/events")]
[Microsoft.AspNetCore.Http.Tags("billing/events")]
[Authorize(Policy = Policies.SystemAdmin)]
public sealed class BillingEventsController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     List billing webhook inbox events
    /// </summary>
    /// <param name="status">Optional status filter: processed, failed or pending.</param>
    /// <param name="provider">Optional provider filter (e.g. stripe).</param>
    /// <param name="eventType">Optional provider event type filter (e.g. invoice.payment_succeeded).</param>
    /// <param name="fromUtc">Optional inclusive lower bound on the acceptance date (ISO-8601).</param>
    /// <param name="toUtc">Optional inclusive upper bound on the acceptance date (ISO-8601).</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take (1-100, default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged feed of durable webhook inbox events, newest first.</returns>
    [HttpGet]
    [EndpointSummary("List billing webhook inbox events")]
    [EndpointDescription(
        "Pages through the durable billing webhook inbox: provider events accepted from Stripe, PayPal, Apple App Store and Google Pay, with their processing status, attempt counters and error details. Supports filtering by status (processed/failed/pending), provider, provider event type and acceptance date range. Payload bodies are never returned.")]
    [ProducesResponseType<PagedResult<BillingWebhookEventListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEvents(
        [FromQuery] string? status = null,
        [FromQuery] string? provider = null,
        [FromQuery] string? eventType = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListBillingEventsQuery(status, provider, eventType, fromUtc, toUtc, skip, take),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get a billing webhook inbox event by id
    /// </summary>
    /// <param name="eventId">Inbox row identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook event details including processing status and error information.</returns>
    [HttpGet("{eventId:guid}")]
    [EndpointSummary("Get a billing webhook inbox event by id")]
    [EndpointDescription(
        "Retrieves one durable webhook inbox event by its local identifier, including processing status, attempt count, error message and tenant/subscription references.")]
    [ProducesResponseType<BillingWebhookEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(Guid eventId, CancellationToken ct)
    {
        var webhookEvent = await sender.Send(new GetWebhookEventQuery(eventId.ToString()), ct).ConfigureAwait(false);
        return webhookEvent is null ? NotFound() : Ok(webhookEvent);
    }

    /// <summary>
    ///     List durable billing integration events from the platform outbox
    /// </summary>
    /// <param name="eventName">Optional stable event name filter (e.g. commerce.billing.invoice-paid.v1).</param>
    /// <param name="status">Optional delivery status filter: pending, completed or deadlettered.</param>
    /// <param name="fromUtc">Optional inclusive lower bound on the occurrence date (ISO-8601).</param>
    /// <param name="toUtc">Optional inclusive upper bound on the occurrence date (ISO-8601).</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take (1-100, default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged feed of Commerce.Billing durable integration events, newest first.</returns>
    [HttpGet("outbox")]
    [EndpointSummary("List durable billing integration events from the platform outbox")]
    [EndpointDescription(
        "Pages through the named billing integration events (webhook processed/failed, invoice paid, subscription renewed/cancelled) recorded in the platform durable outbox, with their delivery status. Dead-lettered events can be replayed through the platform admin event transport endpoints.")]
    [ProducesResponseType<PagedResult<BillingOutboxEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListOutboxEvents(
        [FromQuery] string? eventName = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListBillingOutboxEventsQuery(eventName, status, fromUtc, toUtc, skip, take),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get one durable billing integration event by id
    /// </summary>
    /// <param name="eventId">Durable event identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The billing outbox event read model.</returns>
    [HttpGet("outbox/{eventId:guid}")]
    [EndpointSummary("Get one durable billing integration event by id")]
    [EndpointDescription(
        "Retrieves a single named billing integration event from the platform outbox read model by its durable event identifier.")]
    [ProducesResponseType<BillingOutboxEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOutboxEvent(Guid eventId, CancellationToken ct)
    {
        var outboxEvent = await sender.Send(new GetBillingOutboxEventQuery(eventId), ct).ConfigureAwait(false);
        return outboxEvent is null ? NotFound() : Ok(outboxEvent);
    }
}
