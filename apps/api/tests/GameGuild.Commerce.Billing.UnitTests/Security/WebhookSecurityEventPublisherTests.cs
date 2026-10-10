using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.CQRS.Implementation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Security;

public class WebhookSecurityEventPublisherTests
{
    private static ICacheService CreateCache() => new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

    [Theory]
    [InlineData(WebhookSecurityEventKind.SignatureFailed, AuditActionTypes.WebhookSignatureFailed, AuditRiskLevel.High)]
    [InlineData(WebhookSecurityEventKind.SourceIpRejected, AuditActionTypes.WebhookSourceIpRejected, AuditRiskLevel.High)]
    [InlineData(WebhookSecurityEventKind.ReplayDetected, AuditActionTypes.WebhookReplayDetected, AuditRiskLevel.Low)]
    [InlineData(WebhookSecurityEventKind.RateLimitExceeded, AuditActionTypes.WebhookRateLimitExceeded, AuditRiskLevel.Medium)]
    [InlineData(WebhookSecurityEventKind.SourceBlocked, AuditActionTypes.WebhookSourceBlocked, AuditRiskLevel.High)]
    public async Task PublishAsync_Maps_Each_Kind_To_Its_Taxonomy_Entry(
        WebhookSecurityEventKind kind,
        string expectedActionType,
        AuditRiskLevel expectedRiskLevel)
    {
        var logger = new Mock<ISecurityEventLogger>();
        CreateAuditLogRequest? captured = null;
        var classification = new ClassifiedSecurityEvent(
            SecurityEventKind.ThreatDetection, AuditRiskLevel.High, true, "description", AuditActionTypes.WebhookSignatureFailed);
        logger
            .Setup(l => l.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAuditLogRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new SecurityEventCaptureResult(SecurityEventCaptureOutcome.PersistedToDatabase, classification, Guid.NewGuid()));

        var publisher = new WebhookSecurityEventPublisher(logger.Object, CreateCache(), NullLogger<WebhookSecurityEventPublisher>.Instance);

        await publisher.PublishAsync(kind, "stripe", "198.51.100.7", "detail", "evt-1");

        captured.Should().NotBeNull();
        captured!.ActionType.Should().Be(expectedActionType);
        captured.ResourceType.Should().Be("BillingWebhook");
        captured.ResourceId.Should().Be("stripe");
        captured.Category.Should().Be(AuditCategory.Security);
        captured.RiskLevel.Should().Be(expectedRiskLevel);
        captured.Success.Should().BeFalse();
        captured.IpAddress.Should().Be("198.51.100.7");
        captured.ErrorMessage.Should().Be("detail");
        var metadata = captured.Metadata.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Which;
        metadata["eventId"].Should().Be("evt-1");
        metadata["provider"].Should().Be("stripe");
    }

    [Fact]
    public async Task PublishAsync_Is_Best_Effort_And_Never_Throws()
    {
        var logger = new Mock<ISecurityEventLogger>();
        logger
            .Setup(l => l.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pipeline down"));

        var publisher = new WebhookSecurityEventPublisher(logger.Object, CreateCache(), NullLogger<WebhookSecurityEventPublisher>.Instance);

        var act = () => publisher.PublishAsync(
            WebhookSecurityEventKind.SignatureFailed, "stripe", null, "detail");

        await act.Should().NotThrowAsync("security telemetry must not change the webhook response");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task PublishAsync_Rejects_Blank_Providers(string provider)
    {
        var publisher = new WebhookSecurityEventPublisher(
            Mock.Of<ISecurityEventLogger>(), CreateCache(), NullLogger<WebhookSecurityEventPublisher>.Instance);

        var act = () => publisher.PublishAsync(WebhookSecurityEventKind.SignatureFailed, provider, null, "detail");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
