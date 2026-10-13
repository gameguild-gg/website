using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Integration;

/// <summary>
///     Cross-module event handler that sends a dunning email on payment failure.
///     Economic invariant: Failed payment → Customer is informed and given a chance to remediate.
///     The message content and priority are driven by the configurable dunning escalation
///     ladder (<c>Payments:Dunning</c>, issue #403); the default ladder reproduces the legacy
///     hardcoded reminder.
/// </summary>
/// <remarks>
///     Sends an Email-channel Billing notification to the subscription owner.
///     Resides in the API composition root to keep Commerce.Subscriptions and Notifications modules independent.
/// </remarks>
public sealed class SubscriptionPaymentFailedEmailHandler(
    ISubscriptionRepository subscriptionRepository,
    INotificationService notificationService,
    IMonthlyStatementLinkBuilder statementLinkBuilder,
    IOptions<PaymentDunningOptions> dunningOptions,
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

        var step = PaymentDunningLadder.Resolve(
            dunningOptions.Value.EscalationLadder,
            DunningStages.PaymentFailed,
            notification.FailureDate,
            SystemClock.UtcNow);
        var title = step.Title;
        var message = PaymentDunningLadder.Interpolate(step.Message, notification.FailureDate, notification.Reason);
        var priority = PaymentDunningLadder.ParsePriority(step.Priority, NotificationPriority.High);

        var result = await notificationService.SendAsync(
            recipientId: subscription.CreatedByUserId,
            type: NotificationType.Billing,
            title: title,
            message: message,
            channel: NotificationChannel.Email,
            tenantId: notification.TenantId,
            actionUrl: statementLinkBuilder.GetBillingDashboardPath(),
            priority: priority,
            referenceEntityId: notification.SubscriptionId,
            referenceEntityType: nameof(Subscription),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Dunning email ({Template}) queued for subscription {SubscriptionId} (recipient {RecipientId})",
                step.Template, notification.SubscriptionId, subscription.CreatedByUserId);
        }
        else
        {
            logger.LogWarning(
                "Failed to queue dunning email ({Template}) for subscription {SubscriptionId}: {Error}",
                step.Template, notification.SubscriptionId, result.Error?.Description);
        }
    }
}
