using System.Reflection;
using System.Runtime.Loader;
using FluentAssertions;
using GameGuild.CQRS;

namespace GameGuild.API.UnitTests.Architecture;

public sealed class DurableEventArchitectureTests
{
    [Fact]
    public void ProductionCommands_HaveOneContractAndOneHandler()
    {
        var assemblies = LoadProductionAssemblies();
        var commandTypes = assemblies.SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false } && IsCommand(type))
            .Distinct()
            .ToArray();
        var contracts = assemblies
            .SelectMany(assembly => assembly.GetCustomAttributes<UseCaseEventContractAttribute>())
            .ToArray();

        commandTypes.Should().HaveCountGreaterThanOrEqualTo(386);
        contracts.GroupBy(contract => contract.CommandType)
            .Where(group => group.Count() != 1)
            .Select(group => $"{group.Key.FullName}: {group.Count()}")
            .Should().BeEmpty();
        var missingContracts = commandTypes.Except(contracts.Select(contract => contract.CommandType))
            .Select(type => $"{type.Assembly.GetName().Name}|{type.FullName}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(missingContracts.Length == 0,
            $"Commands missing use-case event contracts:{Environment.NewLine}{string.Join(Environment.NewLine, missingContracts)}");
        contracts.Select(contract => contract.CommandType)
            .Except(commandTypes)
            .Select(type => type.FullName)
            .Should().BeEmpty();

        var handlers = assemblies.SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(handler => handler.GetInterfaces()
                .Where(IsRequestHandler)
                .Select(contract => new { Command = contract.GetGenericArguments()[0], Handler = handler }))
            .Where(pair => commandTypes.Contains(pair.Command))
            .GroupBy(pair => pair.Command)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Handler).Distinct().ToArray());
        var handlerCardinalityErrors = commandTypes.Select(command => new
            {
                Command = command,
                Count = (handlers.TryGetValue(command, out var registered) ? registered.Length : 0)
                        + (contracts.Single(contract => contract.CommandType == command).UnavailableReason is null ? 0 : 1)
            })
            .Where(result => result.Count != 1)
            .Select(result => $"{result.Command.FullName}: {result.Count}")
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToArray();
        Assert.True(handlerCardinalityErrors.Length == 0,
            $"Commands with invalid handler cardinality:{Environment.NewLine}{string.Join(Environment.NewLine, handlerCardinalityErrors)}");
    }

    [Fact]
    public void EventContracts_HaveStableOperationCodesAndObservableOutcomes()
    {
        var contracts = LoadProductionAssemblies()
            .SelectMany(assembly => assembly.GetCustomAttributes<UseCaseEventContractAttribute>())
            .ToArray();

        contracts.Select(contract => contract.OperationCode)
            .Should().OnlyHaveUniqueItems();
        contracts.Where(contract => string.IsNullOrWhiteSpace(contract.OperationCode))
            .Should().BeEmpty();
        contracts.Where(contract => contract.ExpectedEventTypes.Length == 0
                                    && contract.ConditionalEventTypes.Length == 0
                                    && string.IsNullOrWhiteSpace(contract.NoDomainEventReason)
                                    && string.IsNullOrWhiteSpace(contract.UnavailableReason))
            .Select(contract => contract.CommandType.FullName)
            .Should().BeEmpty();
        contracts.SelectMany(contract => contract.ExpectedEventTypes.Concat(contract.ConditionalEventTypes))
            .Where(type => !typeof(IDurableIntegrationEvent).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .Should().BeEmpty();
    }

    [Fact]
    public void DurableConsumers_HaveStableInboxConsumerIdentities()
    {
        var consumers = LoadProductionAssemblies().SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                                   && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .Where(contract => typeof(IDurableIntegrationEvent)
                    .IsAssignableFrom(contract.GetGenericArguments()[0]))
                .Select(contract => new
                {
                    ConsumerName = type.FullName,
                    EventType = contract.GetGenericArguments()[0]
                }))
            .ToArray();

        consumers.Should().NotBeEmpty();
        consumers.Where(consumer => string.IsNullOrWhiteSpace(consumer.ConsumerName))
            .Should().BeEmpty();
        consumers.Select(consumer => $"{consumer.ConsumerName}|{consumer.EventType.FullName}")
            .Should().OnlyHaveUniqueItems();
    }

    private static IReadOnlyList<Assembly> LoadProductionAssemblies()
    {
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "GameGuild*.dll"))
        {
            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
            }
            catch (FileLoadException)
            {
            }
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("GameGuild", StringComparison.Ordinal) == true)
            .Where(assembly => assembly.GetName().Name?.EndsWith("Tests", StringComparison.Ordinal) != true)
            .ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static bool IsCommand(Type type) =>
        type.GetInterfaces().Any(contract =>
            contract == typeof(ICommand)
            || (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)));

    private static bool IsRequestHandler(Type contract) =>
        contract.IsGenericType
        && contract.Namespace == "GameGuild.CQRS"
        && (contract.Name.StartsWith("ICommandHandler", StringComparison.Ordinal)
            || contract.Name.StartsWith("IRequestHandler", StringComparison.Ordinal));
}
