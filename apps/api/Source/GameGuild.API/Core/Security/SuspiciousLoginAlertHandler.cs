using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Configuration;

namespace GameGuild.API.Core.Security;

/// <summary>
/// Queues redacted owner security alerts within the durable consumer's inbox transaction.
/// InApp is always queued; Email only when the user's delivery preferences allow it.
/// </summary>
internal sealed class SuspiciousLoginAlertHandler(
    IUserRepository users,
    INotificationService notifications,
    INotificationPreferenceService preferences,
    IConfiguration configuration) : IIntegrationEventHandler<SuspiciousLoginDetectedV1>
{
    private const string EnabledPath = "Authentication:SecurityNotifications:Enabled";

    public async Task HandleAsync(SuspiciousLoginDetectedV1 @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!configuration.GetValue(EnabledPath, true))
        {
            return;
        }

        var user = await users.GetByIdAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            return;
        }

        var tenantId = @event.TenantId == DurableIntegrationEventTenants.Platform ? (Guid?)null : @event.TenantId;
        var (title, message) = DescribeAlert(@event.AlertKind);
        await QueueAsync(NotificationChannel.InApp, null).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            // Email follows the user's delivery preferences; Urgent security alerts are
            // transactional and bypass mutes/quiet hours unless email is disabled upstream.
            var decision = await preferences.DecideDeliveryAsync(
                user.Id, NotificationType.Security, NotificationChannel.Email, NotificationPriority.Urgent, cancellationToken).ConfigureAwait(false);
            if (decision.Action == NotificationDeliveryAction.Send)
            {
                await QueueAsync(NotificationChannel.Email, user.Email).ConfigureAwait(false);
            }
        }

        async Task QueueAsync(NotificationChannel channel, string? recipientEmail)
        {
            var result = await notifications.SendAsync(user.Id, NotificationType.Security,
                title, message,
                channel, tenantId, priority: NotificationPriority.Urgent,
                referenceEntityId: @event.EventId, referenceEntityType: nameof(SuspiciousLoginDetectedV1),
                recipientEmail: recipientEmail, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsFailure)
            {
                throw new InvalidOperationException("The suspicious-login security alert was not durably queued.");
            }
        }
    }

    /// <summary>
    /// Redacted, kind-specific copy. Payloads and messages never include the identifier,
    /// email address, IP address, user agent, or precise location of the attempt.
    /// </summary>
    private static (string Title, string Message) DescribeAlert(string alertKind) => alertKind switch
    {
        SecurityAlertKinds.LoginStepUpRequired => ("Account security alert",
            "We detected a high-risk sign-in attempt on your account and required additional verification before allowing access. If this was not you, change your password and review your active sessions."),
        SecurityAlertKinds.BruteForceDetected => ("Account security alert",
            "We detected repeated failed sign-in attempts targeting your account. If this was not you, your credentials may be at risk: change your password and enable multi-factor authentication."),
        SecurityAlertKinds.ImpossibleTravel => ("Account security alert",
            "We detected a successful sign-in to your account from a location that cannot be reached from your previous sign-in location in the time elapsed. Please review your recent account activity and secure your account if you do not recognize it."),
        _ => ("Account security alert",
            "We detected suspicious sign-in activity on your account. Please review your recent account activity and secure your account if you do not recognize it.")
    };
}
