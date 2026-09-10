using FluentAssertions;
using GameGuild.API.Setup;
using GameGuild.Finance.Economy.Bounties;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Treasury;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.UnitTests.Core;

public sealed class EconomyModuleCompositionTests
{
    [Fact]
    public void ProductComposition_RegistersEveryEconomyModuleWithoutEnablingValueMovement()
    {
        var builder = WebApplication.CreateBuilder();

        ApiProductComposition.Instance.ConfigureServices(builder);

        ApiProductComposition.Instance.EnabledModules.Should().Contain([
            "Finance.Economy",
            "Finance.Economy.AdRewards",
            "Finance.Economy.Bounties",
            "Finance.Economy.Marketplace",
            "Finance.Economy.Payouts",
            "Finance.Economy.Treasury"
        ]);
        ApiProductComposition.Instance.DisabledModules.Should().NotContain([
            "Finance.Economy.AdRewards",
            "Finance.Economy.Bounties",
            "Finance.Economy.Marketplace",
            "Finance.Economy.Treasury"
        ]);
        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IBountyEscrowStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlBountyEscrowStore));
        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IPayoutRequestStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlPayoutRequestStore));
        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAdminWithdrawalStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlAdminWithdrawalStore));
        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurableBountyEscrowPostWorkflow) &&
            descriptor.ImplementationType == typeof(PostgreSqlDurableBountyEscrowPostWorkflow));
        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurableAdminWithdrawalWorkflow) &&
            descriptor.ImplementationType == typeof(PostgreSqlDurableAdminWithdrawalWorkflow));

        using var services = builder.Services.BuildServiceProvider();
        services.GetRequiredService<IEconomyValueMovementDecisionGate>().IsEnabled.Should().BeFalse();
    }
}
