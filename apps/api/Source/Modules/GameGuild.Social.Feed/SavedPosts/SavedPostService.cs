using GameGuild.CQRS;
using GameGuild.Social.Posts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Feed;

public sealed class SavedPostService(IApplicationDbContext context) : ISavedPostService
{
    public async Task<bool> SaveAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var postIsAvailable = await context.Set<Post>()
            .AsNoTracking()
            .AnyAsync(
                post => post.Id == postId &&
                        post.DeletedAt == null &&
                        (post.Visibility != PostVisibility.Private || post.AuthorId == userId),
                cancellationToken)
            .ConfigureAwait(false);

        if (!postIsAvailable)
        {
            throw new SavedPostUnavailableException(postId);
        }

        var alreadySaved = await IsSavedAsync(userId, postId, cancellationToken).ConfigureAwait(false);
        if (alreadySaved)
        {
            return false;
        }

        context.Set<SavedPost>().Add(SavedPost.Create(userId, postId));

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            var concurrentSaveExists = await context.Set<SavedPost>()
                .AsNoTracking()
                .AnyAsync(
                    saved => saved.UserId == userId && saved.PostId == postId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (concurrentSaveExists)
            {
                // A concurrent identical request won the unique-key race. Saving is idempotent.
                return false;
            }

            throw;
        }
    }

    public async Task<bool> UnsaveAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var savedPost = await context.Set<SavedPost>()
            .FirstOrDefaultAsync(
                saved => saved.UserId == userId && saved.PostId == postId,
                cancellationToken)
            .ConfigureAwait(false);

        if (savedPost is null)
        {
            return false;
        }

        context.Set<SavedPost>().Remove(savedPost);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<bool> IsSavedAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
        => context.Set<SavedPost>()
            .AsNoTracking()
            .AnyAsync(saved => saved.UserId == userId && saved.PostId == postId, cancellationToken);
}

public sealed class SavePostCommandHandler(ISavedPostService savedPosts)
    : ICommandHandler<SavePostCommand, SavedPostStateDto>
{
    public async Task<SavedPostStateDto> Handle(SavePostCommand request, CancellationToken cancellationToken)
    {
        await savedPosts.SaveAsync(request.UserId, request.PostId, cancellationToken).ConfigureAwait(false);
        return new SavedPostStateDto(request.PostId, true);
    }
}

public sealed class UnsavePostCommandHandler(ISavedPostService savedPosts)
    : ICommandHandler<UnsavePostCommand, bool>
{
    public Task<bool> Handle(UnsavePostCommand request, CancellationToken cancellationToken)
        => savedPosts.UnsaveAsync(request.UserId, request.PostId, cancellationToken);
}

public sealed class GetSavedPostStateQueryHandler(ISavedPostService savedPosts)
    : IQueryHandler<GetSavedPostStateQuery, SavedPostStateDto>
{
    public async Task<SavedPostStateDto> Handle(GetSavedPostStateQuery request, CancellationToken cancellationToken)
        => new(
            request.PostId,
            await savedPosts.IsSavedAsync(request.UserId, request.PostId, cancellationToken).ConfigureAwait(false));
}
