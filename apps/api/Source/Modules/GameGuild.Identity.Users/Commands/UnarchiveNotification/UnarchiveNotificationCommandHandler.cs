using GameGuild.CQRS;

namespace GameGuild.Identity.Users;

/// <summary>
///     Command handler for unarchiving a notification
/// </summary>
public sealed class UnarchiveNotificationCommandHandler(IUserRepository userRepository, IUserNotificationRepository notificationRepository)
    : ICommandHandler<UnarchiveNotificationCommand>
{
    public async Task<Unit> Handle(UnarchiveNotificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new UserNotFoundException(request.UserId);
        }

        var notification = await notificationRepository.GetByIdAsync(request.NotificationId, cancellationToken).ConfigureAwait(false);
        if (notification == null || notification.UserId != request.UserId)
        {
            throw new InvalidOperationException("Notification not found or does not belong to user");
        }

        notification.Unarchive();
        await notificationRepository.UpdateAsync(notification, cancellationToken).ConfigureAwait(false);
        await notificationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
