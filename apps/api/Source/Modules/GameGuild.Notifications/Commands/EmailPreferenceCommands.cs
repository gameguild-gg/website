using GameGuild.CQRS;
using GameGuild.Notifications.Services;
using GameGuild.Notifications.Services.Email;

namespace GameGuild.Notifications;

public sealed record SetMutedNotificationTypesCommand(Guid UserId, IReadOnlyList<string> TypeNames)
    : ICommand<Result<NotificationPreference>>;
public sealed record SetNotificationDigestFrequencyCommand(Guid UserId, DigestFrequency? Frequency)
    : ICommand<Result<NotificationPreference>>;
public sealed record ReleaseEmailSuppressionCommand(string Email) : ICommand<Result<bool>>;
public sealed record RequeueEmailNotificationCommand(Guid NotificationId) : ICommand<Result<Notification>>;

public sealed class EmailPreferenceCommandHandler(INotificationPreferenceService preferences) :
    ICommandHandler<SetMutedNotificationTypesCommand, Result<NotificationPreference>>,
    ICommandHandler<SetNotificationDigestFrequencyCommand, Result<NotificationPreference>>
{
    public Task<Result<NotificationPreference>> Handle(SetMutedNotificationTypesCommand command, CancellationToken ct)
        => preferences.SetMutedTypesAsync(command.UserId, command.TypeNames, ct);

    public Task<Result<NotificationPreference>> Handle(SetNotificationDigestFrequencyCommand command, CancellationToken ct)
        => preferences.SetEmailDigestFrequencyAsync(command.UserId, command.Frequency, ct);
}

public sealed class EmailDeliveryAdminCommandHandler(IEmailDeliveryAdminService admin) :
    ICommandHandler<ReleaseEmailSuppressionCommand, Result<bool>>,
    ICommandHandler<RequeueEmailNotificationCommand, Result<Notification>>
{
    public Task<Result<bool>> Handle(ReleaseEmailSuppressionCommand command, CancellationToken ct)
        => admin.ReleaseSuppressionAsync(command.Email, ct);

    public Task<Result<Notification>> Handle(RequeueEmailNotificationCommand command, CancellationToken ct)
        => admin.RequeueAsync(command.NotificationId, ct);
}
