using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>Query (issue #396): page through durable billing integration events in the platform outbox.</summary>
/// <param name="EventName">Optional stable event name filter, e.g. commerce.billing.invoice-paid.v1.</param>
/// <param name="Status">Optional delivery status filter: pending, completed or deadlettered.</param>
/// <param name="FromUtc">Optional inclusive lower bound on OccurredAt (ISO-8601).</param>
/// <param name="ToUtc">Optional inclusive upper bound on OccurredAt (ISO-8601).</param>
/// <param name="Skip">Items to skip.</param>
/// <param name="Take">Items to take (1-100).</param>
public sealed record ListBillingOutboxEventsQuery(
    string? EventName = null,
    string? Status = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Skip = 0,
    int Take = 20) : IQuery<PagedResult<BillingOutboxEventDto>>;

/// <summary>Validator for <see cref="ListBillingOutboxEventsQuery" />.</summary>
public sealed class ListBillingOutboxEventsQueryValidator : AbstractValidator<ListBillingOutboxEventsQuery>
{
    /// <summary>Initializes the validator.</summary>
    public ListBillingOutboxEventsQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

/// <summary>
///     Handler for <see cref="ListBillingOutboxEventsQuery" /> over the platform outbox read
///     model, scoped to events published by the Commerce.Billing module.
/// </summary>
public sealed class ListBillingOutboxEventsQueryHandler(
    IBillingOutboxEventReader outboxReader) : IQueryHandler<ListBillingOutboxEventsQuery, PagedResult<BillingOutboxEventDto>>
{
    /// <inheritdoc />
    public async Task<PagedResult<BillingOutboxEventDto>> Handle(
        ListBillingOutboxEventsQuery request,
        CancellationToken cancellationToken)
    {
        var eventName = string.IsNullOrWhiteSpace(request.EventName) ? null : request.EventName.Trim();
        var status = ParseStatus(request.Status);
        var fromUtc = request.FromUtc?.ToUniversalTime();
        var toUtc = request.ToUtc?.ToUniversalTime();

        var totalCount = await outboxReader
            .CountAsync(eventName, status, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        var items = await outboxReader
            .ListAsync(request.Skip, request.Take, eventName, status, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<BillingOutboxEventDto>(items, totalCount, request.Skip, request.Take);
    }

    private static BillingOutboxEventStatus? ParseStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToLowerInvariant() switch
            {
                "pending" => BillingOutboxEventStatus.Pending,
                "completed" => BillingOutboxEventStatus.Completed,
                "deadlettered" or "dead-lettered" => BillingOutboxEventStatus.DeadLettered,
                _ => null
            };
}
