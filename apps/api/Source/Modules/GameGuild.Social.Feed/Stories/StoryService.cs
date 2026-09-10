using GameGuild.Assets;
using GameGuild.Assets.SocialMedia;
using GameGuild.CQRS;
using GameGuild.Social.Follows;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Feed;

public sealed class StoryService(IApplicationDbContext context) : IStoryService
{
    public async Task<StoryDto> CreateAsync(
        Guid authorId,
        Guid assetReferenceId,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        var asset = await context.Set<AssetReference>()
            .AsNoTracking()
            .Include(reference => reference.Content)
            .FirstOrDefaultAsync(
                asset => asset.Id == assetReferenceId &&
                         asset.CreatedByUserId == authorId &&
                         asset.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (asset?.Content is null ||
            !SocialMediaAssetPolicy.IsSupported(asset.Content.MimeType, asset.Content.SizeBytes) ||
            SocialMediaAssetPolicy.GetProcessingState(asset.Content) != SocialMediaProcessingState.Ready)
        {
            throw new StoryAssetUnavailableException(assetReferenceId);
        }

        var story = Story.Create(authorId, assetReferenceId, caption, SystemClock.UtcNow);
        context.Set<Story>().Add(story);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(story, asset, false);
    }

    public async Task<IReadOnlyList<StoryDto>> GetActiveAsync(
        Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var blockedAuthors = context.Set<Block>()
            .AsNoTracking()
            .Where(block => block.DeletedAt == null && (block.BlockerId == viewerId || block.BlockedId == viewerId))
            .Select(block => block.BlockerId == viewerId ? block.BlockedId : block.BlockerId);
        var mutedAuthors = context.Set<Mute>()
            .AsNoTracking()
            .Where(mute => mute.DeletedAt == null &&
                           mute.MuterId == viewerId &&
                           (mute.ExpiresAt == null || mute.ExpiresAt > now))
            .Select(mute => mute.MutedId);

        var stories = await context.Set<Story>()
            .AsNoTracking()
            .Where(story => story.DeletedAt == null &&
                            story.ExpiresAt > now &&
                            !blockedAuthors.Contains(story.AuthorId) &&
                            !mutedAuthors.Contains(story.AuthorId))
            .OrderBy(story => story.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var storyIds = stories.Select(story => story.Id).ToArray();
        var viewedIds = await context.Set<StoryView>()
            .AsNoTracking()
            .Where(view => view.ViewerId == viewerId && storyIds.Contains(view.StoryId))
            .Select(view => view.StoryId)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var assetIds = stories.Select(story => story.AssetReferenceId).Distinct().ToArray();
        var assetRows = await context.Set<AssetReference>()
            .AsNoTracking()
            .Include(reference => reference.Content)
            .Where(reference => assetIds.Contains(reference.Id) && reference.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var readyAssets = assetRows
            .Where(reference => reference.Content is not null &&
                                SocialMediaAssetPolicy.IsSupported(reference.Content.MimeType, reference.Content.SizeBytes) &&
                                SocialMediaAssetPolicy.GetProcessingState(reference.Content) == SocialMediaProcessingState.Ready)
            .ToDictionary(reference => reference.Id);

        return stories
            .Where(story => readyAssets.ContainsKey(story.AssetReferenceId))
            .Select(story => ToDto(story, readyAssets[story.AssetReferenceId], viewedIds.Contains(story.Id)))
            .ToList();
    }

    public async Task<bool> MarkViewedAsync(
        Guid viewerId,
        Guid storyId,
        CancellationToken cancellationToken = default)
    {
        var storyIsActive = await context.Set<Story>()
            .AsNoTracking()
            .AnyAsync(
                story => story.Id == storyId && story.DeletedAt == null && story.ExpiresAt > SystemClock.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);
        if (!storyIsActive)
        {
            return false;
        }

        var alreadyViewed = await context.Set<StoryView>()
            .AnyAsync(view => view.StoryId == storyId && view.ViewerId == viewerId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyViewed)
        {
            return false;
        }

        context.Set<StoryView>().Add(StoryView.Create(storyId, viewerId, SystemClock.UtcNow));
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            var concurrentViewExists = await context.Set<StoryView>()
                .AsNoTracking()
                .AnyAsync(view => view.StoryId == storyId && view.ViewerId == viewerId, cancellationToken)
                .ConfigureAwait(false);
            if (concurrentViewExists)
            {
                return false;
            }

            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        Guid authorId,
        Guid storyId,
        CancellationToken cancellationToken = default)
    {
        var story = await context.Set<Story>()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == storyId && candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (story is null || story.AuthorId != authorId)
        {
            return false;
        }

        story.Delete();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static StoryDto ToDto(Story story, AssetReference asset, bool isViewed)
        => new(
            story.Id,
            story.AuthorId,
            story.AssetReferenceId,
            $"/api/assets/{story.AssetReferenceId}/content",
            SocialMediaAssetPolicy.NormalizeMimeType(asset.Content.MimeType),
            story.Caption,
            story.ExpiresAt,
            isViewed,
            story.CreatedAt);
}

public sealed class CreateStoryCommandHandler(IStoryService stories) : ICommandHandler<CreateStoryCommand, StoryDto>
{
    public Task<StoryDto> Handle(CreateStoryCommand request, CancellationToken cancellationToken)
        => stories.CreateAsync(request.AuthorId, request.AssetReferenceId, request.Caption, cancellationToken);
}

public sealed class MarkStoryViewedCommandHandler(IStoryService stories) : ICommandHandler<MarkStoryViewedCommand, bool>
{
    public Task<bool> Handle(MarkStoryViewedCommand request, CancellationToken cancellationToken)
        => stories.MarkViewedAsync(request.ViewerId, request.StoryId, cancellationToken);
}

public sealed class DeleteStoryCommandHandler(IStoryService stories) : ICommandHandler<DeleteStoryCommand, bool>
{
    public Task<bool> Handle(DeleteStoryCommand request, CancellationToken cancellationToken)
        => stories.DeleteAsync(request.AuthorId, request.StoryId, cancellationToken);
}

public sealed class GetActiveStoriesQueryHandler(IStoryService stories)
    : IQueryHandler<GetActiveStoriesQuery, IReadOnlyList<StoryDto>>
{
    public Task<IReadOnlyList<StoryDto>> Handle(GetActiveStoriesQuery request, CancellationToken cancellationToken)
        => stories.GetActiveAsync(request.ViewerId, cancellationToken);
}
