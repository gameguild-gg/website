using GameGuild.CQRS;
using GameGuild.Social.Posts.Services;

namespace GameGuild.Social.Posts.Commands;

public sealed record CreatePostEndpointCommand(
    Guid AuthorId,
    string Content,
    PostVisibility Visibility,
    string? MediaUrl,
    MediaType? MediaType,
    Guid? TenantId,
    string[]? Tags) : ICommand<Result<Post>>;

public sealed record UpdatePostEndpointCommand(Guid PostId, Guid ActorId, string Content) : ICommand<Result<Post>>;

public sealed record DeletePostEndpointCommand(Guid PostId, Guid ActorId) : ICommand<Result>;

public sealed record AddPostCommentEndpointCommand(
    Guid PostId,
    Guid AuthorId,
    string Content,
    Guid? ParentCommentId) : ICommand<Result<PostComment>>;

public sealed record UpdatePostCommentEndpointCommand(Guid CommentId, Guid ActorId, string Content) : ICommand<Result<PostComment>>;

public sealed record DeletePostCommentEndpointCommand(Guid CommentId, Guid ActorId) : ICommand<Result>;

public sealed record TogglePostLikeEndpointCommand(
    Guid PostId,
    Guid UserId,
    string ReactionType) : ICommand<Result<bool>>;

public sealed record TogglePostPinEndpointCommand(Guid PostId, Guid ActorId) : ICommand<Result<bool>>;

public sealed record CreateRepostEndpointCommand(Guid SourcePostId, Guid ActorId, string? Content) : ICommand<Result<Post>>;

public sealed record SharePostEndpointCommand(Guid PostId) : ICommand<Result>;

public sealed record RecordPostViewEndpointCommand(
    Guid PostId,
    Guid? ViewerId,
    string? IpAddress,
    string? UserAgent,
    string? Referrer) : ICommand<Result>;

public sealed record FollowPostEndpointCommand(
    Guid PostId,
    Guid UserId,
    bool NotifyOnComments,
    bool NotifyOnLikes,
    bool NotifyOnShares,
    bool NotifyOnUpdates) : ICommand<Result<PostFollower>>;

public sealed record UnfollowPostEndpointCommand(Guid PostId, Guid UserId) : ICommand<Result>;

public sealed class PostEndpointCommandHandler(IPostService postService) :
    ICommandHandler<CreatePostEndpointCommand, Result<Post>>,
    ICommandHandler<UpdatePostEndpointCommand, Result<Post>>,
    ICommandHandler<DeletePostEndpointCommand, Result>,
    ICommandHandler<AddPostCommentEndpointCommand, Result<PostComment>>,
    ICommandHandler<UpdatePostCommentEndpointCommand, Result<PostComment>>,
    ICommandHandler<DeletePostCommentEndpointCommand, Result>,
    ICommandHandler<TogglePostLikeEndpointCommand, Result<bool>>,
    ICommandHandler<TogglePostPinEndpointCommand, Result<bool>>,
    ICommandHandler<CreateRepostEndpointCommand, Result<Post>>,
    ICommandHandler<SharePostEndpointCommand, Result>,
    ICommandHandler<RecordPostViewEndpointCommand, Result>,
    ICommandHandler<FollowPostEndpointCommand, Result<PostFollower>>,
    ICommandHandler<UnfollowPostEndpointCommand, Result>
{
    public async Task<Result<Post>> Handle(
        CreatePostEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var result = await postService.CreatePostAsync(
            request.AuthorId,
            request.Content,
            request.Visibility,
            request.MediaUrl,
            request.MediaType,
            request.TenantId,
            cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null && request.Tags?.Length > 0)
        {
            await postService
                .AddTagsToPostAsync(result.Value.Id, request.Tags, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    public Task<Result<Post>> Handle(UpdatePostEndpointCommand request, CancellationToken cancellationToken) =>
        postService.UpdatePostAsync(request.PostId, request.ActorId, request.Content, cancellationToken);

    public Task<Result> Handle(DeletePostEndpointCommand request, CancellationToken cancellationToken) =>
        postService.DeletePostAsync(request.PostId, request.ActorId, cancellationToken);

    public Task<Result<PostComment>> Handle(AddPostCommentEndpointCommand request, CancellationToken cancellationToken) =>
        postService.AddCommentAsync(
            request.PostId,
            request.AuthorId,
            request.Content,
            request.ParentCommentId,
            cancellationToken);

    public Task<Result<PostComment>> Handle(UpdatePostCommentEndpointCommand request, CancellationToken cancellationToken) =>
        postService.UpdateCommentAsync(request.CommentId, request.ActorId, request.Content, cancellationToken);

    public Task<Result> Handle(DeletePostCommentEndpointCommand request, CancellationToken cancellationToken) =>
        postService.DeleteCommentAsync(request.CommentId, request.ActorId, cancellationToken);

    public Task<Result<bool>> Handle(TogglePostLikeEndpointCommand request, CancellationToken cancellationToken) =>
        postService.TogglePostLikeAsync(request.PostId, request.UserId, request.ReactionType, cancellationToken);

    public Task<Result<bool>> Handle(TogglePostPinEndpointCommand request, CancellationToken cancellationToken) =>
        postService.TogglePostPinAsync(request.PostId, request.ActorId, cancellationToken);

    public Task<Result<Post>> Handle(CreateRepostEndpointCommand request, CancellationToken cancellationToken) =>
        postService.CreateRepostAsync(request.SourcePostId, request.ActorId, request.Content, cancellationToken);

    public Task<Result> Handle(SharePostEndpointCommand request, CancellationToken cancellationToken) =>
        postService.SharePostAsync(request.PostId, cancellationToken);

    public Task<Result> Handle(RecordPostViewEndpointCommand request, CancellationToken cancellationToken) =>
        postService.RecordPostViewAsync(
            request.PostId,
            request.ViewerId,
            request.IpAddress,
            request.UserAgent,
            request.Referrer,
            cancellationToken);

    public Task<Result<PostFollower>> Handle(FollowPostEndpointCommand request, CancellationToken cancellationToken) =>
        postService.FollowPostAsync(
            request.PostId,
            request.UserId,
            request.NotifyOnComments,
            request.NotifyOnLikes,
            request.NotifyOnShares,
            request.NotifyOnUpdates,
            cancellationToken);

    public Task<Result> Handle(UnfollowPostEndpointCommand request, CancellationToken cancellationToken) =>
        postService.UnfollowPostAsync(request.PostId, request.UserId, cancellationToken);
}
