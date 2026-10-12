using FluentAssertions;
using GameGuild.Commerce.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class BillingWebhookServiceTests
{
    [Fact]
    public async Task HandleSubscriptionCreatedAsync_Should_Create_Subscription_And_Set_ExternalIds()
    {
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var externalId = new Mock<ISubscriptionExternalIdService>();
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);

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

        var service = new TestBillingWebhookService(lifecycle.Object, Mock.Of<ISubscriptionQueryService>(), Mock.Of<ISubscriptionBillingService>(), externalId.Object);

        await service.HandleSubscriptionCreatedAsync(new StripeSubscriptionWebhookPayload
        {
            TenantId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            ExternalSubscriptionId = "sub",
            Amount = 10,
            StartDate = DateTime.UtcNow
        });

        externalId.Verify(e => e.SetExternalIdsAsync(subscription.Id, "sub", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Return_When_Subscription_Not_Found()
    {
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync((Subscription?)null);
        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = new TestBillingWebhookService(lifecycle.Object, query.Object, Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionExternalIdService>());

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "active"
        });

        lifecycle.Verify(l => l.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Activate_When_PendingActivation_To_Active()
    {
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);
        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = new TestBillingWebhookService(lifecycle.Object, query.Object, Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionExternalIdService>());

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "active"
        });

        lifecycle.Verify(l => l.ActivateAsync(subscription.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubscriptionCanceledAsync_Should_Cancel_Subscription()
    {
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);
        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = new TestBillingWebhookService(lifecycle.Object, query.Object, Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionExternalIdService>());

        await service.HandleSubscriptionCanceledAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub"
        });

        lifecycle.Verify(l => l.CancelAsync(subscription.Id, CancellationReason.Custom, It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentSucceededAsync_Should_Record_Payment()
    {
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);
        var billing = new Mock<ISubscriptionBillingService>();

        var service = new TestBillingWebhookService(Mock.Of<ISubscriptionLifecycleService>(), query.Object, billing.Object, Mock.Of<ISubscriptionExternalIdService>());

        await service.HandlePaymentSucceededAsync(new StripePaymentWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Amount = 10,
            Currency = "USD"
        });

        billing.Verify(b => b.RecordPaymentAsync(subscription.Id, 10, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_Should_Record_Failure()
    {
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);
        var billing = new Mock<ISubscriptionBillingService>();

        var service = new TestBillingWebhookService(Mock.Of<ISubscriptionLifecycleService>(), query.Object, billing.Object, Mock.Of<ISubscriptionExternalIdService>());

        await service.HandlePaymentFailedAsync(new StripePaymentWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            FailureReason = "fail",
            PaidAt = DateTime.UtcNow
        });

        billing.Verify(b => b.RecordPaymentFailureAsync(subscription.Id, "fail", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestBillingWebhookService(
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionExternalIdService externalIdService)
        : BillingWebhookService(
            NullLogger<BillingWebhookService>.Instance,
            lifecycleService,
            queryService,
            billingService,
            externalIdService)
    {
        protected override string ProviderName => PaymentProviders.Stripe;
    }
}