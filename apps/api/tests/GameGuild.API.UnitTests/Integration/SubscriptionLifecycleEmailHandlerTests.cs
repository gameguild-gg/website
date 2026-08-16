using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using GameGuild.API.Integration;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;

namespace GameGuild.API.UnitTests.Integration;

public sealed class SubscriptionLifecycleEmailHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IMonthlyStatementLinkBuilder> _linkBuilder = new();

    public SubscriptionLifecycleEmailHandlerTests()
    {
        _linkBuilder.Setup(builder => builder.GetBillingDashboardPath()).Returns("/billing");
    }

    [Theory]
    [InlineData(LifecycleEvent.Cancelled)]
    [InlineData(LifecycleEvent.PaymentFailed)]
    [InlineData(LifecycleEvent.RenewalFailed)]
    public async Task Handle_WhenSubscriptionDoesNotExist_DoesNotSend(LifecycleEvent eventType)
    {
        var subscriptionId = Guid.NewGuid();
        _subscriptionRepository
            .Setup(repository => repository.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        await HandleAsync(eventType, subscriptionId, Guid.NewGuid());

        _notificationService.Verify(service => service.SendAsync(
            It.IsAny<Guid>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<NotificationChannel>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(LifecycleEvent.Cancelled, "Your subscription has been cancelled", NotificationPriority.Normal, "UserRequested")]
    [InlineData(LifecycleEvent.PaymentFailed, "Action required: your payment failed", NotificationPriority.High, "card declined")]
    [InlineData(LifecycleEvent.RenewalFailed, "Final notice: your subscription could not be renewed", NotificationPriority.Urgent, "bank rejected")]
    public async Task Handle_WhenNotificationSucceeds_SendsExpectedBillingMessage(
        LifecycleEvent eventType,
        string expectedTitle,
        NotificationPriority expectedPriority,
        string expectedMessagePart)
    {
        var subscriptionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var subscription = CreateSubscription(tenantId);
        ConfigureSubscription(subscriptionId, subscription);
        ConfigureNotification(Result.Success(Notification.Create(
            subscription.CreatedByUserId,
            NotificationType.Billing,
            NotificationChannel.Email,
            expectedTitle,
            expectedMessagePart,
            tenantId)));

        await HandleAsync(eventType, subscriptionId, tenantId);

        _notificationService.Verify(service => service.SendAsync(
            subscription.CreatedByUserId,
            NotificationType.Billing,
            expectedTitle,
            It.Is<string>(message => message.Contains(expectedMessagePart, StringComparison.Ordinal)),
            NotificationChannel.Email,
            tenantId,
            "/billing",
            expectedPriority,
            subscriptionId,
            nameof(Subscription),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(LifecycleEvent.Cancelled)]
    [InlineData(LifecycleEvent.PaymentFailed)]
    [InlineData(LifecycleEvent.RenewalFailed)]
    public async Task Handle_WhenNotificationFails_CompletesWithoutThrowing(LifecycleEvent eventType)
    {
        var subscriptionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        ConfigureSubscription(subscriptionId, CreateSubscription(tenantId));
        ConfigureNotification(Result.Failure<Notification>(
            Error.Failure("notification.failed", "Delivery failed.")));

        var action = () => HandleAsync(eventType, subscriptionId, tenantId);

        await action.Should().NotThrowAsync();
        _notificationService.Verify(service => service.SendAsync(
            It.IsAny<Guid>(),
            NotificationType.Billing,
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationChannel.Email,
            tenantId,
            "/billing",
            It.IsAny<NotificationPriority>(),
            subscriptionId,
            nameof(Subscription),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ConfigureSubscription(Guid subscriptionId, Subscription subscription)
    {
        _subscriptionRepository
            .Setup(repository => repository.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
    }

    private void ConfigureNotification(Result<Notification> result)
    {
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private async Task HandleAsync(LifecycleEvent eventType, Guid subscriptionId, Guid tenantId)
    {
        switch (eventType)
        {
            case LifecycleEvent.Cancelled:
                await new SubscriptionCancelledEmailHandler(
                        _subscriptionRepository.Object,
                        _notificationService.Object,
                        _linkBuilder.Object,
                        Mock.Of<ILogger<SubscriptionCancelledEmailHandler>>())
                    .Handle(
                        new SubscriptionCancelledEvent(
                            subscriptionId,
                            tenantId,
                            CancellationReason.UserRequested,
                            SubscriptionStatus.Active),
                        CancellationToken.None);
                break;
            case LifecycleEvent.PaymentFailed:
                await new SubscriptionPaymentFailedEmailHandler(
                        _subscriptionRepository.Object,
                        _notificationService.Object,
                        _linkBuilder.Object,
                        Mock.Of<ILogger<SubscriptionPaymentFailedEmailHandler>>())
                    .Handle(
                        new SubscriptionPaymentFailedEvent(
                            subscriptionId,
                            tenantId,
                            "card declined",
                            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)),
                        CancellationToken.None);
                break;
            case LifecycleEvent.RenewalFailed:
                await new SubscriptionRenewalFailedEmailHandler(
                        _subscriptionRepository.Object,
                        _notificationService.Object,
                        _linkBuilder.Object,
                        Mock.Of<ILogger<SubscriptionRenewalFailedEmailHandler>>())
                    .Handle(
                        new SubscriptionRenewalFailedEvent(
                            subscriptionId,
                            tenantId,
                            "bank rejected",
                            new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)),
                        CancellationToken.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null);
        }
    }

    private static Subscription CreateSubscription(Guid tenantId)
    {
        return new Subscription(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            BillingCycle.Monthly,
            new Money(49m),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    public enum LifecycleEvent
    {
        Cancelled,
        PaymentFailed,
        RenewalFailed
    }
}
