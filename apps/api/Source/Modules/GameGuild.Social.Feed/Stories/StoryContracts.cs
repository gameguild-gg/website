using GameGuild.CQRS;

namespace GameGuild.Social.Feed;

public sealed record StoryDto(
    Guid Id,
    Guid AuthorId,
    Guid AssetReferenceId,
    string MediaUrl,
    string MediaType,
    string? Caption,
    DateTime ExpiresAt,
    bool IsViewed,
    DateTime CreatedAt);

public sealed record CreateStoryRequest(Guid AssetReferenceId, string? Caption = null);

public sealed record CreateStoryCommand(Guid AuthorId, Guid AssetReferenceId, string? Caption) : ICommand<StoryDto>;

public sealed record MarkStoryViewedCommand(Guid ViewerId, Guid StoryId) : ICommand<bool>;

public sealed record DeleteStoryCommand(Guid AuthorId, Guid StoryId) : ICommand<bool>;

public sealed record GetActiveStoriesQuery(Guid ViewerId) : IQuery<IReadOnlyList<StoryDto>>;

public sealed class StoryAssetUnavailableException(Guid assetReferenceId)
    : InvalidOperationException($"Asset '{assetReferenceId}' is unavailable for this story.")
{
    public Guid AssetReferenceId { get; } = assetReferenceId;
}

public interface IStoryService
{
    Task<StoryDto> CreateAsync(Guid authorId, Guid assetReferenceId, string? caption, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoryDto>> GetActiveAsync(Guid viewerId, CancellationToken cancellationToken = default);

    Task<bool> MarkViewedAsync(Guid viewerId, Guid storyId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid authorId, Guid storyId, CancellationToken cancellationToken = default);
}
