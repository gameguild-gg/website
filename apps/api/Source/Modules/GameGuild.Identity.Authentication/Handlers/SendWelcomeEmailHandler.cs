using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for user signed up notifications - records a welcome email notification row.
/// </summary>
public sealed class SendWelcomeEmailHandler(
    ILogger<SendWelcomeEmailHandler> logger,
    INotificationService notificationService,
    IUserRepository? userRepository = null)
    : INotificationHandler<UserSignedUpNotification>, IIntegrationEventHandler<UserCreatedEvent>
{
    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (userRepository is null)
            throw new InvalidOperationException("User repository is required for durable welcome-email delivery.");

        var user = await userRepository.GetByIdAsync(@event.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", @event.UserId);
        await Handle(new UserSignedUpNotification
        {
            UserId = user.Id,
            Email = user.Email,
            Username = user.Username ?? user.Email,
            TenantId = @event.TenantId == DurableIntegrationEventTenants.Platform ? null : @event.TenantId
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task Handle(UserSignedUpNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var displayName = string.IsNullOrWhiteSpace(notification.Username) ? notification.Email : notification.Username;
            var metadata = JsonSerializer.Serialize(new
            {
                userName = notification.Username,
                displayName,
                email = notification.Email
            });

            await notificationService.SendAsync(
                notification.UserId,
                NotificationType.Onboarding,
                "Welcome to GameGuild",
                "Your account is ready to use.",
                NotificationChannel.Email,
                notification.TenantId,
                metadata: metadata,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Welcome email queued for {Email} (ID: {UserId})", notification.Email, notification.UserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Welcome email queueing failed for user {Email} (ID: {UserId})", notification.Email, notification.UserId);
        }
    }
}
