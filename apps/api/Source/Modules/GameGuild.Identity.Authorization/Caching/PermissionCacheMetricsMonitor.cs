using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>A structured warning emitted when permission-cache performance crosses a configured threshold.</summary>
public sealed record PermissionCachePerformanceAlert(string Code, string CacheType, string Message);

/// <summary>Evaluates the current cache snapshot against configured performance thresholds.</summary>
public static class PermissionCacheAlertEvaluator
{
    public static IReadOnlyList<PermissionCachePerformanceAlert> Evaluate(
        CacheStatistics statistics,
        AuthorizationCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(options);

        var alerts = new List<PermissionCachePerformanceAlert>();
        if (statistics.TotalRequests >= options.MinimumRequestsForPerformanceWarning
            && statistics.OverallHitRate < options.MinimumHitRateWarningThreshold)
        {
            alerts.Add(new PermissionCachePerformanceAlert(
                "permission-cache.low-hit-rate",
                "all",
                $"Permission-cache hit rate {statistics.OverallHitRate:P1} is below the configured {options.MinimumHitRateWarningThreshold:P1} threshold across {statistics.TotalRequests} requests."));
        }

        foreach (var (cacheType, lookup) in statistics.LookupDurationByType)
        {
            if (lookup.Count < options.MinimumRequestsForPerformanceWarning
                || lookup.AverageMilliseconds <= options.LookupLatencyWarningThresholdMilliseconds)
            {
                continue;
            }

            alerts.Add(new PermissionCachePerformanceAlert(
                "permission-cache.high-lookup-latency",
                cacheType,
                $"Average {cacheType} cache lookup latency {lookup.AverageMilliseconds:F1} ms exceeds the configured {options.LookupLatencyWarningThresholdMilliseconds:F1} ms threshold across {lookup.Count} lookups."));
        }

        return alerts;
    }
}

/// <summary>Periodically logs cache health and emits structured warnings for configured performance regressions.</summary>
public sealed class PermissionCacheMetricsMonitor(
    ICacheMetricsService metrics,
    IOptions<AuthorizationCacheOptions> options,
    ILogger<PermissionCacheMetricsMonitor> logger) : BackgroundService
{
    private readonly AuthorizationCacheOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.MetricsLoggingIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            ReportSnapshot();
        }
    }

    /// <summary>Logs the current aggregate and evaluates configured warnings; exposed for deterministic verification.</summary>
    public void ReportSnapshot()
    {
        if (!_options.EnableMetrics)
        {
            return;
        }

        var statistics = metrics.GetStatistics();
        logger.LogInformation(
            "Permission-cache health: requests={RequestCount}, l1Hits={L1Hits}, l2Hits={L2Hits}, misses={Misses}, hitRate={HitRate:P1}, evictions={Evictions}",
            statistics.TotalRequests,
            statistics.L1Hits,
            statistics.L2Hits,
            statistics.Misses,
            statistics.OverallHitRate,
            statistics.Evictions);

        foreach (var alert in PermissionCacheAlertEvaluator.Evaluate(statistics, _options))
        {
            logger.LogWarning(
                "Permission-cache performance alert {AlertCode} for {CacheType}: {AlertMessage}",
                alert.Code,
                alert.CacheType,
                alert.Message);
        }
    }
}
