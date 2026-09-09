using GameGuild.API.Setup;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameGuild.API.HealthChecks;

/// <summary>
/// Reports whether enabled Economy capabilities have their required external provider configuration.
/// It is a dependency signal only: optional value-moving capabilities must not make the API unavailable.
/// </summary>
internal sealed class EconomyCapabilityReadinessHealthCheck(
    IEconomyProviderCapabilityReadiness readiness) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = Enum.GetValues<EconomyValueMovementCapability>()
                .Select(readiness.Assess)
                .ToArray();
            var data = results.ToDictionary(
                result => result.Capability.ToString(),
                result => (object)result.State.ToString(),
                StringComparer.Ordinal);

            if (results.Any(result => result.State == EconomyCapabilityReadinessState.InvalidConfiguration))
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Economy capability configuration is invalid.",
                    data: data));
            }

            if (results.Any(result => result.State == EconomyCapabilityReadinessState.ProviderNotReady))
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "An enabled Economy provider-backed capability is not ready.",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Economy capability configuration is ready or intentionally disabled.",
                data));
        }
        catch
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Economy capability readiness check failed."));
        }
    }
}
