namespace GameGuild.API.Eventing;

internal static class DurableIntegrationEventValidator
{
    public static void Validate(IDurableIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.TenantId == Guid.Empty || integrationEvent.ActorId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Durable events require tenant and actor identifiers; use the stable platform/system sentinels for system activity.");
        }

        if (integrationEvent.EventId == Guid.Empty || integrationEvent.CorrelationId == Guid.Empty)
        {
            throw new InvalidOperationException("Durable events require stable event and correlation identifiers.");
        }

        if (integrationEvent.SchemaVersion < 1 || integrationEvent.OccurredAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("Durable events require a positive schema version and UTC timestamp.");
        }

        if (string.IsNullOrWhiteSpace(integrationEvent.EventName)
            || string.IsNullOrWhiteSpace(integrationEvent.SourceModule)
            || string.IsNullOrWhiteSpace(integrationEvent.AggregateType)
            || string.IsNullOrWhiteSpace(integrationEvent.AggregateId))
        {
            throw new InvalidOperationException("Durable events require event, source-module, and aggregate identities.");
        }
    }

    public static string GetStableTypeName(Type eventType) =>
        $"{eventType.FullName ?? throw new InvalidOperationException("Durable event type has no full name.")}, " +
        $"{eventType.Assembly.GetName().Name ?? throw new InvalidOperationException("Durable event assembly has no name.")}";
}
