using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles password reset email delivery by recording a notification row.
/// </summary>
public sealed class SendPasswordResetRequestedHandler(
    ILogger<SendPasswordResetRequestedHandler> logger,
    INotificationService notificationService,
    IUserRepository userRepository) : INotificationHandler<PasswordResetRequestedNotification>
{
    public async Task Handle(PasswordResetRequestedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByEmailAsync(notification.Email, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                logger.LogWarning("Password reset email requested for unknown email {Email}", notification.Email);
                return;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                token = notification.Token,
                email = notification.Email,
                userName = notification.UserName
            });

            var result = await notificationService.SendAsync(
                user.Id,
                NotificationType.PasswordReset,
                "Reset your GameGuild password",
                "Reset your password.",
                NotificationChannel.Email,
                metadata: metadata,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null || result.IsFailure)
                throw new InvalidOperationException("Authentication notification was not durably queued.");

            logger.LogInformation("Password reset email queued for {Email}", notification.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing password reset email to {Email}", notification.Email);
            throw; // Queue persistence failed; do not acknowledge a lost notification.
        }
    }
}
