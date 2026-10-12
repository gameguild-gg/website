using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests;

/// <summary>
///     DI wiring of the module options. The feature gate, discovery controllers, bulk
///     processor and command handlers all resolve <see cref="IOptions{TOptions}"/>, so
///     <c>AddScimProvisioningModule</c> must bind <see cref="ScimProvisioningOptions"/>
///     through the options pipeline; registering a pre-bound singleton instance leaves
///     <c>IOptions&lt;T&gt;.Value</c> at its defaults (<c>Enabled=false</c>) and 404s the
///     whole <c>/scim</c> surface regardless of configuration (PR #773 CI regression).
/// </summary>
public sealed class ProvisioningModuleOptionsWiringTests
{
    [Fact]
    public void AddScimProvisioningModule_BindsEnabledTrue_ThroughIOptionsPipeline()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Scim:Enabled"] = "true",
            ["Scim:DefaultPageSize"] = "25",
            ["Scim:MaxPageSize"] = "50"
        });
        var options = provider.GetRequiredService<IOptions<ScimProvisioningOptions>>();

        options.Value.Enabled.Should().BeTrue(
            "Scim:Enabled=true must reach IOptions<ScimProvisioningOptions> or the feature gate 404s every /scim route");
        options.Value.DefaultPageSize.Should().Be(25);
        options.Value.MaxPageSize.Should().Be(50);
    }

    [Fact]
    public void AddScimProvisioningModule_EnabledDefaultsToFalse_WhenSectionMissing()
    {
        using var provider = BuildProvider([]);
        var options = provider.GetRequiredService<IOptions<ScimProvisioningOptions>>();

        options.Value.Enabled.Should().BeFalse(
            "without a Scim section the surface must stay hidden (fail closed)");
        options.Value.DefaultPageSize.Should().Be(100);
        options.Value.MaxPageSize.Should().Be(200);
        options.Value.MaxBulkOperations.Should().Be(100);
    }

    [Fact]
    public void AddScimProvisioningModule_EnabledFalse_FromExplicitConfiguration()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Scim:Enabled"] = "false"
        });
        provider.GetRequiredService<IOptions<ScimProvisioningOptions>>().Value.Enabled
            .Should().BeFalse();
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddScimProvisioningModule(configuration);
        return services.BuildServiceProvider();
    }
}
