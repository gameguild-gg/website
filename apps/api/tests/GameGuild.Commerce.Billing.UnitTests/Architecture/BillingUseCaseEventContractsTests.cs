using System.Reflection;
using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Architecture;

/// <summary>
///     Issue #396: the billing module must declare named integration events for its core
///     webhook commands, following the UseCaseEventContract registry pattern.
/// </summary>
public sealed class BillingUseCaseEventContractsTests
{
    private static readonly Assembly BillingAssembly = typeof(BillingWebhookProcessedV1).Assembly;

    private static IReadOnlyList<UseCaseEventContractAttribute> Contracts =>
        BillingAssembly.GetCustomAttributes<UseCaseEventContractAttribute>().ToArray();

    [Fact]
    public void ProviderWebhookCommands_DeclareNamedBillingEvents()
    {
        var providerCommands = new[]
        {
            typeof(ProcessStripeWebhookCommand),
            typeof(ProcessPayPalWebhookCommand),
            typeof(ProcessApplePayWebhookCommand),
            typeof(ProcessGooglePayWebhookCommand)
        };

        foreach (var command in providerCommands)
        {
            var contract = Contracts.SingleOrDefault(attribute => attribute.CommandType == command);
            contract.Should().NotBeNull(
                "the provider webhook command '{0}' must declare a use-case event contract", command.Name);
            contract!.ConditionalEventTypes.Should().Contain(typeof(BillingWebhookProcessedV1),
                "'{0}' must declare the named webhook-processed event", command.Name);
            contract.ConditionalEventTypes.Should().Contain(typeof(BillingWebhookFailedV1),
                "'{0}' must declare the named webhook-failed event", command.Name);
        }
    }

    [Fact]
    public void BillingCommands_HaveExactlyOneContractEach()
    {
        var commandTypes = new[]
        {
            typeof(ProcessStripeWebhookCommand),
            typeof(ProcessPayPalWebhookCommand),
            typeof(ProcessApplePayWebhookCommand),
            typeof(ProcessGooglePayWebhookCommand),
            typeof(ProcessBillingWebhookCommand),
            typeof(RetryInvoicePaymentCommand),
            typeof(RetryWebhookEventCommand)
        };

        foreach (var command in commandTypes)
        {
            Contracts.Count(attribute => attribute.CommandType == command).Should().Be(1,
                "command '{0}' must have exactly one use-case event contract (hand-written and fallback must not overlap)",
                command.Name);
        }
    }

    [Fact]
    public void AllBillingContracts_DeclareObservableOutcome()
    {
        Contracts.Should().NotBeEmpty();
        Contracts.Select(contract => contract.OperationCode).Should().OnlyHaveUniqueItems();

        Contracts.Where(contract => string.IsNullOrWhiteSpace(contract.OperationCode))
            .Should().BeEmpty();

        Contracts.Where(contract => contract.ExpectedEventTypes.Length == 0
                                    && contract.ConditionalEventTypes.Length == 0
                                    && string.IsNullOrWhiteSpace(contract.NoDomainEventReason)
                                    && string.IsNullOrWhiteSpace(contract.UnavailableReason))
            .Select(contract => contract.CommandType.FullName)
            .Should().BeEmpty();
    }

    [Fact]
    public void NamedBillingEvents_AreVersionedDurableIntegrationEvents()
    {
        var namedEvents = Contracts
            .SelectMany(contract => contract.ExpectedEventTypes.Concat(contract.ConditionalEventTypes))
            .Distinct()
            .ToArray();

        namedEvents.Should().NotBeEmpty("the billing module must declare named integration events (issue #396)");

        foreach (var eventType in namedEvents)
        {
            eventType.Should().BeAssignableTo<IDurableIntegrationEvent>(
                "named billing event '{0}' must be a durable integration event", eventType.Name);
        }
    }

    [Fact]
    public void NamedBillingEventRecords_ExposeStableVersionedNames()
    {
        DurableIntegrationEventBase[] instances =
        [
            new BillingWebhookProcessedV1(Guid.NewGuid(), "stripe", "evt_1", "invoice.payment_succeeded", 1)
                { AggregateType = "BillingWebhookEvent", AggregateId = "agg" },
            new BillingWebhookFailedV1(Guid.NewGuid(), "stripe", "evt_1", "invoice.payment_succeeded", 1, "boom")
                { AggregateType = "BillingWebhookEvent", AggregateId = "agg" },
            new BillingInvoicePaidV1(Guid.NewGuid(), "stripe", "evt_1", 10m, "USD")
                { AggregateType = "Subscription", AggregateId = "agg" },
            new BillingSubscriptionRenewedV1(Guid.NewGuid(), "stripe", "evt_1")
                { AggregateType = "Subscription", AggregateId = "agg" },
            new BillingSubscriptionCancelledV1(Guid.NewGuid(), "stripe", "evt_1")
                { AggregateType = "Subscription", AggregateId = "agg" }
        ];

        foreach (var instance in instances)
        {
            instance.EventName.Should().EndWith(".v1",
                "named billing event '{0}' must expose a literal versioned event name", instance.GetType().Name);
            instance.SourceModule.Should().Be("Commerce.Billing");
            instance.SchemaVersion.Should().Be(1);
        }
    }
}
