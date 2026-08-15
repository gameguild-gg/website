using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.SignalR;

namespace GameGuild.API.UnitTests.Core;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void SetupSignalR_WhenOptionsAreConfigured_BindsAndRegistersHubOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalR:EnableDetailedErrors"] = "true",
                ["SignalR:KeepAliveInterval"] = "00:00:09",
                ["SignalR:ClientTimeoutInterval"] = "00:00:45",
                ["SignalR:MaximumReceiveMessageSize"] = "4096"
            })
            .Build();
        var services = new ServiceCollection();

        var result = services.SetupSignalR(configuration, options: null);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HubOptions>>().Value;

        result.Should().BeSameAs(services);
        options.EnableDetailedErrors.Should().BeTrue();
        options.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(9));
        options.ClientTimeoutInterval.Should().Be(TimeSpan.FromSeconds(45));
        options.MaximumReceiveMessageSize.Should().Be(4096);
    }

    [Fact]
    public void SetupSignalR_WhenOptionsAreSupplied_UsesTheSuppliedValues()
    {
        var services = new ServiceCollection();
        var supplied = new SignalROptions
        {
            EnableDetailedErrors = false,
            KeepAliveInterval = TimeSpan.FromSeconds(11),
            ClientTimeoutInterval = TimeSpan.FromSeconds(50),
            MaximumReceiveMessageSize = null
        };

        services.SetupSignalR(new ConfigurationBuilder().Build(), supplied);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HubOptions>>().Value;

        options.EnableDetailedErrors.Should().BeFalse();
        options.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(11));
        options.ClientTimeoutInterval.Should().Be(TimeSpan.FromSeconds(50));
        options.MaximumReceiveMessageSize.Should().BeNull();
    }

    [Fact]
    public void SetupGraphQL_WhenOptionsAreConfigured_BindsAndValidatesConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GraphQL:EnableGraphQL"] = "true",
                ["GraphQL:Endpoint"] = "/v1/graphql"
            })
            .Build();
        var services = new ServiceCollection();

        var result = services.SetupGraphQL(configuration, options: null);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void SetupGraphQL_WhenOptionsAreSupplied_ValidatesAndReturnsServices()
    {
        var services = new ServiceCollection();
        var options = new GraphQLOptions { Endpoint = "/graphql" };

        var result = services.SetupGraphQL(new ConfigurationBuilder().Build(), options);

        result.Should().BeSameAs(services);
    }
}
