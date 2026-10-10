using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Handler for <see cref="GetBillingWebhookSecuritySummaryQuery"/>. Aggregates the billing-side
///     webhook security state (CIDR allowlist, threshold blocking) with the delivery health of the
///     central Compliance.Audit security event pipeline.
///     The aggregation is served from a short-TTL cache (issue #394) because it is a SystemAdmin
///     monitoring surface that tolerates seconds of staleness; <see cref="WebhookSecurityEventPublisher"/>
///     evicts the cached entry whenever a webhook security event is published.
/// </summary>
public sealed class GetBillingWebhookSecuritySummaryHandler(
    WebhookSourceIpAllowlist allowlist,
    IWebhookSuspiciousActivityMonitor suspiciousActivityMonitor,
    IOptions<BillingConfiguration> billingConfiguration,
    ISecurityEventQueryService securityEventQueryService,
    ICacheService cacheService,
    ILogger<GetBillingWebhookSecuritySummaryHandler> logger) : IQueryHandler<GetBillingWebhookSecuritySummaryQuery, BillingWebhookSecuritySummaryDto>
{
    public async Task<BillingWebhookSecuritySummaryDto> Handle(GetBillingWebhookSecuritySummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cached = await TryGetCachedSummaryAsync(cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            logger.LogDebug(
                "Serving billing webhook security summary from the {Ttl} query cache.",
                GetBillingWebhookSecuritySummaryQuery.CacheTimeToLive);
            return cached;
        }

        var now = SystemClock.UtcNow;
        var securitySettings = billingConfiguration.Value.Webhook.Security;

        GameGuild.Compliance.Audit.SecurityEventDeliveryStatusResponse? pipelineStatus = null;
        try
        {
            pipelineStatus = await securityEventQueryService
                .GetDeliveryStatusAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The summary must stay available even when the pipeline read side fails.
            logger.LogError(exception, "Failed to read security event pipeline delivery status for the webhook security summary.");
        }

        IReadOnlyList<BillingWebhookSecurityAlertDto> openAlerts = [];
        try
        {
            var alerts = await securityEventQueryService
                .GetAlertsAsync(new SecurityAlertListRequest { Status = SecurityAlertStatus.Open, Take = 10 }, cancellationToken)
                .ConfigureAwait(false);

            openAlerts = alerts
                .Select(alert => new BillingWebhookSecurityAlertDto(
                    alert.Id,
                    alert.RuleId,
                    alert.Kind.ToString(),
                    alert.Severity.ToString(),
                    alert.Title,
                    alert.LastSeenAtUtc))
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to read open security alerts for the webhook security summary.");
        }

        suspiciousActivityMonitor.Prune(now);

        var summary = new BillingWebhookSecuritySummaryDto
        {
            SourceIpAllowlist = new BillingWebhookAllowlistStatusDto
            {
                IsEnabled = allowlist.IsEnabled,
                ConfiguredNetworkCount = securitySettings.SourceIpAllowlist.Count,
                ConfiguredNetworks = securitySettings.SourceIpAllowlist
            },
            SuspiciousActivity = new BillingWebhookSuspiciousActivityStatusDto
            {
                IsEnabled = securitySettings.SuspiciousActivity.Enabled,
                FailureThreshold = securitySettings.SuspiciousActivity.FailureThreshold,
                WindowSeconds = securitySettings.SuspiciousActivity.WindowSeconds,
                BlockDurationSeconds = securitySettings.SuspiciousActivity.BlockDurationSeconds,
                BlockedSources = suspiciousActivityMonitor.GetBlockedSources(now)
                    .Select(block => new BillingWebhookBlockedSourceDto(block.SourceKey, block.BlockedUntilUtc, block.FailureCount))
                    .ToList()
            },
            GeneratedAtUtc = now,
            SecurityEventPipeline = pipelineStatus,
            OpenSecurityAlerts = openAlerts
        };

        await TryCacheSummaryAsync(summary, cancellationToken).ConfigureAwait(false);

        return summary;
    }

    /// <summary>
    ///     Reads the cached summary. Cache reads are best-effort: a cache-layer failure must never
    ///     make the SystemAdmin monitoring query unavailable, so the summary is recomputed instead.
    /// </summary>
    private async Task<BillingWebhookSecuritySummaryDto?> TryGetCachedSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await cacheService
                .GetAsync<BillingWebhookSecuritySummaryDto>(GetBillingWebhookSecuritySummaryQuery.CacheKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to read the cached webhook security summary; recomputing.");
            return null;
        }
    }

    /// <summary>
    ///     Stores the freshly computed summary with the short TTL. Best-effort for the same
    ///     reason as the read path (e.g., a sized in-process cache rejects unsized entries).
    /// </summary>
    private async Task TryCacheSummaryAsync(BillingWebhookSecuritySummaryDto summary, CancellationToken cancellationToken)
    {
        try
        {
            await cacheService
                .SetAsync(GetBillingWebhookSecuritySummaryQuery.CacheKey, summary, GetBillingWebhookSecuritySummaryQuery.CacheTimeToLive, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to cache the webhook security summary; returning the freshly computed value.");
        }
    }
}
