using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public sealed class BillingWebhookDeadLetterServiceTests
{
    private readonly Mock<IBillingWebhookRepository> _webhookRepository = new();
    private readonly Mock<IWebhookSecurityEventPublisher> _securityEvents = new();
    private readonly Mock<IBillingIntegrationEventPublisher> _billingEvents = new();

    private BillingWebhookDeadLetterService CreateService() => new(
        _webhookRepository.Object,
        _securityEvents.Object,
        _billingEvents.Object,
        NullLogger<BillingWebhookDeadLetterService>.Instance);

    [Fact]
    public async Task DeadLetterAsync_ShouldMarkTheRow_AndPublishSecurityAndIntegrationEvents()
    {
        var row = new BillingWebhookEvent
        {
            ExternalEventId = "evt_dl",
            Provider = PaymentProviders.Stripe,
            EventType = "invoice.payment_succeeded",
            ProcessingAttempts = 5
        };
        _webhookRepository
            .Setup(repository => repository.UpdateAsync(row, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        await CreateService().DeadLetterAsync(row, "Maximum retry attempts (5) exceeded", CancellationToken.None);

        row.DeadLetteredAt.Should().NotBeNull();
        row.DeadLetterReason.Should().Be("Maximum retry attempts (5) exceeded");

        _securityEvents.Verify(publisher => publisher.PublishAsync(
            WebhookSecurityEventKind.DeadLettered,
            PaymentProviders.Stripe,
            null,
            It.Is<string>(detail => detail.Contains("evt_dl") && detail.Contains("5 attempt(s)")),
            "evt_dl",
            It.IsAny<CancellationToken>()), Times.Once,
            "the dead-letter must raise a security event so the central alert pipeline surfaces repeated delivery failures");
        _billingEvents.Verify(publisher => publisher.PublishWebhookFailedAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeadLetterAsync_ShouldBeIdempotent_ForAlreadyDeadLetteredRows()
    {
        var row = new BillingWebhookEvent { ExternalEventId = "evt_dl", Provider = PaymentProviders.PayPal };
        row.MarkAsDeadLettered("already parked");

        await CreateService().DeadLetterAsync(row, "second attempt", CancellationToken.None);

        row.DeadLetterReason.Should().Be("already parked");
        _webhookRepository.Verify(repository => repository.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _securityEvents.Verify(publisher => publisher.PublishAsync(
            It.IsAny<WebhookSecurityEventKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeadLetterAsync_ShouldStillPersist_WhenSecurityPublishingFails()
    {
        // Publishing is best-effort: an audit-pipeline outage must not lose the dead-letter state.
        var row = new BillingWebhookEvent { ExternalEventId = "evt_dl", Provider = PaymentProviders.Stripe, ProcessingAttempts = 5 };
        _webhookRepository
            .Setup(repository => repository.UpdateAsync(row, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        _securityEvents
            .Setup(publisher => publisher.PublishAsync(It.IsAny<WebhookSecurityEventKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pipeline unavailable"));

        var act = () => CreateService().DeadLetterAsync(row, "reason", CancellationToken.None);

        await act.Should().NotThrowAsync();
        row.DeadLetteredAt.Should().NotBeNull();
    }
}
