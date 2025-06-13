using MediatR;
using GameGuild.Modules.Auth.Notifications;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for user signed up notifications - sends welcome email
/// </summary>
public class SendWelcomeEmailHandler : INotificationHandler<UserSignedUpNotification>
{
    private readonly ILogger<SendWelcomeEmailHandler> _logger;

    public SendWelcomeEmailHandler(ILogger<SendWelcomeEmailHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(UserSignedUpNotification notification, CancellationToken cancellationToken)
    {
        // In a real application, you would send an actual email
        _logger.LogInformation("Sending welcome email to user {Email} (ID: {UserId})", 
            notification.Email, notification.UserId);

        // Simulate email sending delay
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("Welcome email sent to {Email}", notification.Email);
    }
}
