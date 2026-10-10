using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.Commerce.Billing;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Core.Integration;

/// <summary>
///     Connects the Commerce.Billing module's <see cref="IBillingOutboxEventReader" /> port to the
///     platform outbox read model. Only events published by the Commerce.Billing module are
///     returned; delivery status is derived from the outbox row lifecycle timestamps.
/// </summary>
public sealed class BillingOutboxEventReader(ApplicationDbContext context) : IBillingOutboxEventReader
{
    private const string BillingSourceModule = "Commerce.Billing";

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillingOutboxEventDto>> ListAsync(
        int skip,
        int take,
        string? eventName,
        BillingOutboxEventStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default)
    {
        // Materialize rows first: the status derivation happens in memory because it
        // combines three lifecycle columns that cannot be projected into a method call.
        var messages = await BuildQuery(eventName, status, fromUtc, toUtc)
            .OrderByDescending(message => message.OccurredAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return messages
            .Select(message => ToDto(message))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        string? eventName,
        BillingOutboxEventStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default) =>
        await BuildQuery(eventName, status, fromUtc, toUtc)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<BillingOutboxEventDto?> GetByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var message = await context.Set<OutboxMessage>()
            .Where(message => message.EventId == eventId && message.SourceModule == BillingSourceModule)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return message is null ? null : ToDto(message);
    }

    private IQueryable<OutboxMessage> BuildQuery(
        string? eventName,
        BillingOutboxEventStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        var query = context.Set<OutboxMessage>()
            .Where(message => message.SourceModule == BillingSourceModule);

        if (!string.IsNullOrWhiteSpace(eventName))
        {
            var name = eventName;
            query = query.Where(message => message.EventName == name);
        }

        if (status.HasValue)
        {
            query = status.Value switch
            {
                BillingOutboxEventStatus.Completed => query.Where(message => message.CompletedAtUtc != null),
                BillingOutboxEventStatus.DeadLettered => query.Where(message => message.DeadLetteredAtUtc != null),
                BillingOutboxEventStatus.Pending => query.Where(message =>
                    message.CompletedAtUtc == null && message.DeadLetteredAtUtc == null),
                _ => query
            };
        }

        if (fromUtc.HasValue)
        {
            var from = fromUtc.Value;
            query = query.Where(message => message.OccurredAtUtc >= from);
        }

        if (toUtc.HasValue)
        {
            var to = toUtc.Value;
            query = query.Where(message => message.OccurredAtUtc <= to);
        }

        return query;
    }

    private static BillingOutboxEventDto ToDto(OutboxMessage message)
    {
        // Delivery status is derived from the outbox lifecycle: dead-letter dominates,
        // then completion; everything else is still in flight.
        var status = message.DeadLetteredAtUtc.HasValue
            ? BillingOutboxEventStatus.DeadLettered
            : message.CompletedAtUtc.HasValue
                ? BillingOutboxEventStatus.Completed
                : BillingOutboxEventStatus.Pending;

        return new BillingOutboxEventDto(
            message.EventId,
            message.EventName,
            message.EventType,
            message.AggregateType,
            message.AggregateId,
            message.TenantId,
            message.CorrelationId,
            status,
            message.OccurredAtUtc,
            message.SchemaVersion);
    }
}
