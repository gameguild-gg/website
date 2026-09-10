using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutsModuleTests
{
    [Fact]
    public void ModuleAlwaysComposesDurableWorkflowsWhileValueAuthorizationRemainsFailClosed()
    {
        var module = new PayoutsModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        module.Name.Should().Be("Finance.Economy.Payouts");
        module.EnabledByDefault.Should().BeTrue();
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);
        services.AddPayoutsComposition(configuration).Should().BeSameAs(services);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IPayoutOperationStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlPayoutOperationStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IPayoutRequestStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlPayoutRequestStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurablePayoutReservationWorkflow) &&
            descriptor.ImplementationType == typeof(PostgreSqlDurablePayoutReservationWorkflow) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurablePayoutSettlementWorkflow) &&
            descriptor.ImplementationType == typeof(PostgreSqlDurablePayoutSettlementWorkflow) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IPayoutFencingTokenAllocator) &&
            descriptor.ImplementationType == typeof(PostgreSqlPayoutFencingTokenAllocator) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IPayoutAuthorizationEvidenceWriter) &&
            descriptor.ImplementationType == typeof(PostgreSqlPayoutAuthorizationEvidenceWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurablePayoutApplicationService) &&
            descriptor.ImplementationType == typeof(DurablePayoutApplicationService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IConnectPayoutProvider));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IStripeConnectWebhookNormalizer));
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(PayoutCoordinator));
        services.Should().NotContain(descriptor => descriptor.ImplementationType == typeof(InMemoryPayoutOperationStore));
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(DbContext));
    }

}
