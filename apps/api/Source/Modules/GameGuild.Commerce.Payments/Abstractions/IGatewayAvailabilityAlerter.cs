namespace GameGuild.Commerce.Payments;

/// <summary>
///     Raises the all-gateways-down alert (issue #413) through the platform notification
///     pipeline. Implementations must be failure-tolerant: an alerting problem is logged,
///     never propagated into the payment path.
/// </summary>
public interface IGatewayAvailabilityAlerter
{
    /// <summary>
    ///     Alerts operators that no payment gateway could be attempted (typically every
    ///     circuit breaker open). Implementations throttle repeats so a sustained outage
    ///     does not flood the notification pipeline.
    /// </summary>
    Task RaiseAllGatewaysUnavailableAsync(
        IReadOnlyList<GatewayCircuitSnapshot> circuitStates,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     No-op <see cref="IGatewayAvailabilityAlerter" /> for tests and environments without
///     the notification pipeline; keeps alert wiring explicit and observable.
/// </summary>
public sealed class NullGatewayAvailabilityAlerter : IGatewayAvailabilityAlerter
{
    /// <inheritdoc />
    public Task RaiseAllGatewaysUnavailableAsync(
        IReadOnlyList<GatewayCircuitSnapshot> circuitStates,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
