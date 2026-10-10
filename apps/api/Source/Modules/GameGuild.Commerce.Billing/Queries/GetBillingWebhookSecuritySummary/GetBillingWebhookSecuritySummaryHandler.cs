using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Handler for <see cref="GetBillingWebhookSecuritySummaryQuery"/>. Aggregates the billing-side
///     webhook security state (CIDR allowlist, threshold blocking) with the delivery health of the
///     central Compliance.Audit security event pipeline.
/// </summary>
public sealed class GetBillingWebhookSecuritySummaryHandler(
    WebhookSourceIpAllowlist allowlist,
    IWebhookSuspiciousActivityMonitor suspiciousActivityMonitor,
    IOptions<BillingConfiguration> billingConfiguration,
    ISecurityEventQueryService securityEventQueryService,
    ILogger<GetBillingWebhookSecuritySummaryHandler> logger) : IQueryHandler<GetBillingWebhookSecuritySummaryQuery, BillingWebhookSecuritySummaryDto>
{
    public async Task<BillingWebhookSecuritySummaryDto> Handle(GetBillingWebhookSecuritySummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

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

        return new BillingWebhookSecuritySummaryDto
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
    }
}
