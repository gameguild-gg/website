using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles verification email delivery by recording a notification row.
/// </summary>
public sealed class SendEmailVerificationRequestedHandler(
    ILogger<SendEmailVerificationRequestedHandler> logger,
    INotificationService notificationService,
    IUserRepository userRepository) : INotificationHandler<EmailVerificationRequestedNotification>
{
    public async Task Handle(EmailVerificationRequestedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByEmailAsync(notification.Email, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                logger.LogWarning("Verification email requested for unknown email {Email}", notification.Email);
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
                NotificationType.EmailVerification,
                "Verify your GameGuild email address",
                "Please verify your email address.",
                NotificationChannel.Email,
                metadata: metadata,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null || result.IsFailure)
                throw new InvalidOperationException("Authentication notification was not durably queued.");

            logger.LogInformation("Verification email queued for {Email}", notification.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing verification email to {Email}", notification.Email);
            throw; // Queue persistence failed; do not acknowledge a lost notification.
        }
    }
}
