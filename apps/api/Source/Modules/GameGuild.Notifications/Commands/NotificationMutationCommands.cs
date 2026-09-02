using GameGuild.CQRS;
using GameGuild.Notifications.Services;

namespace GameGuild.Notifications;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : ICommand<Result>;
public sealed record MarkAllNotificationsReadCommand(Guid UserId) : ICommand<Result>;
public sealed record MarkNotificationUnreadCommand(Guid NotificationId) : ICommand<Result>;
public sealed record DeleteNotificationCommand(Guid NotificationId) : ICommand<Result>;
public sealed record DeleteReadNotificationsCommand(Guid UserId) : ICommand<Result<int>>;
public sealed record UpdateNotificationPreferencesCommand(
    Guid UserId,
    bool? EmailEnabled,
    bool? PushEnabled,
    bool? InAppEnabled,
    bool? SmsEnabled,
    bool? MarketingEnabled,
    bool? SocialEnabled,
    bool? LearningEnabled,
    bool? AchievementsEnabled) : ICommand<Result<NotificationPreference>>;
public sealed record SetNotificationQuietHoursCommand(
    Guid UserId,
    TimeOnly? Start,
    TimeOnly? End,
    string? Timezone) : ICommand<Result>;
public sealed record SetNotificationMutedTypesCommand(
    Guid UserId,
    IReadOnlyList<string> TypeNames) : ICommand<Result<NotificationPreference>>;
public sealed record SetNotificationDigestFrequencyCommand(
    Guid UserId,
    DigestFrequency? Frequency) : ICommand<Result<NotificationPreference>>;

public sealed class NotificationMutationCommandHandler(
    INotificationService notificationService,
    INotificationPreferenceService preferenceService) :
    ICommandHandler<MarkNotificationReadCommand, Result>,
    ICommandHandler<MarkAllNotificationsReadCommand, Result>,
    ICommandHandler<MarkNotificationUnreadCommand, Result>,
    ICommandHandler<DeleteNotificationCommand, Result>,
    ICommandHandler<DeleteReadNotificationsCommand, Result<int>>,
    ICommandHandler<UpdateNotificationPreferencesCommand, Result<NotificationPreference>>,
    ICommandHandler<SetNotificationQuietHoursCommand, Result>,
    ICommandHandler<SetNotificationMutedTypesCommand, Result<NotificationPreference>>,
    ICommandHandler<SetNotificationDigestFrequencyCommand, Result<NotificationPreference>>
{
    public Task<Result> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken) =>
        notificationService.MarkAsReadAsync(command.NotificationId, cancellationToken);

    public Task<Result> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken) =>
        notificationService.MarkAllAsReadAsync(command.UserId, cancellationToken);

    public Task<Result> Handle(MarkNotificationUnreadCommand command, CancellationToken cancellationToken) =>
        notificationService.MarkAsUnreadAsync(command.NotificationId, cancellationToken);

    public Task<Result> Handle(DeleteNotificationCommand command, CancellationToken cancellationToken) =>
        notificationService.DeleteAsync(command.NotificationId, cancellationToken);

    public Task<Result<int>> Handle(DeleteReadNotificationsCommand command, CancellationToken cancellationToken) =>
        notificationService.DeleteReadNotificationsAsync(command.UserId, cancellationToken);

    public Task<Result<NotificationPreference>> Handle(
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken) =>
        notificationService.UpdatePreferencesAsync(
            command.UserId,
            command.EmailEnabled,
            command.PushEnabled,
            command.InAppEnabled,
            command.SmsEnabled,
            command.MarketingEnabled,
            command.SocialEnabled,
            command.LearningEnabled,
            command.AchievementsEnabled,
            cancellationToken);

    public Task<Result> Handle(SetNotificationQuietHoursCommand command, CancellationToken cancellationToken) =>
        notificationService.SetQuietHoursAsync(
            command.UserId,
            command.Start,
            command.End,
            command.Timezone,
            cancellationToken);

    public Task<Result<NotificationPreference>> Handle(
        SetNotificationMutedTypesCommand command,
        CancellationToken cancellationToken) =>
        preferenceService.SetMutedTypesAsync(command.UserId, command.TypeNames, cancellationToken);

    public Task<Result<NotificationPreference>> Handle(
        SetNotificationDigestFrequencyCommand command,
        CancellationToken cancellationToken) =>
        preferenceService.SetEmailDigestFrequencyAsync(command.UserId, command.Frequency, cancellationToken);
}
