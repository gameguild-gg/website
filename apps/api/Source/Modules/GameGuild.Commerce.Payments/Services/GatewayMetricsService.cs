namespace GameGuild.Commerce.Payments;

/// <summary>
///     Aggregated performance of one gateway over one rolling window, computed from
///     persisted payment records (issue #413).
/// </summary>
/// <param name="ProviderId">Provider identifier of the gateway.</param>
/// <param name="Window">Window label (24h, 7d or 30d).</param>
/// <param name="SinceUtc">Inclusive window start (UTC).</param>
/// <param name="Attempts">Payments recorded for the gateway inside the window.</param>
/// <param name="Succeeded">Payments that reached <see cref="PaymentStatus.Succeeded" />.</param>
/// <param name="Failed">Payments that reached <see cref="PaymentStatus.Failed" />.</param>
/// <param name="Unresolved">Payments still pending/processing/requiring action inside the window.</param>
/// <param name="SuccessRate">Succeeded / Attempts; 0 when no attempts.</param>
/// <param name="AverageProcessingLatencyMs">
///     Mean of <c>ProcessedAt - CreatedAt</c> over succeeded payments, milliseconds; null
///     when no succeeded payment has both timestamps.
/// </param>
public sealed record GatewayWindowMetrics(
    string ProviderId,
    string Window,
    DateTime SinceUtc,
    int Attempts,
    int Succeeded,
    int Failed,
    int Unresolved,
    double SuccessRate,
    double? AverageProcessingLatencyMs);

/// <summary>
///     Per-gateway success-rate and latency aggregates over rolling 24h/7d/30d windows,
///     computed from payment records (issue #413). Together with the provider health surface
///     from the #780 external-providers API and <see cref="IGatewayCircuitBreaker" />
///     snapshots this forms the per-gateway health and performance picture.
/// </summary>
public interface IGatewayMetricsService
{
    /// <summary>
    ///     Computes the 24h/7d/30d window aggregates for every provider found in the
    ///     payment records. Providers without payments in a window are omitted from that
    ///     window's list (no zero-inflation). An optional tenant scope restricts the
    ///     aggregated payment records to one tenant.
    /// </summary>
    Task<IReadOnlyList<GatewayWindowMetrics>> GetWindowMetricsAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IGatewayMetricsService" />: reads payment records through
/// <see cref="IPaymentRepository" /> and aggregates them in memory.
/// </summary>
public sealed class GatewayMetricsService(IPaymentRepository paymentRepository) : IGatewayMetricsService
{
    private static readonly (string Label, TimeSpan Duration)[] Windows =
    [
        ("24h", TimeSpan.FromHours(24)),
        ("7d", TimeSpan.FromDays(7)),
        ("30d", TimeSpan.FromDays(30))
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<GatewayWindowMetrics>> GetWindowMetricsAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var metrics = new List<GatewayWindowMetrics>();

        foreach (var (label, duration) in Windows)
        {
            var since = now.Subtract(duration);
            var payments = await paymentRepository
                .GetByDateRangeAsync(since, now, tenantId, cancellationToken)
                .ConfigureAwait(false);

            metrics.AddRange(
                payments
                    .GroupBy(payment => payment.Provider, StringComparer.OrdinalIgnoreCase)
                    .Select(group => Aggregate(group.Key, group.ToList(), label, since)));
        }

        return metrics;
    }

    private static GatewayWindowMetrics Aggregate(
        string providerId,
        IReadOnlyList<Payment> payments,
        string window,
        DateTime sinceUtc)
    {
        var succeeded = payments.Count(payment => payment.Status == PaymentStatus.Succeeded);
        var failed = payments.Count(payment => payment.Status == PaymentStatus.Failed);
        var unresolved = payments.Count(payment =>
            payment.Status is PaymentStatus.Pending
                or PaymentStatus.Processing
                or PaymentStatus.RequiresAction);

        var latencySamples = payments
            .Where(payment => payment.Status == PaymentStatus.Succeeded && payment.ProcessedAt.HasValue)
            .Select(payment => (payment.ProcessedAt!.Value - payment.CreatedAt).TotalMilliseconds)
            .Where(milliseconds => milliseconds >= 0)
            .ToList();

        return new GatewayWindowMetrics(
            ProviderId: providerId.ToLowerInvariant(),
            Window: window,
            SinceUtc: sinceUtc,
            Attempts: payments.Count,
            Succeeded: succeeded,
            Failed: failed,
            Unresolved: unresolved,
            SuccessRate: payments.Count == 0 ? 0.0 : (double)succeeded / payments.Count,
            AverageProcessingLatencyMs: latencySamples.Count == 0
                ? null
                : Math.Round(latencySamples.Average(), 2, MidpointRounding.AwayFromZero));
    }
}
