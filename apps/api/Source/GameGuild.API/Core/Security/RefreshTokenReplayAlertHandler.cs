using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using GameGuild.Notifications.Services;

namespace GameGuild.API.Core.Security;

/// <summary>Queues owner alerts within the durable consumer's inbox transaction.</summary>
internal sealed class RefreshTokenReplayAlertHandler(
    IUserRepository users,
    INotificationService notifications) : IIntegrationEventHandler<RefreshTokenReplayContainedV1>
{
    public async Task HandleAsync(RefreshTokenReplayContainedV1 @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var user = await users.GetByIdAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            return;
        }

        var tenantId = @event.TenantId == DurableIntegrationEventTenants.Platform ? (Guid?)null : @event.TenantId;
        await QueueAsync(NotificationChannel.InApp, null).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await QueueAsync(NotificationChannel.Email, user.Email).ConfigureAwait(false);
        }

        async Task QueueAsync(NotificationChannel channel, string? recipientEmail)
        {
            var result = await notifications.SendAsync(user.Id, NotificationType.Security,
                "Account security alert",
                "We detected reuse of a refresh token and revoked the affected authentication sessions to protect your account.",
                channel, tenantId, priority: NotificationPriority.Urgent,
                referenceEntityId: @event.EventId, referenceEntityType: nameof(RefreshTokenReplayContainedV1),
                recipientEmail: recipientEmail, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsFailure)
            {
                throw new InvalidOperationException("The refresh-token security alert was not durably queued.");
            }
        }
    }
}
