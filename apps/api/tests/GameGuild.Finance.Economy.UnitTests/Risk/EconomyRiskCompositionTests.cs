using FluentAssertions;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EconomyRiskCompositionTests
{
    [Fact]
    public void ValueMovingDecisionsAreDisabledByDefault()
    {
        var services = new ServiceCollection();
        services.AddEconomyRiskComposition(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IEconomyValueMovementDecisionGate>();

        gate.IsEnabled.Should().BeFalse();
        FluentActions.Invoking(gate.EnsureEnabled)
            .Should().Throw<EconomyValueMovementDisabledException>();
    }

    [Fact]
    public void ValueMovingDecisionsRequireExplicitConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            [EconomyRiskCompositionOptions.SectionName + ":ValueMovingDecisionsEnabled"] = "true",
            [EconomyRiskCompositionOptions.SectionName + ":EnabledCapabilities:0"] = "ConfirmHardCoinFunding",
            [EconomyRiskCompositionOptions.SectionName + ":AllowedJurisdictions:0"] = "BR"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddEconomyRiskComposition(configuration);

        using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IEconomyValueMovementDecisionGate>();

        gate.IsEnabled.Should().BeTrue();
        gate.IsCapabilityEnabled(EconomyValueMovementCapability.ConfirmHardCoinFunding).Should().BeTrue();
        gate.IsCapabilityEnabled(EconomyValueMovementCapability.PayoutExecution).Should().BeFalse();
        FluentActions.Invoking(gate.EnsureEnabled).Should().NotThrow();
        FluentActions.Invoking(() => gate.EnsureEnabled(EconomyValueMovementCapability.ConfirmHardCoinFunding))
            .Should().NotThrow();
        FluentActions.Invoking(() => gate.EnsureEnabled(EconomyValueMovementCapability.PayoutExecution))
            .Should().Throw<EconomyValueMovementDisabledException>();
    }

    [Fact]
    public void ValueMovingDecisionsRemainDisabledWhenTheJurisdictionAllowlistIsEmpty()
    {
        var values = new Dictionary<string, string?>
        {
            [EconomyRiskCompositionOptions.SectionName + ":ValueMovingDecisionsEnabled"] = "true",
            [EconomyRiskCompositionOptions.SectionName + ":EnabledCapabilities:0"] = "ConfirmHardCoinFunding"
        };
        var services = new ServiceCollection();
        services.AddEconomyRiskComposition(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IEconomyValueMovementDecisionGate>();

        gate.IsEnabled.Should().BeFalse();
        gate.IsCapabilityEnabled(EconomyValueMovementCapability.ConfirmHardCoinFunding).Should().BeFalse();
        FluentActions.Invoking(() => gate.EnsureEnabled(EconomyValueMovementCapability.ConfirmHardCoinFunding))
            .Should().Throw<EconomyValueMovementDisabledException>()
            .WithMessage("*jurisdiction allowlist is empty*");
    }

    [Fact]
    public void CompositionRejectsMissingDependencies()
    {
        FluentActions.Invoking(() => EconomyRiskCompositionExtensions.AddEconomyRiskComposition(
                null!, new ConfigurationBuilder().Build()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ServiceCollection().AddEconomyRiskComposition(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValueMovingDecisionsRequireNamedCapabilities()
    {
        var values = new Dictionary<string, string?>
        {
            [EconomyRiskCompositionOptions.SectionName + ":ValueMovingDecisionsEnabled"] = "true"
        };
        var services = new ServiceCollection();
        services.AddEconomyRiskComposition(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        using var provider = services.BuildServiceProvider();
        FluentActions.Invoking(() => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomyRiskCompositionOptions>>().Value)
            .Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("confirm-hard-coin-funding")]
    public void ValueMovingCapabilitiesRejectUnknownNames(string capability)
    {
        var values = new Dictionary<string, string?>
        {
            [EconomyRiskCompositionOptions.SectionName + ":EnabledCapabilities:0"] = capability
        };
        var services = new ServiceCollection();
        services.AddEconomyRiskComposition(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        using var provider = services.BuildServiceProvider();
        FluentActions.Invoking(() => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomyRiskCompositionOptions>>().Value)
            .Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public void InvalidCapabilityConfigurationDoesNotRegisterAStartupValidator()
    {
        var values = new Dictionary<string, string?>
        {
            [EconomyRiskCompositionOptions.SectionName + ":ValueMovingDecisionsEnabled"] = "true"
        };
        var services = new ServiceCollection();

        services.AddEconomyRiskComposition(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(Microsoft.Extensions.Options.IStartupValidator));
    }

    [Fact]
    public void JurisdictionAllowlistRequiresAtLeastOneNonWhitespaceEntry()
    {
        EconomyValueMovementCapabilities.HasAllowedJurisdiction(null).Should().BeFalse();
        EconomyValueMovementCapabilities.HasAllowedJurisdiction([]).Should().BeFalse();
        EconomyValueMovementCapabilities.HasAllowedJurisdiction(["", "   "]).Should().BeFalse();
        EconomyValueMovementCapabilities.HasAllowedJurisdiction(["BR"]).Should().BeTrue();
    }
}
