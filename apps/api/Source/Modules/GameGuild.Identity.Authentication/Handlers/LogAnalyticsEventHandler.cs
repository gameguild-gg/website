using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for user signed up notifications - logs analytics event.
///     PLANNED: Inject IAnalyticsService (Mixpanel, Segment, etc.) when implemented.
/// </summary>
public sealed class LogAnalyticsEventHandler(ILogger<LogAnalyticsEventHandler> logger)
    : INotificationHandler<UserSignedUpNotification>, IIntegrationEventHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Analytics: durable user sign-up event - UserId: {UserId}, TenantId: {TenantId}",
            @event.UserId,
            @event.TenantId);
        return Task.CompletedTask;
    }

    public Task Handle(UserSignedUpNotification notification, CancellationToken cancellationToken)
    {
        // PLANNED: Replace with actual analytics service call:
        // await _analyticsService.TrackEventAsync("user_signed_up", new {
        //     user_id = notification.UserId,
        //     email = notification.Email,
        //     username = notification.Username,
        //     tenant_id = notification.TenantId
        // });
        logger.LogInformation(
            "Analytics: User sign-up event - UserId: {UserId}, Email: {Email}, Username: {Username}, TenantId: {TenantId}",
            notification.UserId,
            notification.Email,
            notification.Username,
            notification.TenantId
        );

        return Task.CompletedTask;
    }
}
