using FluentAssertions;
using GameGuild.Commerce.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class BillingWebhookServiceStatusTransitionTests
{
    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Suspend_When_Unpaid()
    {
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "unpaid"
        });

        lifecycle.Verify(l => l.SuspendAsync(subscription.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Cancel_When_Cancelled()
    {
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "canceled"
        });

        lifecycle.Verify(l => l.CancelAsync(subscription.Id, CancellationReason.Custom, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Start_Trial_When_PendingActivation()
    {
        var subscription = CreateSubscription(SubscriptionStatus.PendingActivation);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "trialing"
        });

        lifecycle.Verify(l => l.StartTrialAsync(subscription.Id, 14, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Activate_When_Suspended_To_Active()
    {
        var subscription = CreateSubscription(SubscriptionStatus.Suspended);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "active"
        });

        lifecycle.Verify(l => l.ActivateAsync(subscription.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Do_Nothing_On_Unknown_Status()
    {
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "mystery"
        });

        lifecycle.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("past_due")]
    [InlineData("incomplete_expired")]
    public async Task HandleSubscriptionUpdatedAsync_Should_Handle_Other_Statuses(string status)
    {
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = status
        });

        lifecycle.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleSubscriptionUpdatedAsync_Should_Suspend_When_Paused()
    {
        var subscription = CreateSubscription(SubscriptionStatus.Active);
        var query = new Mock<ISubscriptionQueryService>();
        query.Setup(q => q.GetByExternalIdAsync("sub", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var service = CreateService(lifecycle.Object, query.Object);

        await service.HandleSubscriptionUpdatedAsync(new StripeSubscriptionWebhookPayload
        {
            ExternalSubscriptionId = "sub",
            Status = "paused"
        });

        lifecycle.Verify(l => l.SuspendAsync(subscription.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Subscription CreateSubscription(SubscriptionStatus status)
    {
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var statusProperty = typeof(Subscription).GetProperty("Status", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        statusProperty!.SetValue(subscription, status);
        return subscription;
    }

    private static BillingWebhookService CreateService(ISubscriptionLifecycleService lifecycle, ISubscriptionQueryService query)
    {
        return new TestBillingWebhookService(lifecycle, query, Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionExternalIdService>());
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