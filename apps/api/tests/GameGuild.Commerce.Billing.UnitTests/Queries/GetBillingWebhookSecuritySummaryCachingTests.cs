using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.CQRS.Implementation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Queries;

/// <summary>
///     Issue #394 (query handler caching strategy): the webhook security summary is served from a
///     short-TTL <see cref="ICacheService"/> cache. These tests cover cache miss (first call populates
///     the cache), cache hit (subsequent calls within the TTL skip recomputation), TTL expiry (an
///     expired entry is treated as a miss), eviction on webhook security events, and cache-layer
///     fault tolerance.
/// </summary>
public class GetBillingWebhookSecuritySummaryCachingTests
{
    private readonly MemoryCacheService _cache = new(new MemoryCache(new MemoryCacheOptions()));
    private readonly Mock<IWebhookSuspiciousActivityMonitor> _monitor = new();
    private readonly Mock<ISecurityEventQueryService> _queryService = new();

    public GetBillingWebhookSecuritySummaryCachingTests()
    {
        _monitor
            .Setup(m => m.GetBlockedSources(It.IsAny<DateTime>()))
            .Returns([]);

        _queryService
            .Setup(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityEventDeliveryStatusResponse { SpooledEventCount = 2, SpoolingEnabled = true });
        _queryService
            .Setup(q => q.GetAlertsAsync(It.IsAny<SecurityAlertListRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private GetBillingWebhookSecuritySummaryHandler CreateHandler() =>
        GetBillingWebhookSecuritySummaryHandlerTests.CreateHandler(
            new WebhookSourceIpAllowlist(Options.Create(new BillingConfiguration())),
            _monitor.Object,
            new BillingConfiguration(),
            _queryService.Object,
            _cache);

    [Fact]
    public async Task First_Call_Is_A_Cache_Miss_And_Populates_The_Cache()
    {
        var handler = CreateHandler();

        var summary = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        summary.Should().NotBeNull();
        _queryService.Verify(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
        var cached = await _cache.GetAsync<BillingWebhookSecuritySummaryDto>(
            GetBillingWebhookSecuritySummaryQuery.CacheKey, CancellationToken.None);
        cached.Should().BeSameAs(summary, "the first (miss) call must store the computed summary in the cache");
    }

    [Fact]
    public async Task Second_Call_Within_The_Ttl_Is_A_Cache_Hit_And_Does_Not_Recompute()
    {
        var handler = CreateHandler();

        var first = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);
        var second = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        second.Should().BeSameAs(first, "the second call within the TTL must be served from the cache");
        _queryService.Verify(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()), Times.Once,
            "a cache hit must not re-query the security event pipeline");
        _queryService.Verify(
            q => q.GetAlertsAsync(It.IsAny<SecurityAlertListRequest>(), It.IsAny<CancellationToken>()),
            Times.Once, "a cache hit must not re-read open alerts");
        _monitor.Verify(m => m.Prune(It.IsAny<DateTime>()), Times.Once,
            "a cache hit must not touch the suspicious-activity monitor");
    }

    [Fact]
    public async Task Expired_Entry_Is_Treated_As_A_Miss_And_The_Summary_Is_Recomputed()
    {
        var stale = new BillingWebhookSecuritySummaryDto
        {
            GeneratedAtUtc = DateTime.UtcNow.AddHours(-1),
            SecurityEventPipeline = new SecurityEventDeliveryStatusResponse { SpooledEventCount = 99 }
        };
        // Seed the cache with an entry whose TTL has (almost immediately) elapsed, then let it expire.
        await _cache.SetAsync(GetBillingWebhookSecuritySummaryQuery.CacheKey, stale, TimeSpan.FromMilliseconds(1), CancellationToken.None);
        await Task.Delay(80);

        var handler = CreateHandler();
        var summary = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        summary.Should().NotBeSameAs(stale, "an expired cache entry must not be served");
        summary.GeneratedAtUtc.Should().BeAfter(stale.GeneratedAtUtc, "the summary must be freshly recomputed after TTL expiry");
        summary.SecurityEventPipeline!.SpooledEventCount.Should().Be(2,
            "the recomputed summary reflects the live pipeline read, not the stale entry");
        _queryService.Verify(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publishing_A_Webhook_Security_Event_Evicts_The_Cached_Summary()
    {
        var handler = CreateHandler();
        await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);
        (await _cache.GetAsync<BillingWebhookSecuritySummaryDto>(GetBillingWebhookSecuritySummaryQuery.CacheKey, CancellationToken.None))
            .Should().NotBeNull("precondition: the summary is cached");

        var securityLogger = new Mock<ISecurityEventLogger>();
        securityLogger
            .Setup(l => l.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityEventCaptureResult(
                SecurityEventCaptureOutcome.PersistedToDatabase,
                new ClassifiedSecurityEvent(SecurityEventKind.ThreatDetection, AuditRiskLevel.High, true, "description", AuditActionTypes.WebhookSignatureFailed),
                Guid.NewGuid()));
        var publisher = new WebhookSecurityEventPublisher(securityLogger.Object, _cache, NullLogger<WebhookSecurityEventPublisher>.Instance);

        await publisher.PublishAsync(WebhookSecurityEventKind.SignatureFailed, "stripe", "198.51.100.7", "signature mismatch", "evt-1");

        (await _cache.GetAsync<BillingWebhookSecuritySummaryDto>(GetBillingWebhookSecuritySummaryQuery.CacheKey, CancellationToken.None))
            .Should().BeNull("publishing a webhook security event must evict the cached summary");

        var recomputed = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);
        recomputed.Should().NotBeNull();
        _queryService.Verify(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()), Times.Exactly(2),
            "after eviction the next call must recompute the summary");
    }

    [Fact]
    public async Task Cache_Layer_Failures_Never_Fail_The_Query()
    {
        var throwingCache = new Mock<ICacheService>();
        throwingCache
            .Setup(c => c.GetAsync<BillingWebhookSecuritySummaryDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        throwingCache
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<BillingWebhookSecuritySummaryDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var handler = GetBillingWebhookSecuritySummaryHandlerTests.CreateHandler(
            new WebhookSourceIpAllowlist(Options.Create(new BillingConfiguration())),
            _monitor.Object,
            new BillingConfiguration(),
            _queryService.Object,
            throwingCache.Object);

        var summary = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        summary.Should().NotBeNull("the monitoring query must stay available when the cache layer fails");
        summary.SecurityEventPipeline!.SpooledEventCount.Should().Be(2);
    }

    [Fact]
    public void Cache_Time_To_Live_Is_Within_The_Intended_Short_Window()
    {
        GetBillingWebhookSecuritySummaryQuery.CacheTimeToLive.Should().BeGreaterThan(TimeSpan.Zero);
        GetBillingWebhookSecuritySummaryQuery.CacheTimeToLive.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30),
            "the summary cache TTL must stay in the 10-30s freshness window");
        GetBillingWebhookSecuritySummaryQuery.CacheTimeToLive.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(10));
    }
}
