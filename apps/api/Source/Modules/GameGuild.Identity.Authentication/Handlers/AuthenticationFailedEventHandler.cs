using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for logging authentication failure events and performing security actions
/// </summary>
public sealed class AuthenticationFailedEventHandler(ILogger<AuthenticationFailedEventHandler> logger) : INotificationHandler<AuthenticationFailedEvent>
{
    public async Task Handle(AuthenticationFailedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Authentication failed for identifier {Identifier}. Reason: {Reason}. IP: {IpAddress}, User Agent: {UserAgent}, Time: {Timestamp}",
            notification.Identifier,
            notification.Reason,
            notification.IpAddress ?? "Unknown",
            notification.UserAgent ?? "Unknown",
            notification.Timestamp
        );

        // Security escalation for failed authentication lives in the sign-in pipeline:
        // risk analysis (LoginAttemptAnalysisService) records the attempt, forwards signals to
        // the SIEM, and — on confirmed brute force against a known account — publishes a
        // SuspiciousLoginDetectedV1 durable event. The host-side SuspiciousLoginAlertHandler
        // consumes it and queues the NotificationType.Security owner alert (InApp always,
        // Email per preferences), gated by Authentication:SecurityNotifications.

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
