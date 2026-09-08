using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace GameGuild.API.Eventing;

internal static class DurableEventSerializer
{
    private static readonly HashSet<string> MetadataProperties =
    [
        nameof(IIntegrationEvent.EventId),
        nameof(IIntegrationEvent.OccurredAt),
        nameof(IIntegrationEvent.SourceModule),
        nameof(IDurableIntegrationEvent.EventName),
        nameof(IDurableIntegrationEvent.SchemaVersion),
        nameof(IDurableIntegrationEvent.TenantId),
        nameof(IDurableIntegrationEvent.ActorId),
        nameof(IDurableIntegrationEvent.AggregateType),
        nameof(IDurableIntegrationEvent.AggregateId),
        nameof(IDurableIntegrationEvent.CorrelationId),
        nameof(IDurableIntegrationEvent.CausationId)
    ];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IDurableIntegrationEvent integrationEvent)
    {
        ValidateEventPayload(integrationEvent);
        return JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);
    }

    public static IDurableIntegrationEvent Deserialize(OutboxMessage message)
    {
        var eventType = Type.GetType(message.EventType, throwOnError: true)!;
        return (IDurableIntegrationEvent)(JsonSerializer.Deserialize(message.Payload, eventType, Options)
            ?? throw new InvalidOperationException($"Event '{message.EventId}' deserialized to null."));
    }

    private static void ValidateEventPayload(IDurableIntegrationEvent integrationEvent)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var property in integrationEvent.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (MetadataProperties.Contains(property.Name) || property.GetMethod is null)
            {
                continue;
            }

            ValidateClassifiedProperty(integrationEvent, property, property.Name, visited);
        }
    }

    private static void ValidateClassifiedProperty(
        object owner,
        PropertyInfo property,
        string path,
        ISet<object> visited)
    {
        if (property.GetCustomAttribute<NonPersonalEventDataAttribute>() is null)
        {
            throw new InvalidOperationException(
                $"Durable event payload property '{path}' is unclassified. " +
                $"Explicitly mark non-personal fields with {nameof(NonPersonalEventDataAttribute)}.");
        }

        ValidateNestedValue(property.GetValue(owner), path, visited);
    }

    private static void ValidateNestedValue(object? value, string path, ISet<object> visited)
    {
        if (value is null || IsScalar(value.GetType()))
        {
            return;
        }

        if (!value.GetType().IsValueType && !visited.Add(value))
        {
            return;
        }

        if (value is IEnumerable items)
        {
            var index = 0;
            foreach (var item in items)
            {
                ValidateNestedValue(item, $"{path}[{index++}]", visited);
            }

            return;
        }

        var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null)
            .ToList();
        if (properties.Count == 0)
        {
            throw new InvalidOperationException(
                $"Durable event payload property '{path}' has an unsupported unclassified type.");
        }

        foreach (var property in properties)
        {
            ValidateClassifiedProperty(value, property, $"{path}.{property.Name}", visited);
        }
    }

    private static bool IsScalar(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(Guid)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly);
}
