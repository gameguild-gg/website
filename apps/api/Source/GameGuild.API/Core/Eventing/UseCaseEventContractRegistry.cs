using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;

namespace GameGuild.API.Eventing;

internal sealed class UseCaseEventContractRegistry : IUseCaseEventContractRegistry
{
    private readonly ConcurrentDictionary<Type, UseCaseEventContract> _contracts = new();

    public UseCaseEventContractRegistry()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(assembly => assembly.GetName().Name?.StartsWith("GameGuild", StringComparison.Ordinal) == true))
            LoadAssemblyContracts(assembly);
    }

    public UseCaseEventContract GetRequired(Type commandType)
    {
        LoadAssemblyContracts(commandType.Assembly);
        return _contracts.TryGetValue(commandType, out var contract)
            ? contract
            : throw new InvalidOperationException(
                $"Mutating command '{commandType.FullName}' is missing a use-case event contract.");
    }

    public IReadOnlyCollection<UseCaseEventContract> GetAll()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(assembly => assembly.GetName().Name?.StartsWith("GameGuild", StringComparison.Ordinal) == true))
            LoadAssemblyContracts(assembly);
        return _contracts.Values.ToArray();
    }

    private void LoadAssemblyContracts(System.Reflection.Assembly assembly)
    {
        var attributes = assembly.GetCustomAttributes(typeof(UseCaseEventContractAttribute), false)
            .Cast<UseCaseEventContractAttribute>()
            .ToArray();
        foreach (var group in attributes.GroupBy(attribute => attribute.CommandType))
        {
            if (group.Count() > 1)
                throw new InvalidOperationException($"Command '{group.Key.FullName}' has multiple use-case event contracts.");
            _contracts.TryAdd(group.Key, ToContract(group.Single()));
        }
    }

    private static UseCaseEventContract ToContract(UseCaseEventContractAttribute attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute.OperationCode))
            throw new InvalidOperationException($"Command '{attribute.CommandType.FullName}' has an empty operation code.");
        if (attribute.ExpectedEventTypes.Length == 0
            && attribute.ConditionalEventTypes.Length == 0
            && string.IsNullOrWhiteSpace(attribute.NoDomainEventReason))
            throw new InvalidOperationException(
                $"Command '{attribute.CommandType.FullName}' must declare an event or a no-domain-event reason.");

        var declaredEvents = attribute.ExpectedEventTypes.Concat(attribute.ConditionalEventTypes);
        if (declaredEvents.Any(type => !typeof(IDurableIntegrationEvent).IsAssignableFrom(type)))
            throw new InvalidOperationException(
                $"Command '{attribute.CommandType.FullName}' declares a non-durable event type.");

        return new UseCaseEventContract(
            attribute.CommandType,
            attribute.OperationCode,
            attribute.ExpectedEventTypes,
            attribute.ConditionalEventTypes,
            attribute.QuotaImpact,
            attribute.NoDomainEventReason,
            attribute.UnavailableReason);
    }
}

internal sealed class UseCaseEventVerifier(ApplicationDbContext context) : IUseCaseEventVerifier
{
    public async Task VerifyAsync(
        UseCaseEventContract contract,
        UseCaseOperationContext operationContext,
        CancellationToken cancellationToken = default)
    {
        if (!operationContext.BusinessMutationObserved)
            return;

        var storedEventTypes = await context.Set<OutboxMessage>()
            .Where(message => message.CorrelationId == operationContext.CorrelationId)
            .Select(message => message.EventType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var genericType = DurableIntegrationEventValidator.GetStableTypeName(typeof(UseCaseOperationOccurredV1));
        if (!storedEventTypes.Contains(genericType, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Command '{contract.CommandType.FullName}' mutated state without its durable operation event.");

        var missingEvents = contract.ExpectedEventTypes
            .Where(expected => !storedEventTypes.Contains(
                DurableIntegrationEventValidator.GetStableTypeName(expected),
                StringComparer.Ordinal))
            .Select(expected => expected.FullName)
            .ToArray();
        if (missingEvents.Length > 0)
            throw new InvalidOperationException(
                $"Command '{contract.CommandType.FullName}' is missing declared durable events: {string.Join(", ", missingEvents)}.");
    }
}
