using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

public sealed class QuotaLedgerEventHandler(IApplicationDbContext context) :
    IIntegrationEventHandler<UserCreatedEvent>,
    IIntegrationEventHandler<UserDeletedEvent>,
    IIntegrationEventHandler<PropertyCreatedEvent>,
    IIntegrationEventHandler<PropertyDeletedEvent>,
    IIntegrationEventHandler<AssetReferenceCreatedEvent>,
    IIntegrationEventHandler<AssetReferenceRemovedEvent>
{
    public Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default) =>
        HandleAsync((IDurableIntegrationEvent)@event, cancellationToken);

    public Task HandleAsync(UserDeletedEvent @event, CancellationToken cancellationToken = default) =>
        HandleAsync((IDurableIntegrationEvent)@event, cancellationToken);

    public Task HandleAsync(PropertyCreatedEvent @event, CancellationToken cancellationToken = default) =>
        HandleAsync((IDurableIntegrationEvent)@event, cancellationToken);

    public Task HandleAsync(PropertyDeletedEvent @event, CancellationToken cancellationToken = default) =>
        HandleAsync((IDurableIntegrationEvent)@event, cancellationToken);

    public Task HandleAsync(AssetReferenceCreatedEvent @event, CancellationToken cancellationToken = default) =>
        HandleAsync((IDurableIntegrationEvent)@event, cancellationToken);

    public Task HandleAsync(AssetReferenceRemovedEvent @event, CancellationToken cancellationToken = default) =>
        HandleAsync((IDurableIntegrationEvent)@event, cancellationToken);

    public async Task HandleAsync(
        IDurableIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (await context.Set<QuotaLedgerEntry>()
                .AnyAsync(entry => entry.EventId == @event.EventId, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var (resourceType, delta, reservationFinalization) = @event switch
        {
            UserCreatedEvent => (ResourceUsageType.Users, 1L, true),
            UserDeletedEvent => (ResourceUsageType.Users, -1L, false),
            PropertyCreatedEvent => (ResourceUsageType.Properties, 1L, true),
            PropertyDeletedEvent => (ResourceUsageType.Properties, -1L, false),
            AssetReferenceCreatedEvent => (ResourceUsageType.Assets, 1L, true),
            AssetReferenceRemovedEvent => (ResourceUsageType.Assets, -1L, false),
            _ => throw new ArgumentOutOfRangeException(nameof(@event), @event.EventName, "Unsupported quota lifecycle event.")
        };

        context.Set<QuotaLedgerEntry>().Add(new QuotaLedgerEntry
        {
            EventId = @event.EventId,
            TenantId = @event.TenantId,
            ResourceType = resourceType,
            Delta = delta,
            OperationCode = @event.EventName,
            CorrelationId = @event.CorrelationId,
            AggregateType = @event.AggregateType,
            AggregateId = @event.AggregateId,
            TimestampUtc = @event.OccurredAt,
            IsReservationFinalization = reservationFinalization
        });

        if (!reservationFinalization)
        {
            var quota = await context.Set<ResourceQuota>()
                .SingleOrDefaultAsync(
                    current => current.TenantId == @event.TenantId && current.Type == resourceType,
                    cancellationToken)
                .ConfigureAwait(false);
            quota?.RemoveUsage(1);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
