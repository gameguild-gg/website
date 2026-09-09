using GameGuild.CQRS;
using GameGuild.Social.Follows.Services;

namespace GameGuild.Social.Follows.Commands;

public sealed record FollowEntityEndpointCommand(
    Guid UserId,
    Guid EntityId,
    string EntityType,
    bool NotificationsEnabled) : ICommand<Result<Follow>>;

public sealed record UnfollowEntityEndpointCommand(
    Guid UserId,
    Guid EntityId,
    string EntityType) : ICommand<Result>;

public sealed record UpdateFollowNotificationsEndpointCommand(
    Guid UserId,
    Guid EntityId,
    string EntityType,
    bool NotificationsEnabled) : ICommand<Result<Follow>>;

public sealed record UpdateFollowPrivacyEndpointCommand(
    Guid UserId,
    bool IsFollowerListPublic,
    bool IsFollowingListPublic,
    bool AllowFollowers,
    bool NotifyOnNewFollower,
    bool ShowFollowerCount,
    bool ShowFollowingCount) : ICommand<Result<FollowPrivacySettings>>;

public sealed record BlockUserEndpointCommand(
    Guid UserId,
    Guid BlockedUserId,
    string? Reason) : ICommand<Result<Block>>;

public sealed record UnblockUserEndpointCommand(Guid UserId, Guid BlockedUserId) : ICommand<Result>;

public sealed record MuteUserEndpointCommand(
    Guid UserId,
    Guid MutedUserId,
    string? Reason,
    DateTime? ExpiresAt) : ICommand<Result<Mute>>;

public sealed record UnmuteUserEndpointCommand(Guid UserId, Guid MutedUserId) : ICommand<Result>;

public sealed class FollowerEndpointCommandHandler(IFollowerService followerService) :
    ICommandHandler<FollowEntityEndpointCommand, Result<Follow>>,
    ICommandHandler<UnfollowEntityEndpointCommand, Result>,
    ICommandHandler<UpdateFollowNotificationsEndpointCommand, Result<Follow>>,
    ICommandHandler<UpdateFollowPrivacyEndpointCommand, Result<FollowPrivacySettings>>,
    ICommandHandler<BlockUserEndpointCommand, Result<Block>>,
    ICommandHandler<UnblockUserEndpointCommand, Result>,
    ICommandHandler<MuteUserEndpointCommand, Result<Mute>>,
    ICommandHandler<UnmuteUserEndpointCommand, Result>
{
    public Task<Result<Follow>> Handle(FollowEntityEndpointCommand request, CancellationToken cancellationToken) =>
        followerService.FollowAsync(
            request.UserId,
            request.EntityId,
            request.EntityType,
            request.NotificationsEnabled,
            cancellationToken);

    public Task<Result> Handle(UnfollowEntityEndpointCommand request, CancellationToken cancellationToken) =>
        followerService.UnfollowAsync(request.UserId, request.EntityId, request.EntityType, cancellationToken);

    public Task<Result<Follow>> Handle(
        UpdateFollowNotificationsEndpointCommand request,
        CancellationToken cancellationToken) =>
        followerService.UpdateNotificationSettingsAsync(
            request.UserId,
            request.EntityId,
            request.EntityType,
            request.NotificationsEnabled,
            cancellationToken);

    public Task<Result<FollowPrivacySettings>> Handle(
        UpdateFollowPrivacyEndpointCommand request,
        CancellationToken cancellationToken) =>
        followerService.UpdatePrivacySettingsAsync(
            request.UserId,
            request.IsFollowerListPublic,
            request.IsFollowingListPublic,
            request.AllowFollowers,
            request.NotifyOnNewFollower,
            request.ShowFollowerCount,
            request.ShowFollowingCount,
            cancellationToken);

    public Task<Result<Block>> Handle(BlockUserEndpointCommand request, CancellationToken cancellationToken) =>
        followerService.BlockUserAsync(
            request.UserId,
            request.BlockedUserId,
            request.Reason,
            cancellationToken);

    public Task<Result> Handle(UnblockUserEndpointCommand request, CancellationToken cancellationToken) =>
        followerService.UnblockUserAsync(request.UserId, request.BlockedUserId, cancellationToken);

    public Task<Result<Mute>> Handle(MuteUserEndpointCommand request, CancellationToken cancellationToken) =>
        followerService.MuteUserAsync(
            request.UserId,
            request.MutedUserId,
            request.Reason,
            request.ExpiresAt,
            cancellationToken);

    public Task<Result> Handle(UnmuteUserEndpointCommand request, CancellationToken cancellationToken) =>
        followerService.UnmuteUserAsync(request.UserId, request.MutedUserId, cancellationToken);
}
