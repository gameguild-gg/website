using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;

namespace GameGuild.API.Integration;

/// <summary>
///     Cross-module event handler that sends a final-notice dunning email when an automatic
///     renewal fails. Typically follows one or more <see cref="SubscriptionPaymentFailedEvent"/>s.
/// </summary>
/// <remarks>
///     Resides in the API composition root to keep modules independent.
/// </remarks>
public sealed class SubscriptionRenewalFailedEmailHandler(
    ISubscriptionRepository subscriptionRepository,
    INotificationService notificationService,
    IMonthlyStatementLinkBuilder statementLinkBuilder,
    ILogger<SubscriptionRenewalFailedEmailHandler> logger
) : INotificationHandler<SubscriptionRenewalFailedEvent>
{
    public async Task Handle(SubscriptionRenewalFailedEvent notification, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository
            .GetByIdAsync(notification.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Subscription {SubscriptionId} not found while sending renewal-failed dunning email. Skipping.",
                notification.SubscriptionId);
            return;
        }

        var title = "Final notice: your subscription could not be renewed";
        var message =
            $"Your subscription renewal on {notification.FailedAt:yyyy-MM-dd} failed. " +
            $"Reason: {notification.Reason}. " +
            "If we cannot collect payment, your subscription will be suspended. " +
            "Please update your payment method now to keep your service active.";

        var result = await notificationService.SendAsync(
            recipientId: subscription.CreatedByUserId,
            type: NotificationType.Billing,
            title: title,
            message: message,
            channel: NotificationChannel.Email,
            tenantId: notification.TenantId,
            actionUrl: statementLinkBuilder.GetBillingDashboardPath(),
            priority: NotificationPriority.Urgent,
            referenceEntityId: notification.SubscriptionId,
            referenceEntityType: nameof(Subscription),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Renewal-failed dunning email queued for subscription {SubscriptionId} (recipient {RecipientId})",
                notification.SubscriptionId, subscription.CreatedByUserId);
        }
        else
        {
            logger.LogWarning(
                "Failed to queue renewal-failed dunning email for subscription {SubscriptionId}: {Error}",
                notification.SubscriptionId, result.Error?.Description);
        }
    }
}
