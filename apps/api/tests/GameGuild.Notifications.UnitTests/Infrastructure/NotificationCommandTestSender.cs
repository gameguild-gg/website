using GameGuild.CQRS;

namespace GameGuild.Notifications.UnitTests.Infrastructure;

internal sealed class NotificationCommandTestSender(
    NotificationMutationCommandHandler? notificationHandler = null,
    EmailDeliveryMutationCommandHandler? emailDeliveryHandler = null) : ISender
{
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        object response = request switch
        {
            MarkNotificationReadCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            MarkAllNotificationsReadCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            MarkNotificationUnreadCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            DeleteNotificationCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            DeleteReadNotificationsCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            UpdateNotificationPreferencesCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            SetNotificationQuietHoursCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            SetNotificationMutedTypesCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            SetNotificationDigestFrequencyCommand command when notificationHandler is not null =>
                await notificationHandler.Handle(command, cancellationToken),
            ReleaseEmailSuppressionCommand command when emailDeliveryHandler is not null =>
                await emailDeliveryHandler.Handle(command, cancellationToken),
            RequeueEmailNotificationCommand command when emailDeliveryHandler is not null =>
                await emailDeliveryHandler.Handle(command, cancellationToken),
            IngestEmailDeliveryEventCommand command when emailDeliveryHandler is not null =>
                await emailDeliveryHandler.Handle(command, cancellationToken),
            _ => throw new InvalidOperationException($"No test handler is configured for {request.GetType().Name}.")
        };

        return (TResponse)response;
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        return request switch
        {
            MarkNotificationReadCommand command => await Send(command, cancellationToken),
            MarkAllNotificationsReadCommand command => await Send(command, cancellationToken),
            MarkNotificationUnreadCommand command => await Send(command, cancellationToken),
            DeleteNotificationCommand command => await Send(command, cancellationToken),
            DeleteReadNotificationsCommand command => await Send(command, cancellationToken),
            UpdateNotificationPreferencesCommand command => await Send(command, cancellationToken),
            SetNotificationQuietHoursCommand command => await Send(command, cancellationToken),
            SetNotificationMutedTypesCommand command => await Send(command, cancellationToken),
            SetNotificationDigestFrequencyCommand command => await Send(command, cancellationToken),
            ReleaseEmailSuppressionCommand command => await Send(command, cancellationToken),
            RequeueEmailNotificationCommand command => await Send(command, cancellationToken),
            IngestEmailDeliveryEventCommand command => await Send(command, cancellationToken),
            _ => throw new InvalidOperationException($"No test handler is configured for {request.GetType().Name}.")
        };
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        await Send((object)request, cancellationToken);
    }
}
