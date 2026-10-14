using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles push MFA approval-challenge delivery by recording a durable push notification row
///     (issue #259). The notification sweep delivers it through the configured push provider
///     (FCM/APNs); the device answers through the challenge-respond endpoint.
/// </summary>
public sealed class SendPushMfaChallengeHandler(
    ILogger<SendPushMfaChallengeHandler> logger,
    INotificationService notificationService,
    IUserRepository userRepository) : INotificationHandler<PushMfaChallengeNotification>
{
    public async Task Handle(PushMfaChallengeNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(notification.UserId, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                logger.LogWarning("Push MFA challenge requested for an unknown user {UserId}", LogRedaction.RedactId(notification.UserId, "uid"));
                return;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                deviceToken = notification.DeviceToken,
                data = new Dictionary<string, string>
                {
                    ["challengeId"] = notification.ChallengeId.ToString("N"),
                    ["kind"] = "mfa-challenge"
                }
            });

            var result = await notificationService.SendAsync(
                user.Id,
                NotificationType.PushMfaChallenge,
                "GameGuild sign-in approval",
                "Approve this sign-in request, or deny it if this wasn't you.",
                NotificationChannel.Push,
                tenantId: null,
                priority: NotificationPriority.Urgent,
                metadata: metadata,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null || result.IsFailure)
            {
                throw new InvalidOperationException("Authentication notification was not durably queued.");
            }

            logger.LogInformation("Push MFA challenge queued for user {UserId}",
                LogRedaction.RedactId(user.Id, "uid"));
        }
        catch (Exception ex)
        {
            logger.LogError("Error queueing push MFA challenge for user {UserId}: {ErrorType}",
                LogRedaction.RedactId(notification.UserId, "uid"), ex.GetType().FullName);
            throw; // Queue persistence failed; do not acknowledge a lost notification.
        }
    }
}
