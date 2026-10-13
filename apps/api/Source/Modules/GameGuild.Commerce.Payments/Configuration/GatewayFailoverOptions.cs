using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Gateway failover and circuit-breaker policy (issue #413), bound from
///     <c>Payments:GatewayFailover</c>. Governs the synchronous failover loop around
///     gateway charges: same-gateway retries, the total attempt budget, and the
///     per-gateway circuit breaker. The scheduled retry cadence for failed payments
///     remains governed by <see cref="PaymentRetryOptions" /> (<c>Payments:Retry</c>).
/// </summary>
public sealed class GatewayFailoverOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:GatewayFailover";

    /// <summary>
    ///     Hard cap on gateway attempts for a single charge, across every gateway.
    ///     The issue #413 default is 3: one attempt plus two retries/failovers.
    /// </summary>
    [Range(1, 10)]
    public int MaxAttemptsTotal { get; set; } = 3;

    /// <summary>
    ///     Immediate retries on the same gateway after a retriable gateway failure,
    ///     before failing over to the next-priority gateway. Clamped at runtime by
    ///     <c>Payments:Retry</c> <see cref="PaymentRetryOptions.MaxRetries" /> — when
    ///     retries are disabled there, no same-gateway retry happens.
    /// </summary>
    [Range(0, 5)]
    public int SameGatewayRetryLimit { get; set; } = 1;

    /// <summary>
    ///     Consecutive retriable gateway failures required to open a gateway's circuit.
    /// </summary>
    [Range(1, 100)]
    public int CircuitFailureThreshold { get; set; } = 5;

    /// <summary>
    ///     How long an open circuit stays open before a half-open probe attempt is allowed.
    /// </summary>
    [Range(1, 3600)]
    public int CircuitOpenSeconds { get; set; } = 60;
}

/// <summary>
///     Startup validation for <see cref="GatewayFailoverOptions" />; fails fast on out-of-range values.
/// </summary>
public sealed class GatewayFailoverOptionsValidator : IValidateOptions<GatewayFailoverOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GatewayFailoverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxAttemptsTotal is < 1 or > 10)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(GatewayFailoverOptions.MaxAttemptsTotal)} must be between 1 and 10.");
        }

        if (options.SameGatewayRetryLimit is < 0 or > 5)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(GatewayFailoverOptions.SameGatewayRetryLimit)} must be between 0 and 5.");
        }

        if (options.CircuitFailureThreshold is < 1 or > 100)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(GatewayFailoverOptions.CircuitFailureThreshold)} must be between 1 and 100.");
        }

        if (options.CircuitOpenSeconds is < 1 or > 3600)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(GatewayFailoverOptions.CircuitOpenSeconds)} must be between 1 and 3600 seconds.");
        }

        return ValidateOptionsResult.Success;
    }
}
