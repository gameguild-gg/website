using GameGuild.CQRS;

namespace GameGuild.Social.Feed;

public sealed record SavedPostStateDto(Guid PostId, bool IsSaved);

public sealed record SavePostCommand(Guid UserId, Guid PostId) : ICommand<SavedPostStateDto>;

public sealed record UnsavePostCommand(Guid UserId, Guid PostId) : ICommand<bool>;

public sealed record GetSavedPostStateQuery(Guid UserId, Guid PostId) : IQuery<SavedPostStateDto>;

public sealed class SavedPostUnavailableException(Guid postId)
    : InvalidOperationException($"Post '{postId}' is unavailable and cannot be saved.")
{
    public Guid PostId { get; } = postId;
}

public interface ISavedPostService
{
    Task<bool> SaveAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);

    Task<bool> UnsaveAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);

    Task<bool> IsSavedAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
