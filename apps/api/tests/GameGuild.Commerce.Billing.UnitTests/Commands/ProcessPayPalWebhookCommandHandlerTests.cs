using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

public class ProcessPayPalWebhookCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Result_From_Service()
    {
        var existingEvent = new BillingWebhookEvent
        {
            ExternalEventId = "tx",
            Provider = PaymentProviders.PayPal,
            IsProcessed = true,
            ProcessedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        var handler = new ProcessPayPalWebhookCommandHandler(
            CreateService(repository.Object, Mock.Of<IPayPalSignatureVerificationService>()),
            NullLogger<ProcessPayPalWebhookCommandHandler>.Instance);

        var payload = "{\"id\":\"evt_1\",\"event_type\":\"PAYMENT.SALE.COMPLETED\"}";
        var result = await handler.Handle(new ProcessPayPalWebhookCommand(
            payload,
            "tx",
            "sig",
            "time"), CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.WasAlreadyProcessed.Should().BeTrue();
        result.EventId.Should().Be("tx");
    }

    [Fact]
    public async Task Handle_Should_Handle_Invalid_Json_Payload()
    {
        var existingEvent = new BillingWebhookEvent
        {
            ExternalEventId = "tx",
            Provider = PaymentProviders.PayPal,
            IsProcessed = true,
            ProcessedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("tx", PaymentProviders.PayPal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        var handler = new ProcessPayPalWebhookCommandHandler(
            CreateService(repository.Object, Mock.Of<IPayPalSignatureVerificationService>()),
            NullLogger<ProcessPayPalWebhookCommandHandler>.Instance);

        var result = await handler.Handle(new ProcessPayPalWebhookCommand("{", "tx", "sig", "time"), CancellationToken.None);

        result.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Failed_When_Verification_Fails()
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
            .Setup(v => v.VerifySignatureAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPalVerificationResult.Failed("bad"));

        var handler = new ProcessPayPalWebhookCommandHandler(
            CreateService(repository.Object, verification.Object),
            NullLogger<ProcessPayPalWebhookCommandHandler>.Instance);

        var payload = "{\"id\":\"evt_1\",\"event_type\":\"PAYMENT.SALE.COMPLETED\"}";

        var result = await handler.Handle(new ProcessPayPalWebhookCommand(
            payload,
            "tx",
            "sig",
            "time"), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bad");
    }

    private static PayPalBillingWebhookService CreateService(
        IBillingWebhookRepository repository,
        IPayPalSignatureVerificationService verification)
    {
        return new PayPalBillingWebhookService(
            repository,
            verification,
            NullLogger<PayPalBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());
    }
}