using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles one-time email sign-in code delivery by recording a notification row.
/// </summary>
public sealed class SendEmailCodeRequestedHandler(
    ILogger<SendEmailCodeRequestedHandler> logger,
    INotificationService notificationService,
    IUserRepository userRepository) : INotificationHandler<EmailCodeRequestedNotification>
{
    public async Task Handle(EmailCodeRequestedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userRepository.GetByEmailAsync(notification.Email, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                logger.LogWarning("Email-code requested for unknown email {Email}", LogRedaction.MaskEmail(notification.Email));
                return;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                code = notification.Code,
                email = notification.Email,
                userName = notification.UserName
            });

            var result = await notificationService.SendAsync(
                user.Id,
                NotificationType.EmailCode,
                "Your GameGuild sign-in code",
                "Sign in with this one-time code.",
                NotificationChannel.Email,
                notification.TenantId,
                metadata: metadata,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null || result.IsFailure)
            {
                throw new InvalidOperationException("Authentication notification was not durably queued.");
            }

            logger.LogInformation("Email-code email queued for {Email}", LogRedaction.MaskEmail(notification.Email));
        }
        catch (Exception ex)
        {
            logger.LogError("Error queueing email-code email to {Email}: {ErrorType}",
                LogRedaction.MaskEmail(notification.Email), ex.GetType().Name);
            throw; // Queue persistence failed; do not acknowledge a lost notification.
        }
    }
}
