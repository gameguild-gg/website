using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class PayPalBillingWebhookServiceTests
{
    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Return_AlreadyProcessed_For_Duplicate()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent { ExternalEventId = "tx", Provider = PaymentProviders.PayPal, IsProcessed = true, ProcessedAt = DateTime.UtcNow });

        var service = CreateService(repository, Mock.Of<IPayPalSignatureVerificationService>(), Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessPayPalWebhookAsync("wh", "{}", "tx", "time", "sig", null, null, CancellationToken.None);

        result.WasAlreadyProcessed.Should().BeTrue();
        repository.Verify(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never,
            "a completed duplicate is acknowledged without reprocessing");
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Reprocess_PreviouslyFailed_Redelivery_Instead_Of_Masking_It_As_Duplicate()
    {
        // Regression (issue #408): a redelivery of a previously FAILED transmission used to be
        // acknowledged as AlreadyProcessed, permanently masking the failure. It must instead be
        // re-verified and re-routed on the existing inbox row.
        var failedRow = new BillingWebhookEvent { ExternalEventId = "tx", Provider = PaymentProviders.PayPal, ProcessingAttempts = 1 };
        failedRow.MarkAsFailed("transient downstream failure");

        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedRow);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

        var service = CreateService(repository, verification.Object, Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"event_type\":\"BILLING.SUBSCRIPTION.UPDATED\",\"resource\":{\"id\":\"sub_1\",\"status\":\"ACTIVE\"}}";
        var result = await service.ProcessPayPalWebhookAsync("wh", payload, "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.WasAlreadyProcessed.Should().BeFalse();
        failedRow.IsProcessed.Should().BeTrue();
        failedRow.IsSignatureVerified.Should().BeTrue();
        failedRow.ProcessingAttempts.Should().Be(2,
            "the redelivery reuses the existing row and counts as its next attempt");
        repository.Verify(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never,
            "no duplicate inbox row may be created for a redelivery");
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Not_Flag_SignatureVerified_For_Failed_Verification()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .Callback<BillingWebhookEvent, CancellationToken>((e, _) => e.MarkAsFailed("placeholder"))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Failed("bad"));

        var service = CreateService(repository, verification.Object, Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessPayPalWebhookAsync("wh", "{}", "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.RequiresRetry.Should().BeFalse("a failed signature is never retried");
    }

    [Fact]
    public async Task ReplayPayPalWebhookAsync_Should_Refuse_Unverified_Rows()
    {
        // Fail closed: the retry pipeline must never re-route a delivery that has no
        // retained proof of signature verification (issue #408).
        var unverifiedRow = new BillingWebhookEvent { ExternalEventId = "tx", Provider = PaymentProviders.PayPal, EventType = "BILLING.SUBSCRIPTION.UPDATED", Payload = "{}" };
        unverifiedRow.MarkAsFailed("Invalid webhook signature: bad");

        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var service = CreateService(repository, Mock.Of<IPayPalSignatureVerificationService>(), Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ReplayPayPalWebhookAsync(unverifiedRow, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.RequiresRetry.Should().BeFalse();
        unverifiedRow.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Return_Failed_When_Signature_Invalid()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Failed("bad"));

        var service = CreateService(repository, verification.Object, Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessPayPalWebhookAsync("wh", "{}", "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bad");
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Create_Subscription_For_Subscription_Created()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

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

        var externalIdService = new Mock<ISubscriptionExternalIdService>();

        var service = CreateService(repository, verification.Object, lifecycle.Object, externalIdService.Object);

        var payload = "{\"event_type\":\"BILLING.SUBSCRIPTION.CREATED\",\"resource\":{\"id\":\"sub_123\",\"status\":\"ACTIVE\",\"amount\":{\"total\":\"10.00\",\"currency\":\"USD\"}}}";
        var result = await service.ProcessPayPalWebhookAsync("wh", payload, "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeTrue();
        externalIdService.Verify(s => s.SetExternalIdsAsync(subscription.Id, "sub_123", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Record_Failure_On_Capture_Denied()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub_999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var billing = new Mock<ISubscriptionBillingService>();

        var service = new PayPalBillingWebhookService(
            repository.Object,
            verification.Object,
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            query.Object,
            billing.Object,
            Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"event_type\":\"PAYMENT.CAPTURE.DENIED\",\"resource\":{\"id\":\"pay_1\",\"billing_agreement_id\":\"sub_999\",\"status\":\"DENIED\",\"amount\":{\"total\":\"10.00\",\"currency\":\"USD\"}}}";
        var result = await service.ProcessPayPalWebhookAsync("wh", payload, "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeTrue();
        billing.Verify(b => b.RecordPaymentFailureAsync(subscription.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Cancel_Subscription_On_Cancelled()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub_888", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = new PayPalBillingWebhookService(
            repository.Object,
            verification.Object,
            NullLogger<PayPalBillingWebhookService>.Instance,
            lifecycle.Object,
            query.Object,
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"event_type\":\"BILLING.SUBSCRIPTION.CANCELLED\",\"resource\":{\"id\":\"sub_888\",\"status\":\"CANCELLED\"}}";
        var result = await service.ProcessPayPalWebhookAsync("wh", payload, "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeTrue();
        lifecycle.Verify(l => l.CancelAsync(subscription.Id, CancellationReason.Custom, It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPayPalWebhookAsync_Should_Record_Payment_On_Sale_Completed()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var verification = new Mock<IPayPalSignatureVerificationService>();
        verification
            .Setup(v => v.VerifySignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Success());

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub_777", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var billing = new Mock<ISubscriptionBillingService>();

        var service = new PayPalBillingWebhookService(
            repository.Object,
            verification.Object,
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            query.Object,
            billing.Object,
            Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"event_type\":\"PAYMENT.SALE.COMPLETED\",\"resource\":{\"id\":\"pay_2\",\"billing_agreement_id\":\"sub_777\",\"status\":\"COMPLETED\",\"amount\":{\"total\":\"10.00\",\"currency\":\"USD\"}}}";
        var result = await service.ProcessPayPalWebhookAsync("wh", payload, "tx", "time", "sig", null, null, CancellationToken.None);

        result.Processed.Should().BeTrue();
        billing.Verify(b => b.RecordPaymentAsync(subscription.Id, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PayPalBillingWebhookService CreateService(
        Mock<IBillingWebhookRepository> repository,
        IPayPalSignatureVerificationService verification,
        ISubscriptionLifecycleService lifecycle,
        ISubscriptionExternalIdService externalIdService)
    {
        return new PayPalBillingWebhookService(
            repository.Object,
            verification,
            NullLogger<PayPalBillingWebhookService>.Instance,
            lifecycle,
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            externalIdService);
    }
}