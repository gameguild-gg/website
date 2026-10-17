using GameGuild.Assets;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Blog.Services;

/// <summary>
/// Marks an asset reference as in-use while any non-deleted post body (draft or
/// published — the draft row IS the post) carries an asset:// URI for it.
/// </summary>
public sealed class BlogAssetUsageGuard(IApplicationDbContext db) : IAssetUsageGuard
{
    public Task<bool> IsInUseAsync(Guid assetReferenceId, CancellationToken cancellationToken = default)
    {
        var uri = $"asset://{assetReferenceId}";
        return db.Set<BlogPost>()
            .AsNoTracking()
            .AnyAsync(post => post.DeletedAt == null
                && (post.Content.Contains(uri) || (post.JsonBody != null && post.JsonBody.Contains(uri))),
                cancellationToken);
    }
}
