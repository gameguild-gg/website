using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles one-time SMS MFA code delivery by recording a durable SMS notification row
///     (issue #244). The notification sweep delivers it through the configured SMS provider.
/// </summary>
public sealed class SendSmsMfaCodeRequestedHandler(
    ILogger<SendSmsMfaCodeRequestedHandler> logger,
    INotificationService notificationService,
    IUserRepository userRepository) : INotificationHandler<SmsMfaCodeRequestedNotification>
{
    public async Task Handle(SmsMfaCodeRequestedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(notification.UserId, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                logger.LogWarning("SMS MFA code requested for an unknown user {UserId}", LogRedaction.RedactId(notification.UserId, "uid"));
                return;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                code = notification.Code,
                phoneNumber = notification.PhoneNumber
            });

            var result = await notificationService.SendAsync(
                user.Id,
                NotificationType.SmsMfaCode,
                "GameGuild verification code",
                $"Your GameGuild verification code is {notification.Code}. It expires in 10 minutes. Never share this code.",
                NotificationChannel.Sms,
                tenantId: null,
                priority: NotificationPriority.High,
                metadata: metadata,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null || result.IsFailure)
            {
                throw new InvalidOperationException("Authentication notification was not durably queued.");
            }

            logger.LogInformation("SMS MFA code queued for user {UserId}",
                LogRedaction.RedactId(user.Id, "uid"));
        }
        catch (Exception ex)
        {
            logger.LogError("Error queueing SMS MFA code for user {UserId}: {ErrorType}",
                LogRedaction.RedactId(notification.UserId, "uid"), ex.GetType().FullName);
            throw; // Queue persistence failed; do not acknowledge a lost notification.
        }
    }
}
