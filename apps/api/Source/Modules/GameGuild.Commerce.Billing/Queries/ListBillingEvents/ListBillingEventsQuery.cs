using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Processing status filter values for the billing webhook inbox event feed.
///     Matched case-insensitively from the API surface.
/// </summary>
public static class BillingEventStatusFilter
{
    /// <summary>Successfully processed events.</summary>
    public const string Processed = "processed";

    /// <summary>Events whose last processing attempt failed.</summary>
    public const string Failed = "failed";

    /// <summary>Accepted events that have neither completed nor failed yet.</summary>
    public const string Pending = "pending";

    /// <summary>All accepted filter values, lowercase.</summary>
    public static readonly IReadOnlyList<string> All = [Processed, Failed, Pending];

    /// <summary>Normalizes a caller-supplied status filter or returns null when absent.</summary>
    public static string? Normalize(string? status) =>
        string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
}

/// <summary>List item of the billing webhook inbox event feed (issue #396). Payloads are excluded.</summary>
/// <param name="Id">Local inbox row identifier.</param>
/// <param name="Provider">Payment provider that emitted the webhook.</param>
/// <param name="ExternalEventId">Provider-scoped event identifier.</param>
/// <param name="EventType">Provider event type, e.g. invoice.payment_succeeded.</param>
/// <param name="IsProcessed">Whether processing completed.</param>
/// <param name="IsFailed">Whether the last attempt failed.</param>
/// <param name="ProcessingAttempts">Number of processing attempts.</param>
/// <param name="ErrorMessage">Error of the last failed attempt, when present.</param>
/// <param name="ProcessedAt">When processing completed.</param>
/// <param name="CreatedAt">When the event was accepted into the inbox.</param>
/// <param name="TenantId">Related tenant, when known.</param>
/// <param name="SubscriptionId">Related subscription, when known.</param>
public sealed record BillingWebhookEventListItemDto(
    Guid Id,
    string Provider,
    string ExternalEventId,
    string EventType,
    bool IsProcessed,
    bool IsFailed,
    int ProcessingAttempts,
    string? ErrorMessage,
    DateTime? ProcessedAt,
    DateTime CreatedAt,
    Guid? TenantId,
    Guid? SubscriptionId);

/// <summary>Query (issue #396): page through the durable billing webhook inbox events.</summary>
/// <param name="Status">Optional status filter: processed, failed or pending.</param>
/// <param name="Provider">Optional provider filter (e.g. stripe).</param>
/// <param name="EventType">Optional provider event type filter (e.g. invoice.payment_succeeded).</param>
/// <param name="FromUtc">Optional inclusive lower bound on CreatedAt (ISO-8601).</param>
/// <param name="ToUtc">Optional inclusive upper bound on CreatedAt (ISO-8601).</param>
/// <param name="Skip">Items to skip.</param>
/// <param name="Take">Items to take (1-100).</param>
public sealed record ListBillingEventsQuery(
    string? Status = null,
    string? Provider = null,
    string? EventType = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Skip = 0,
    int Take = 20) : IQuery<PagedResult<BillingWebhookEventListItemDto>>;

/// <summary>Validator for <see cref="ListBillingEventsQuery" />.</summary>
public sealed class ListBillingEventsQueryValidator : AbstractValidator<ListBillingEventsQuery>
{
    /// <summary>Initializes the validator.</summary>
    public ListBillingEventsQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => status is null || BillingEventStatusFilter.All.Contains(BillingEventStatusFilter.Normalize(status)))
            .WithMessage($"Status must be one of: {string.Join(", ", BillingEventStatusFilter.All)}.");
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

/// <summary>Handler for <see cref="ListBillingEventsQuery" /> over the existing webhook inbox store.</summary>
public sealed class ListBillingEventsQueryHandler(
    IBillingWebhookRepository webhookRepository) : IQueryHandler<ListBillingEventsQuery, PagedResult<BillingWebhookEventListItemDto>>
{
    /// <inheritdoc />
    public async Task<PagedResult<BillingWebhookEventListItemDto>> Handle(
        ListBillingEventsQuery request,
        CancellationToken cancellationToken)
    {
        var criteria = new BillingWebhookEventSearchCriteria(
            Status: BillingEventStatusFilter.Normalize(request.Status),
            Provider: string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider.Trim().ToLowerInvariant(),
            EventType: string.IsNullOrWhiteSpace(request.EventType) ? null : request.EventType.Trim(),
            FromUtc: request.FromUtc?.UtcDateTime,
            ToUtc: request.ToUtc?.UtcDateTime);

        var (matched, totalCount) = await webhookRepository
            .SearchEventsAsync(criteria, request.Skip, request.Take, cancellationToken)
            .ConfigureAwait(false);

        var items = matched
            .Select(webhookEvent => new BillingWebhookEventListItemDto(
                webhookEvent.Id,
                webhookEvent.Provider,
                webhookEvent.ExternalEventId,
                webhookEvent.EventType,
                webhookEvent.IsProcessed,
                webhookEvent.IsFailed,
                webhookEvent.ProcessingAttempts,
                webhookEvent.ErrorMessage,
                webhookEvent.ProcessedAt,
                webhookEvent.CreatedAt,
                webhookEvent.TenantId,
                webhookEvent.SubscriptionId))
            .ToArray();

        return new PagedResult<BillingWebhookEventListItemDto>(items, totalCount, request.Skip, request.Take);
    }
}
