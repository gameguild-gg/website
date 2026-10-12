using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

/// <summary>
///     Verifies that the Stripe, PayPal, and Apple webhook services publish security events
///     into the Compliance.Audit pipeline on verification failures and replay detections.
/// </summary>
public class WebhookSecurityEventPublishingTests
{
    [Fact]
    public async Task Stripe_Service_Publishes_SignatureFailed_When_Verification_Throws()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        var verifier = new Mock<IStripeWebhookVerifier>();
        verifier
            .Setup(v => v.Verify("payload", "bad-sig"))
            .Throws(new InvalidWebhookSignatureException("Stripe signature verification failed."));
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = CreateStripeService(repository, verifier.Object, securityEvents.Object);

        var act = () => service.ProcessStripeWebhookAsync("payload", "bad-sig");

        await act.Should().ThrowAsync<InvalidWebhookSignatureException>();
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.SignatureFailed,
            PaymentProviders.Stripe,
            null,
            "Stripe signature verification failed.",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stripe_Service_Publishes_ReplayDetected_For_Processed_Duplicates()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByProviderScopeAsync(
                PaymentProviders.Stripe, "test", "platform", "we_test", "evt_stripe_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent
            {
                ExternalEventId = "evt_stripe_1",
                Provider = PaymentProviders.Stripe,
                IsProcessed = true,
                ProcessedAt = DateTime.UtcNow
            });
        var verifier = new Mock<IStripeWebhookVerifier>();
        verifier
            .Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(CreateVerifiedEvent("evt_stripe_1"));
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = CreateStripeService(repository, verifier.Object, securityEvents.Object);

        var result = await service.ProcessStripeWebhookAsync("{}", "sig");

        result.WasAlreadyProcessed.Should().BeTrue();
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.ReplayDetected,
            PaymentProviders.Stripe,
            null,
            It.IsAny<string>(),
            "evt_stripe_1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayPal_Service_Publishes_SignatureFailed_When_The_Signature_Is_Invalid()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync(It.IsAny<string>(), PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Failed("invalid signature"));
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = new PayPalBillingWebhookService(
            repository.Object,
            verification.Object,
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            securityEvents.Object);

        var result = await service.ProcessPayPalWebhookAsync("wh", "{}", "tx", "time", "sig");

        result.Processed.Should().BeFalse();
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.SignatureFailed,
            PaymentProviders.PayPal,
            null,
            "invalid signature",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayPal_Service_Publishes_ReplayDetected_For_Duplicates()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent { ExternalEventId = "tx", ProcessedAt = DateTime.UtcNow });
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = new PayPalBillingWebhookService(
            repository.Object,
            Mock.Of<IPayPalSignatureVerificationService>(),
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            securityEvents.Object);

        var result = await service.ProcessPayPalWebhookAsync("wh", "{}", "tx", "time", "sig");

        result.WasAlreadyProcessed.Should().BeTrue();
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.ReplayDetected,
            PaymentProviders.PayPal,
            null,
            It.IsAny<string>(),
            "tx",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Apple_Service_Publishes_SignatureFailed_When_Notification_Validation_Fails()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        var validation = new Mock<IApplePayReceiptValidationService>();
        validation
            .Setup(v => v.VerifyNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppleNotificationVerificationResult.Failed("bad notification"));
        var securityEvents = new Mock<IWebhookSecurityEventPublisher>();

        var service = new ApplePayBillingWebhookService(
            repository.Object,
            validation.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            securityEvents.Object);

        var result = await service.ProcessAppStoreNotificationAsync("payload");

        result.Processed.Should().BeFalse();
        securityEvents.Verify(p => p.PublishAsync(
            WebhookSecurityEventKind.SignatureFailed,
            PaymentProviders.AppleAppStore,
            null,
            "bad notification",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Provider_Services_Still_Work_Without_A_Security_Event_Publisher()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        var validation = new Mock<IApplePayReceiptValidationService>();
        validation
            .Setup(v => v.VerifyNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppleNotificationVerificationResult.Failed("bad notification"));

        var service = new ApplePayBillingWebhookService(
            repository.Object,
            validation.Object,
            NullLogger<ApplePayBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            securityEvents: null);

        var result = await service.ProcessAppStoreNotificationAsync("payload");

        result.Processed.Should().BeFalse();
    }

    private static StripeBillingWebhookService CreateStripeService(
        Mock<IBillingWebhookRepository> repository,
        IStripeWebhookVerifier verifier,
        IWebhookSecurityEventPublisher securityEvents) =>
        new(
            repository.Object,
            verifier,
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            Options.Create(new BillingConfiguration()),
            verifiedEventConsumers: null,
            securityEvents: securityEvents);

    private static VerifiedStripeWebhookEvent CreateVerifiedEvent(string eventId) => new()
    {
        EventId = eventId,
        EventType = "invoice.paid",
        ProviderEnvironment = "test",
        ProviderAccountId = "platform",
        WebhookEndpointId = "we_test",
        EventSchemaVersion = "2023-10-16",
        ProviderObjectId = "obj_test",
        ProviderObjectType = "test_object",
        ProviderMonetaryLeg = "nonmonetary",
        VerifiedPayload = "{}",
        RetainedPayload = "{}",
        PayloadSha256 = new string('0', 64)
    };
}
