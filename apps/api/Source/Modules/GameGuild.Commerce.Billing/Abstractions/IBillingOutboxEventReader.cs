namespace GameGuild.Commerce.Billing;

/// <summary>Delivery status of a durable billing event in the platform outbox.</summary>
public enum BillingOutboxEventStatus
{
    /// <summary>The event is still being dispatched to consumers.</summary>
    Pending = 0,

    /// <summary>The event completed delivery to all consumers.</summary>
    Completed = 1,

    /// <summary>At least one consumer dead-lettered the event.</summary>
    DeadLettered = 2
}

/// <summary>Read model over a durable billing integration event published through the platform outbox.</summary>
/// <param name="EventId">Durable event identifier.</param>
/// <param name="EventName">Stable event name, e.g. commerce.billing.invoice-paid.v1.</param>
/// <param name="EventType">Stable CLR type name of the event payload.</param>
/// <param name="AggregateType">Aggregate the event refers to.</param>
/// <param name="AggregateId">Aggregate identifier the event refers to.</param>
/// <param name="TenantId">Tenant scope of the event.</param>
/// <param name="CorrelationId">Correlation identifier of the emitting operation.</param>
/// <param name="Status">Delivery status derived from the outbox row.</param>
/// <param name="OccurredAtUtc">When the event occurred.</param>
/// <param name="SchemaVersion">Schema version of the event payload.</param>
public sealed record BillingOutboxEventDto(
    Guid EventId,
    string EventName,
    string EventType,
    string AggregateType,
    string AggregateId,
    Guid TenantId,
    Guid CorrelationId,
    BillingOutboxEventStatus Status,
    DateTimeOffset OccurredAtUtc,
    int SchemaVersion);

/// <summary>
///     Read-only projection over the platform outbox scoped to events published by the
///     Commerce.Billing module. Implemented by the host against the durable outbox read
///     model; the billing module owns the contract so it stays transport-agnostic.
/// </summary>
public interface IBillingOutboxEventReader
{
    /// <summary>Lists billing outbox events, newest first.</summary>
    Task<IReadOnlyList<BillingOutboxEventDto>> ListAsync(
        int skip,
        int take,
        string? eventName,
        BillingOutboxEventStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Counts billing outbox events matching the filters.</summary>
    Task<int> CountAsync(
        string? eventName,
        BillingOutboxEventStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a single billing outbox event by identifier.</summary>
    Task<BillingOutboxEventDto?> GetByIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
