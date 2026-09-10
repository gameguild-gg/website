using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountiesModuleTests
{
    [Fact]
    public void ModuleComposesPersistentBountyServicesByDefault()
    {
        var module = new BountiesModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        module.Name.Should().Be("Finance.Economy.Bounties");
        module.EnabledByDefault.Should().BeTrue();
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);
        services.AddBountiesComposition(configuration).Should().BeSameAs(services);
        services.Should().SatisfyRespectively(
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IBountyEscrowStore) &&
                item.ImplementationType == typeof(PostgreSqlBountyEscrowStore) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IBountyTerminalEventStore) &&
                item.ImplementationType == typeof(PostgreSqlBountyTerminalEventStore) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IBountyPostableLotReader) &&
                item.ImplementationType == typeof(PostgreSqlBountyPostableLotReader) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IDurableBountyEscrowPostWorkflow) &&
                item.ImplementationType == typeof(PostgreSqlDurableBountyEscrowPostWorkflow) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IBountyTerminalClaimWriter) &&
                item.ImplementationType == typeof(PostgreSqlBountyTerminalClaimWriter) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IDurableBountyClaimWorkflow) &&
                item.ImplementationType == typeof(PostgreSqlDurableBountyClaimWorkflow) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IBountyTerminalReclaimWriter) &&
                item.ImplementationType == typeof(PostgreSqlBountyTerminalReclaimWriter) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(PostgreSqlBountyExpirationWorkflow) &&
                item.ImplementationType == typeof(PostgreSqlBountyExpirationWorkflow) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IDurableBountyExpirationWorkflow) &&
                item.ImplementationFactory != null &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IBountyExpirationTransition) &&
                item.ImplementationFactory != null &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IDurableBountyReclaimWorkflow) &&
                item.ImplementationType == typeof(PostgreSqlDurableBountyReclaimWorkflow) &&
                item.Lifetime == ServiceLifetime.Scoped),
            descriptor => descriptor.Should().Match<ServiceDescriptor>(item =>
                item.ServiceType == typeof(IDurableBountyApplicationService) &&
                item.ImplementationType == typeof(DurableBountyApplicationService) &&
                item.Lifetime == ServiceLifetime.Scoped));
        typeof(IBountyTerminalEventStore).GetMethod("Complete").Should().BeNull(
            "a terminal state transition must be coupled to its immutable ledger posting");
    }
}
