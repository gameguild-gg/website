using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;

namespace GameGuild.API.Integration;

/// <summary>
///     Cross-module event handler that sends a cancellation-confirmation email
///     when a subscription is cancelled.
/// </summary>
/// <remarks>
///     Resides in the API composition root to keep modules independent.
/// </remarks>
public sealed class SubscriptionCancelledEmailHandler(
    ISubscriptionRepository subscriptionRepository,
    INotificationService notificationService,
    IMonthlyStatementLinkBuilder statementLinkBuilder,
    ILogger<SubscriptionCancelledEmailHandler> logger
) : INotificationHandler<SubscriptionCancelledEvent>
{
    public async Task Handle(SubscriptionCancelledEvent notification, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository
            .GetByIdAsync(notification.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Subscription {SubscriptionId} not found while sending cancellation email. Skipping.",
                notification.SubscriptionId);
            return;
        }

        var title = "Your subscription has been cancelled";
        var message =
            $"Your subscription was cancelled (reason: {notification.Reason}). " +
            "You will retain access until the end of your current billing period. " +
            "If this was a mistake, you can reactivate your subscription from the billing dashboard.";

        var result = await notificationService.SendAsync(
            recipientId: subscription.CreatedByUserId,
            type: NotificationType.Billing,
            title: title,
            message: message,
            channel: NotificationChannel.Email,
            tenantId: notification.TenantId,
            actionUrl: statementLinkBuilder.GetBillingDashboardPath(),
            priority: NotificationPriority.Normal,
            referenceEntityId: notification.SubscriptionId,
            referenceEntityType: nameof(Subscription),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Cancellation email queued for subscription {SubscriptionId} (recipient {RecipientId})",
                notification.SubscriptionId, subscription.CreatedByUserId);
        }
        else
        {
            logger.LogWarning(
                "Failed to queue cancellation email for subscription {SubscriptionId}: {Error}",
                notification.SubscriptionId, result.Error?.Description);
        }
    }
}
