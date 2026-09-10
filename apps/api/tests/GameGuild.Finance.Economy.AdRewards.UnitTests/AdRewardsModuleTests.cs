using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardsModuleTests
{
    [Fact]
    public void ModuleAndCompositionHookRegisterTheDurableFailClosedRuntime()
    {
        var module = new AdRewardsModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        module.Name.Should().Be("Finance.Economy.AdRewards");
        module.EnabledByDefault.Should().BeTrue();
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);
        services.AddAdRewardsComposition(configuration).Should().BeSameAs(services);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IDurableAdRewardSessionService) &&
                                                descriptor.ImplementationType == typeof(DurableAdRewardSessionService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IDurableAdRewardSessionReader) &&
                                                descriptor.ImplementationType == typeof(PostgreSqlDurableAdRewardSessionReader));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IDurableAdRewardCompletionService) &&
                                                descriptor.ImplementationType == typeof(DurableAdRewardCompletionService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IDurableAdRewardReportService) &&
                                                descriptor.ImplementationType == typeof(DurableAdRewardReportService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IDurableAdRewardReportReader) &&
                                                descriptor.ImplementationType == typeof(PostgreSqlDurableAdRewardReportReader));
    }
}
