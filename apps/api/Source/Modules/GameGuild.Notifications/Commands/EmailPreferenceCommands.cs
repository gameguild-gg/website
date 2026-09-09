using GameGuild.CQRS;
using GameGuild.Notifications.Services;

namespace GameGuild.Notifications;

public sealed record SetMutedNotificationTypesCommand(Guid UserId, IReadOnlyList<string> TypeNames)
    : ICommand<Result<NotificationPreference>>;
public sealed record SetNotificationDigestFrequencyCommand(Guid UserId, DigestFrequency? Frequency)
    : ICommand<Result<NotificationPreference>>;

public sealed class EmailPreferenceCommandHandler(INotificationPreferenceService preferences) :
    ICommandHandler<SetMutedNotificationTypesCommand, Result<NotificationPreference>>,
    ICommandHandler<SetNotificationDigestFrequencyCommand, Result<NotificationPreference>>
{
    public Task<Result<NotificationPreference>> Handle(SetMutedNotificationTypesCommand command, CancellationToken ct)
        => preferences.SetMutedTypesAsync(command.UserId, command.TypeNames, ct);

    public Task<Result<NotificationPreference>> Handle(SetNotificationDigestFrequencyCommand command, CancellationToken ct)
        => preferences.SetEmailDigestFrequencyAsync(command.UserId, command.Frequency, ct);
}
