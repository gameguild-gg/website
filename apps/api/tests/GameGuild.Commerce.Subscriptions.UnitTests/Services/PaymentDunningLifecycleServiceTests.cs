using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Payment-recovery bridge into the subscription lifecycle (issue #415): retry-exhaustion
///     suspension through the standard lifecycle with the dunning reason plus final-notice
///     notification, and reason-guarded reactivation after a successful recovery payment.
/// </summary>
public class PaymentDunningLifecycleServiceTests
{
    private readonly Mock<ISubscriptionRepository> _repository = new();
    private readonly Mock<ISubscriptionLifecycleService> _lifecycle = new();
    private readonly Mock<IPublisher> _publisher = new();

    [Fact]
    public async Task SuspendForRetryExhaustion_FromActive_SuspendsWithDunningReasonAndNotifies()
    {
        var subscription = ActiveSubscription();
        SetupSubscription(subscription);
        _lifecycle
            .Setup(service => service.SuspendAsync(subscription.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        var service = CreateService();

        var suspended = await service.SuspendForRetryExhaustionAsync(subscription.Id, subscription.TenantId!.Value, "declined");

        suspended.Should().BeTrue();
        _lifecycle.Verify(
            service => service.SuspendAsync(subscription.Id, PaymentDunningReasons.RetryExhausted, It.IsAny<CancellationToken>()),
            Times.Once);
        _publisher.Verify(
            publisher => publisher.Publish(
                It.Is<SubscriptionRenewalFailedEvent>(domainEvent =>
                    domainEvent.SubscriptionId == subscription.Id
                    && domainEvent.TenantId == subscription.TenantId!.Value
                    && domainEvent.Reason == "declined"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendForRetryExhaustion_FromPastDue_SuspendsWithDunningReason()
    {
        var subscription = ActiveSubscription();
        subscription.RecordPaymentFailure("declined", SystemClock.UtcNow);
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        SetupSubscription(subscription);
        _lifecycle
            .Setup(service => service.SuspendAsync(subscription.Id, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        var service = CreateService();

        var suspended = await service.SuspendForRetryExhaustionAsync(subscription.Id, subscription.TenantId!.Value, "declined");

        suspended.Should().BeTrue();
    }

    [Fact]
    public async Task SuspendForRetryExhaustion_AlreadySuspended_IsIdempotent()
    {
        var subscription = SuspendedSubscription(PaymentDunningReasons.RetryExhausted);
        SetupSubscription(subscription);
        var service = CreateService();

        var suspended = await service.SuspendForRetryExhaustionAsync(subscription.Id, subscription.TenantId!.Value, "declined");

        suspended.Should().BeFalse();
        _lifecycle.Verify(
            service => service.SuspendAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.Publish(It.IsAny<SubscriptionRenewalFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuspendForRetryExhaustion_UnknownSubscription_DoesNothing()
    {
        _repository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);
        var service = CreateService();

        var suspended = await service.SuspendForRetryExhaustionAsync(Guid.NewGuid(), Guid.NewGuid(), "declined");

        suspended.Should().BeFalse();
    }

    [Fact]
    public async Task TryReactivateAfterRecovery_DunningSuspendedSubscription_Reactivates()
    {
        var subscription = SuspendedSubscription(PaymentDunningReasons.RetryExhausted);
        SetupSubscription(subscription);
        _lifecycle
            .Setup(service => service.ReactivateAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        var service = CreateService();

        var reactivated = await service.TryReactivateAfterRecoveryAsync(subscription.Id);

        reactivated.Should().BeTrue();
        _lifecycle.Verify(
            service => service.ReactivateAsync(subscription.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryReactivateAfterRecovery_ManuallySuspendedSubscription_IsNotReactivated()
    {
        var subscription = SuspendedSubscription("manual_hold");
        SetupSubscription(subscription);
        var service = CreateService();

        var reactivated = await service.TryReactivateAfterRecoveryAsync(subscription.Id);

        reactivated.Should().BeFalse();
        _lifecycle.Verify(
            service => service.ReactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryReactivateAfterRecovery_SuspendedWithoutReason_IsNotReactivated()
    {
        var subscription = ActiveSubscription();
        subscription.Suspend();
        SetupSubscription(subscription);
        var service = CreateService();

        var reactivated = await service.TryReactivateAfterRecoveryAsync(subscription.Id);

        reactivated.Should().BeFalse();
    }

    [Fact]
    public async Task TryReactivateAfterRecovery_ActiveSubscription_DoesNothing()
    {
        var subscription = ActiveSubscription();
        SetupSubscription(subscription);
        var service = CreateService();

        var reactivated = await service.TryReactivateAfterRecoveryAsync(subscription.Id);

        reactivated.Should().BeFalse();
        _lifecycle.Verify(
            service => service.ReactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private PaymentDunningLifecycleService CreateService() =>
        new(
            _repository.Object,
            _lifecycle.Object,
            _publisher.Object,
            NullLogger<PaymentDunningLifecycleService>.Instance);

    private void SetupSubscription(Subscription subscription) =>
        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

    private static Subscription ActiveSubscription()
    {
        var subscription = new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(29.99m, "USD"),
            startDate: SystemClock.UtcNow);
        subscription.Activate();
        return subscription;
    }

    private static Subscription SuspendedSubscription(string reason)
    {
        var subscription = ActiveSubscription();
        subscription.Suspend(reason);
        subscription.Status.Should().Be(SubscriptionStatus.Suspended);
        return subscription;
    }
}
