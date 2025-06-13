using MediatR;
using GameGuild.Modules.Auth.Notifications;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for user signed up notifications - logs analytics event
/// </summary>
public class LogAnalyticsEventHandler : INotificationHandler<UserSignedUpNotification>
{
    private readonly ILogger<LogAnalyticsEventHandler> _logger;

    public LogAnalyticsEventHandler(ILogger<LogAnalyticsEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(UserSignedUpNotification notification, CancellationToken cancellationToken)
    {
        // In a real application, you would send this to an analytics service
        _logger.LogInformation("Analytics: User sign-up event - UserId: {UserId}, Email: {Email}, Time: {SignUpTime}", 
            notification.UserId, notification.Email, notification.SignUpTime);

        // Simulate analytics API call
        await Task.Delay(50, cancellationToken);

        _logger.LogInformation("Analytics event logged for user {Email}", notification.Email);
    }
}
