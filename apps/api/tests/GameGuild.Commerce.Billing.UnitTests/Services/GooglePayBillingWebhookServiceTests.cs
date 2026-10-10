using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class GooglePayBillingWebhookServiceTests
{
    [Fact]
    public async Task ProcessGooglePayWebhookAsync_Throws_On_Verification_Failure_And_Publishes_A_Security_Event()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        var verification = new Mock<IGooglePayWebhookVerificationService>();
        verification
            .Setup(v => v.Verify("payload", "Bearer bad", "project"))
            .Returns(GooglePayWebhookVerificationResult.Failure("Bearer JWT signature verification failed."));
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = CreateService(repository, verification.Object, securityEvents.Object);

        var act = () => service.ProcessGooglePayWebhookAsync("payload", "Bearer bad", "project");

        await act.Should().ThrowAsync<InvalidWebhookSignatureException>();
        repository.VerifyNoOtherCalls();
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.SignatureFailed,
            PaymentProviders.GooglePay,
            null,
            "Bearer JWT signature verification failed.",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGooglePayWebhookAsync_Acknowledges_Duplicates_Without_Reprocessing()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt-1", PaymentProviders.GooglePay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent
            {
                ExternalEventId = "evt-1",
                Provider = PaymentProviders.GooglePay,
                ProcessedAt = DateTime.UtcNow
            });
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = CreateService(repository, Verified("evt-1"), securityEvents.Object);

        var result = await service.ProcessGooglePayWebhookAsync("{}", "Bearer token", "project");

        result.WasAlreadyProcessed.Should().BeTrue();
        repository.Verify(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.ReplayDetected,
            PaymentProviders.GooglePay,
            null,
            It.IsAny<string>(),
            "evt-1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGooglePayWebhookAsync_Creates_Inbox_Event_Marked_Processed_On_Success()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt-1", PaymentProviders.GooglePay, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        BillingWebhookEvent? stored = null;
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<BillingWebhookEvent, CancellationToken>((e, _) => stored = e)
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle
            .Setup(l => l.CreateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<BillingCycle>(),
                It.IsAny<Money>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var service = CreateService(repository, Verified("evt-1", "SUBSCRIPTION_PURCHASED"), lifecycle: lifecycle.Object);

        var payload = """
        {
            "event_id": "evt-1",
            "event_type": "SUBSCRIPTION_PURCHASED",
            "subscription": { "subscription_id": "sub-1", "status": "ACTIVE" },
            "metadata": { "tenant_id": "00000000-0000-0000-0000-000000000001", "plan_id": "00000000-0000-0000-0000-000000000002" }
        }
        """;
        var result = await service.ProcessGooglePayWebhookAsync(payload, "Bearer token", "project");

        result.Processed.Should().BeTrue();
        result.EventId.Should().Be("evt-1");
        stored.Should().NotBeNull();
        stored!.ExternalEventId.Should().Be("evt-1");
        stored.Provider.Should().Be(PaymentProviders.GooglePay);
        stored.EventType.Should().Be("SUBSCRIPTION_PURCHASED");
        stored.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessGooglePayWebhookAsync_Marks_The_Event_Failed_When_Routing_Throws()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt-1", PaymentProviders.GooglePay, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle
            .Setup(l => l.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<BillingCycle>(), It.IsAny<Money>(),
                It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("downstream down"));

        var service = CreateService(repository, Verified("evt-1", "SUBSCRIPTION_PURCHASED"), lifecycle: lifecycle.Object);

        var payload = """
        {
            "event_id": "evt-1",
            "event_type": "SUBSCRIPTION_PURCHASED",
            "subscription": { "subscription_id": "sub-1", "status": "ACTIVE" },
            "metadata": { "tenant_id": "00000000-0000-0000-0000-000000000001", "plan_id": "00000000-0000-0000-0000-000000000002" }
        }
        """;
        var result = await service.ProcessGooglePayWebhookAsync(payload, "Bearer token", "project");

        result.Processed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("downstream down");
    }

    [Fact]
    public async Task ProcessGooglePayWebhookAsync_Tolerates_A_Null_Security_Event_Publisher()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt-1", PaymentProviders.GooglePay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent { ExternalEventId = "evt-1", ProcessedAt = DateTime.UtcNow });

        var service = CreateService(repository, Verified("evt-1"), securityEvents: null);

        var result = await service.ProcessGooglePayWebhookAsync("{}", "Bearer token", "project");

        result.WasAlreadyProcessed.Should().BeTrue();
    }

    private static IGooglePayWebhookVerificationService Verified(string eventId, string eventType = "unknown")
    {
        var verification = new Mock<IGooglePayWebhookVerificationService>();
        verification
            .Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new GooglePayWebhookVerificationResult
            {
                IsValid = true,
                EventId = eventId,
                EventType = eventType,
                OccurredAtUtc = DateTime.UtcNow
            });
        return verification.Object;
    }

    private static GooglePayBillingWebhookService CreateService(
        Mock<IBillingWebhookRepository> repository,
        IGooglePayWebhookVerificationService verification,
        IWebhookSecurityEventPublisher? securityEvents = null,
        ISubscriptionLifecycleService? lifecycle = null) =>
        new(
            repository.Object,
            verification,
            NullLogger<GooglePayBillingWebhookService>.Instance,
            lifecycle ?? Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            securityEvents);
}
