using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Blog.Services;

/// <summary>Atomic view-count increment for published posts (view beacon).</summary>
public interface IBlogViewCounterService
{
    /// <summary>
    /// Increments <see cref="BlogPost.ViewsCount"/> for a published post. Returns true when a
    /// post was incremented, false when the id is unknown, unpublished, or deleted.
    /// </summary>
    Task<bool> IncrementAsync(Guid postId, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IBlogViewCounterService"/>: atomic <c>UPDATE ... SET ViewsCount = ViewsCount + 1</c>
/// via EF Core <c>ExecuteUpdateAsync</c> — no read-modify-write, no <c>UpdatedAt</c> bump (keeps
/// JSON-LD dateModified stable). Falls back to a tracked increment under the InMemory provider,
/// which lacks ExecuteUpdateAsync (unit tests).
/// </summary>
public sealed class BlogViewCounterService(IApplicationDbContext context) : IBlogViewCounterService
{
    /// <inheritdoc />
    public async Task<bool> IncrementAsync(Guid postId, CancellationToken ct = default)
    {
        if (context is DbContext dbContext && !dbContext.Database.IsRelational())
        {
            return await IncrementTrackedAsync(postId, ct).ConfigureAwait(false);
        }

        try
        {
            var affected = await context.Set<BlogPost>()
                .Where(p => p.Id == postId && p.Status == BlogPostStatus.Published && p.DeletedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(p => p.ViewsCount, p => p.ViewsCount + 1),
                    ct)
                .ConfigureAwait(false);
            return affected > 0;
        }
        catch (NotSupportedException)
        {
            return await IncrementTrackedAsync(postId, ct).ConfigureAwait(false);
        }
    }

    private async Task<bool> IncrementTrackedAsync(Guid postId, CancellationToken ct)
    {
        var post = await context.Set<BlogPost>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.Status == BlogPostStatus.Published && p.DeletedAt == null, ct)
            .ConfigureAwait(false);
        if (post is null)
        {
            return false;
        }

        // IncrementViews() never touches UpdatedAt (JSON-LD dateModified stability).
        post.IncrementViews();
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
