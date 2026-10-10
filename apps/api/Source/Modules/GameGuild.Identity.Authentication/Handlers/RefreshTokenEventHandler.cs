using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for logging refresh token lifecycle events
/// </summary>
public sealed class RefreshTokenEventHandler(ILogger<RefreshTokenEventHandler> logger) : INotificationHandler<RefreshTokenGeneratedEvent>,
    INotificationHandler<RefreshTokenUsedEvent>,
    INotificationHandler<RefreshTokenRevokedEvent>
{
    public async Task Handle(RefreshTokenGeneratedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Refresh token {TokenId} generated for user {UserId}, expires at {ExpiresAt}", notification.TokenId, notification.UserId, notification.ExpiresAt);

        // Here you could also:
        // - Update token tracking database
        // - Log to security audit system
        // - Track token generation metrics

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task Handle(RefreshTokenRevokedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Refresh token {TokenId} revoked for user {UserId}. Reason: {Reason} at {RevokedAt}", notification.TokenId, notification.UserId, notification.Reason, notification.RevokedAt);

        // Suspicious revocations notify the owner through the durable security pipeline:
        // replay containment publishes RefreshTokenReplayContainedV1, whose host-side
        // RefreshTokenReplayAlertHandler queues the NotificationType.Security owner alert.
        // Sign-in risk signals (step-up, brute force, impossible travel) use
        // SuspiciousLoginDetectedV1 with the host-side SuspiciousLoginAlertHandler, gated
        // by Authentication:SecurityNotifications.

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task Handle(RefreshTokenUsedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Refresh token {TokenId} used by user {UserId} at {UsedAt}", notification.TokenId, notification.UserId, notification.UsedAt);

        // Here you could also:
        // - Update last used timestamp in database
        // - Detect suspicious token reuse patterns
        // - Track token usage metrics

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
