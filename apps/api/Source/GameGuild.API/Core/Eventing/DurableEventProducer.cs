using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;

namespace GameGuild.API.Eventing;

internal sealed class DurableEventProducer(ApplicationDbContext context) : IDurableEventProducer
{
    public async Task RecordAsync(
        IDurableIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        DurableIntegrationEventValidator.Validate(integrationEvent);
        if (await context.Set<OutboxMessage>()
                .AnyAsync(message => message.EventId == integrationEvent.EventId, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        context.Set<OutboxMessage>().Add(new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            TenantId = integrationEvent.TenantId,
            ActorId = integrationEvent.ActorId,
            EventName = integrationEvent.EventName,
            EventType = DurableIntegrationEventValidator.GetStableTypeName(integrationEvent.GetType()),
            SourceModule = integrationEvent.SourceModule,
            AggregateType = integrationEvent.AggregateType,
            AggregateId = integrationEvent.AggregateId,
            CorrelationId = integrationEvent.CorrelationId,
            CausationId = integrationEvent.CausationId,
            OccurredAtUtc = integrationEvent.OccurredAt,
            SchemaVersion = integrationEvent.SchemaVersion,
            Payload = DurableEventSerializer.Serialize(integrationEvent),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
