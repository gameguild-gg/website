using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

public class ProcessGooglePayWebhookCommandHandlerTests
{
    [Fact]
    public async Task Handle_Delegates_To_The_GooglePay_Webhook_Service()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt-1", PaymentProviders.GooglePay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent { ExternalEventId = "evt-1", ProcessedAt = DateTime.UtcNow });

        var verification = new Mock<IGooglePayWebhookVerificationService>();
        verification
            .Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new GooglePayWebhookVerificationResult
            {
                IsValid = true,
                EventId = "evt-1",
                EventType = "SUBSCRIPTION_PURCHASED"
            });

        var service = new GooglePayBillingWebhookService(
            repository.Object,
            verification.Object,
            NullLogger<GooglePayBillingWebhookService>.Instance,
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionLifecycleService>(),
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionQueryService>(),
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionBillingService>(),
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionExternalIdService>());

        var handler = new ProcessGooglePayWebhookCommandHandler(service, NullLogger<ProcessGooglePayWebhookCommandHandler>.Instance);
        var command = new ProcessGooglePayWebhookCommand("{}", "Bearer token", "project");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.WasAlreadyProcessed.Should().BeTrue();
        result.EventId.Should().Be("evt-1");
    }

    [Fact]
    public async Task Handle_Propagates_Verification_Failures()
    {
        var verification = new Mock<IGooglePayWebhookVerificationService>();
        verification
            .Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(GooglePayWebhookVerificationResult.Failure("Bearer JWT signature verification failed."));

        var service = new GooglePayBillingWebhookService(
            Mock.Of<IBillingWebhookRepository>(),
            verification.Object,
            NullLogger<GooglePayBillingWebhookService>.Instance,
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionLifecycleService>(),
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionQueryService>(),
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionBillingService>(),
            Mock.Of<GameGuild.Commerce.Subscriptions.ISubscriptionExternalIdService>());

        var handler = new ProcessGooglePayWebhookCommandHandler(service, NullLogger<ProcessGooglePayWebhookCommandHandler>.Instance);
        var command = new ProcessGooglePayWebhookCommand("{}", "Bearer bad", "project");

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidWebhookSignatureException>();
    }
}
