using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class PermissionCacheMetricsMonitorTests
{
    [Fact]
    public void AlertEvaluator_ReportsLowHitRateAndSlowCacheTypesOnlyAfterMinimumSamples()
    {
        var options = new AuthorizationCacheOptions
        {
            MinimumHitRateWarningThreshold = 0.70,
            MinimumRequestsForPerformanceWarning = 10,
            LookupLatencyWarningThresholdMilliseconds = 50
        };
        var statistics = new CacheStatistics
        {
            L1Hits = 2,
            Misses = 8,
            LookupDurationByType = new Dictionary<string, CacheLookupStatistics>(StringComparer.Ordinal)
            {
                ["acl"] = new(10, 75),
                ["policy"] = new(9, 500)
            }
        };

        var alerts = PermissionCacheAlertEvaluator.Evaluate(statistics, options);

        alerts.Should().ContainSingle(alert => alert.Code == "permission-cache.low-hit-rate");
        alerts.Should().ContainSingle(alert => alert.Code == "permission-cache.high-lookup-latency" && alert.CacheType == "acl");
        alerts.Should().NotContain(alert => alert.CacheType == "policy", "the policy sample is below the minimum request count");
    }

    [Fact]
    public void AlertEvaluator_DoesNotReportHealthyMetrics()
    {
        var options = new AuthorizationCacheOptions
        {
            MinimumHitRateWarningThreshold = 0.70,
            MinimumRequestsForPerformanceWarning = 10,
            LookupLatencyWarningThresholdMilliseconds = 50
        };
        var statistics = new CacheStatistics
        {
            L1Hits = 9,
            Misses = 1,
            LookupDurationByType = new Dictionary<string, CacheLookupStatistics>(StringComparer.Ordinal)
            {
                ["acl"] = new(10, 25)
            }
        };

        PermissionCacheAlertEvaluator.Evaluate(statistics, options).Should().BeEmpty();
    }

    [Fact]
    public void DisabledMetricsService_DoesNotCollectOrExposeStatistics()
    {
        var metrics = new CacheMetricsService(enabled: false);
        metrics.RecordHit(CacheLevel.L1, "acl");
        metrics.RecordMiss("acl");
        metrics.RecordLookupDuration(TimeSpan.FromMilliseconds(500), "acl");
        metrics.RecordEviction(CacheLevel.L1, "acl");

        var statistics = metrics.GetStatistics();

        statistics.TotalRequests.Should().Be(0);
        statistics.Evictions.Should().Be(0);
        statistics.LookupDurationByType.Should().BeEmpty();
    }

    [Fact]
    public void MetricsMonitor_DoesNotReportWhenMetricsAreDisabled()
    {
        var metrics = new CacheMetricsService();
        var monitor = new PermissionCacheMetricsMonitor(
            metrics,
            Options.Create(new AuthorizationCacheOptions { EnableMetrics = false }),
            NullLogger<PermissionCacheMetricsMonitor>.Instance);

        var report = () => monitor.ReportSnapshot();

        report.Should().NotThrow();
    }
}
