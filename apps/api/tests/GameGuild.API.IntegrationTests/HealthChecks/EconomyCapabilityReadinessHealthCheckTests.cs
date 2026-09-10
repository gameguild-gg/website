using FluentAssertions;
using GameGuild.API.HealthChecks;
using GameGuild.API.Setup;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameGuild.API.IntegrationTests.HealthChecks;

public sealed class EconomyCapabilityReadinessHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenAllCapabilitiesAreDisabled_ReturnsHealthy()
    {
        var healthCheck = new EconomyCapabilityReadinessHealthCheck(
            new StubReadiness(EconomyCapabilityReadinessState.Disabled));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey(nameof(EconomyValueMovementCapability.PayoutExecution))
            .WhoseValue.Should().Be(nameof(EconomyCapabilityReadinessState.Disabled));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAnEnabledProviderCapabilityIsNotReady_ReturnsDegraded()
    {
        var healthCheck = new EconomyCapabilityReadinessHealthCheck(
            new StubReadiness(EconomyCapabilityReadinessState.ProviderNotReady));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("An enabled Economy provider-backed capability is not ready.");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConfigurationIsInvalid_ReturnsDegraded()
    {
        var healthCheck = new EconomyCapabilityReadinessHealthCheck(
            new StubReadiness(EconomyCapabilityReadinessState.InvalidConfiguration));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Economy capability configuration is invalid.");
    }

    private sealed class StubReadiness(EconomyCapabilityReadinessState state)
        : IEconomyProviderCapabilityReadiness
    {
        public EconomyCapabilityReadinessResult Assess(EconomyValueMovementCapability capability) =>
            new(capability, state, []);

        public void EnsureReady(EconomyValueMovementCapability capability) =>
            throw new NotSupportedException();
    }
}
