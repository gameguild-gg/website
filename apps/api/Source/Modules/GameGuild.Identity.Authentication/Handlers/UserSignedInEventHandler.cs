using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for logging user sign-in events and performing post-login actions
/// </summary>
public sealed class UserSignedInEventHandler(ILogger<UserSignedInEventHandler> logger) : INotificationHandler<UserSignedInEvent>
{
    public async Task Handle(UserSignedInEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "User {UserId} ({Email}) signed in via {AuthMethod} from {IpAddress} at {Timestamp}",
            LogRedaction.RedactId(notification.UserId, "uid"),
            LogRedaction.MaskEmail(notification.Email),
            LogRedaction.Sanitize(notification.AuthMethod),
            notification.IpAddress is null ? "Unknown" : LogRedaction.RedactId(notification.IpAddress, "ip"),
            notification.Timestamp
        );

        // Here you could also:
        // - Update user last login timestamp
        // - Track authentication metrics
        // - Send notifications for unusual login patterns
        // - Update session tracking
        // - Detect anomalies in login location/device

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
