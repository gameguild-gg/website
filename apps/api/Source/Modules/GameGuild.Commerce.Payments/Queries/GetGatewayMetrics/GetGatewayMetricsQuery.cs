using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Per-gateway health and performance snapshot over the rolling 24h/7d/30d windows
///     (issue #413 follow-up): window aggregates from <see cref="IGatewayMetricsService"/>
///     merged with the live circuit-breaker state from <see cref="IGatewayCircuitBreaker"/>.
///     Requires the <c>payments:admin</c> permission; the tenant scope is enforced at the
///     API boundary from the actor context.
/// </summary>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary.</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record GetGatewayMetricsQuery(Guid? TenantId = null) : IQuery<GatewayMetricsReport>;

/// <summary>
///     Combined read model of one reporting pass: rolling-window aggregates per gateway
///     plus the current circuit-breaker snapshot of every known gateway.
/// </summary>
public sealed record GatewayMetricsReport
{
    /// <summary>UTC instant the report was generated.</summary>
    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    ///     Window aggregates (24h/7d/30d) per provider with payments in the window;
    ///     providers without payments in a window are omitted from that window.
    /// </summary>
    public required IReadOnlyList<GatewayWindowMetrics> Windows { get; init; }

    /// <summary>Circuit-breaker snapshot of every known gateway at generation time.</summary>
    public required IReadOnlyList<GatewayCircuitSnapshot> CircuitBreakers { get; init; }
}

/// <summary>Handler for <see cref="GetGatewayMetricsQuery"/>.</summary>
public sealed class GetGatewayMetricsQueryHandler(
    IGatewayMetricsService gatewayMetrics,
    IGatewayCircuitBreaker circuitBreaker) : IQueryHandler<GetGatewayMetricsQuery, GatewayMetricsReport>
{
    public async Task<GatewayMetricsReport> Handle(GetGatewayMetricsQuery request, CancellationToken cancellationToken)
    {
        var windows = await gatewayMetrics
            .GetWindowMetricsAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return new GatewayMetricsReport
        {
            GeneratedAtUtc = SystemClock.UtcNow,
            Windows = windows,
            CircuitBreakers = circuitBreaker.GetAllSnapshots()
        };
    }
}
