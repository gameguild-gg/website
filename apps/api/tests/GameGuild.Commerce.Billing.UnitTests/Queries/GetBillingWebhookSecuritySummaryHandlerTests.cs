using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Queries;

public class GetBillingWebhookSecuritySummaryHandlerTests
{
    [Fact]
    public async Task Handle_Aggregates_Allowlist_Monitor_And_Pipeline_State()
    {
        var configuration = new BillingConfiguration
        {
            Webhook = new WebhookSettings
            {
                Security = new WebhookSecuritySettings
                {
                    SourceIpAllowlist = ["203.0.113.0/24"],
                    SuspiciousActivity = new WebhookSuspiciousActivitySettings
                    {
                        Enabled = true,
                        FailureThreshold = 5,
                        WindowSeconds = 60,
                        BlockDurationSeconds = 120
                    }
                }
            }
        };
        var allowlist = new WebhookSourceIpAllowlist(Options.Create(configuration));
        var monitor = new Mock<IWebhookSuspiciousActivityMonitor>();
        monitor
            .Setup(m => m.GetBlockedSources(It.IsAny<DateTime>()))
            .Returns([new WebhookSourceBlockState("198.51.100.7", DateTime.UtcNow.AddMinutes(2), 5)]);

        var alerts = new List<SecurityAlertResponse>
        {
            CreateAlert(Guid.NewGuid(), "rule-1", "suspicious login pattern", SecurityAlertStatus.Open)
        };
        var queryService = new Mock<ISecurityEventQueryService>();
        queryService
            .Setup(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityEventDeliveryStatusResponse
            {
                SpooledEventCount = 2,
                SpoolingEnabled = true
            });
        queryService
            .Setup(q => q.GetAlertsAsync(It.IsAny<SecurityAlertListRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        var handler = CreateHandler(allowlist, monitor.Object, configuration, queryService.Object);

        var summary = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        summary.SourceIpAllowlist.IsEnabled.Should().BeTrue();
        summary.SourceIpAllowlist.ConfiguredNetworks.Should().Contain("203.0.113.0/24");
        summary.SuspiciousActivity.IsEnabled.Should().BeTrue();
        summary.SuspiciousActivity.FailureThreshold.Should().Be(5);
        summary.SuspiciousActivity.BlockedSources.Should().HaveCount(1);
        summary.SuspiciousActivity.BlockedSources[0].SourceKey.Should().Be("198.51.100.7");
        summary.SecurityEventPipeline!.SpooledEventCount.Should().Be(2);
        summary.OpenSecurityAlerts.Should().HaveCount(1);
        summary.OpenSecurityAlerts[0].RuleId.Should().Be("rule-1");
        summary.GeneratedAtUtc.Should().BeOnOrBefore(DateTime.UtcNow);
        monitor.Verify(m => m.Prune(It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Reports_Disabled_Controls_When_Using_Defaults()
    {
        var configuration = new BillingConfiguration();
        var allowlist = new WebhookSourceIpAllowlist(Options.Create(configuration));
        var monitor = new Mock<IWebhookSuspiciousActivityMonitor>();
        monitor
            .Setup(m => m.GetBlockedSources(It.IsAny<DateTime>()))
            .Returns([]);

        var handler = CreateHandler(allowlist, monitor.Object, configuration, Mock.Of<ISecurityEventQueryService>());

        var summary = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        summary.SourceIpAllowlist.IsEnabled.Should().BeFalse();
        summary.SourceIpAllowlist.ConfiguredNetworkCount.Should().Be(0);
        summary.SuspiciousActivity.IsEnabled.Should().BeFalse();
        summary.SuspiciousActivity.BlockedSources.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Tolerates_Pipeline_Read_Failures()
    {
        var configuration = new BillingConfiguration();
        var allowlist = new WebhookSourceIpAllowlist(Options.Create(configuration));
        var queryService = new Mock<ISecurityEventQueryService>();
        queryService
            .Setup(q => q.GetDeliveryStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("down"));
        queryService
            .Setup(q => q.GetAlertsAsync(It.IsAny<SecurityAlertListRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("down"));

        var handler = CreateHandler(allowlist, Mock.Of<IWebhookSuspiciousActivityMonitor>(), configuration, queryService.Object);

        var summary = await handler.Handle(new GetBillingWebhookSecuritySummaryQuery(), CancellationToken.None);

        summary.SecurityEventPipeline.Should().BeNull();
        summary.OpenSecurityAlerts.Should().BeEmpty();
    }

    private static GetBillingWebhookSecuritySummaryHandler CreateHandler(
        WebhookSourceIpAllowlist allowlist,
        IWebhookSuspiciousActivityMonitor monitor,
        BillingConfiguration configuration,
        ISecurityEventQueryService queryService) =>
        new(
            allowlist,
            monitor,
            Options.Create(configuration),
            queryService,
            NullLogger<GetBillingWebhookSecuritySummaryHandler>.Instance);

    private static SecurityAlertResponse CreateAlert(Guid id, string ruleId, string title, SecurityAlertStatus status) =>
        new(
            id,
            TenantId: null,
            ruleId,
            SecurityEventKind.ThreatDetection,
            AuditRiskLevel.High,
            title,
            "description",
            SourceActionType: AuditActionTypes.WebhookSignatureFailed,
            SourceAuditLogId: null,
            SubjectUserId: null,
            IpAddress: null,
            status,
            OccurrenceCount: 1,
            FirstSeenAtUtc: DateTime.UtcNow.AddMinutes(-5),
            LastSeenAtUtc: DateTime.UtcNow,
            AcknowledgedByUserId: null,
            AcknowledgedAtUtc: null,
            AcknowledgementNotes: null);
}
