using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Integration;

/// <summary>
///     Cross-module event handler that sends a final-notice dunning email when an automatic
///     renewal fails. Typically follows one or more <see cref="SubscriptionPaymentFailedEvent"/>s.
///     The message content and priority are driven by the configurable dunning escalation
///     ladder (<c>Payments:Dunning</c>, issue #403); the default ladder reproduces the legacy
///     hardcoded final notice.
/// </summary>
/// <remarks>
///     Resides in the API composition root to keep modules independent.
/// </remarks>
public sealed class SubscriptionRenewalFailedEmailHandler(
    ISubscriptionRepository subscriptionRepository,
    INotificationService notificationService,
    IMonthlyStatementLinkBuilder statementLinkBuilder,
    IOptions<PaymentDunningOptions> dunningOptions,
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

        var step = PaymentDunningLadder.Resolve(
            dunningOptions.Value.EscalationLadder,
            DunningStages.FinalNotice,
            notification.FailedAt,
            SystemClock.UtcNow);
        var title = step.Title;
        var message = PaymentDunningLadder.Interpolate(step.Message, notification.FailedAt, notification.Reason);
        var priority = PaymentDunningLadder.ParsePriority(step.Priority, NotificationPriority.Urgent);

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
                "Renewal-failed dunning email ({Template}) queued for subscription {SubscriptionId} (recipient {RecipientId})",
                step.Template, notification.SubscriptionId, subscription.CreatedByUserId);
        }
        else
        {
            logger.LogWarning(
                "Failed to queue renewal-failed dunning email ({Template}) for subscription {SubscriptionId}: {Error}",
                step.Template, notification.SubscriptionId, result.Error?.Description);
        }
    }
}
