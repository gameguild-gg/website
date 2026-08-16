using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;

namespace GameGuild.API.Integration;

/// <summary>
///     Cross-module event handler that sends a dunning email on payment failure.
///     Economic invariant: Failed payment → Customer is informed and given a chance to remediate.
/// </summary>
/// <remarks>
///     Sends an Email-channel Billing notification to the subscription owner.
///     Resides in the API composition root to keep Commerce.Subscriptions and Notifications modules independent.
/// </remarks>
public sealed class SubscriptionPaymentFailedEmailHandler(
    ISubscriptionRepository subscriptionRepository,
    INotificationService notificationService,
    IMonthlyStatementLinkBuilder statementLinkBuilder,
    ILogger<SubscriptionPaymentFailedEmailHandler> logger
) : INotificationHandler<SubscriptionPaymentFailedEvent>
{
    public async Task Handle(SubscriptionPaymentFailedEvent notification, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository
            .GetByIdAsync(notification.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Subscription {SubscriptionId} not found while sending payment-failed dunning email. Skipping.",
                notification.SubscriptionId);
            return;
        }

        var title = "Action required: your payment failed";
        var message =
            $"We were unable to process your most recent subscription payment on " +
            $"{notification.FailureDate:yyyy-MM-dd}. Reason: {notification.Reason}. " +
            "Please update your payment method to avoid service interruption.";

        var result = await notificationService.SendAsync(
            recipientId: subscription.CreatedByUserId,
            type: NotificationType.Billing,
            title: title,
            message: message,
            channel: NotificationChannel.Email,
            tenantId: notification.TenantId,
            actionUrl: statementLinkBuilder.GetBillingDashboardPath(),
            priority: NotificationPriority.High,
            referenceEntityId: notification.SubscriptionId,
            referenceEntityType: nameof(Subscription),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Dunning email queued for subscription {SubscriptionId} (recipient {RecipientId})",
                notification.SubscriptionId, subscription.CreatedByUserId);
        }
        else
        {
            logger.LogWarning(
                "Failed to queue dunning email for subscription {SubscriptionId}: {Error}",
                notification.SubscriptionId, result.Error?.Description);
        }
    }
}
