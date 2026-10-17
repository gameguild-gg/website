using GameGuild.Assets;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Marks an asset reference as in-use while any non-deleted post carries it as media.
/// </summary>
public sealed class PostAssetUsageGuard(IApplicationDbContext db) : IAssetUsageGuard
{
    public Task<bool> IsInUseAsync(Guid assetReferenceId, CancellationToken cancellationToken = default) =>
        db.Set<Post>()
            .AsNoTracking()
            .AnyAsync(post => post.DeletedAt == null && post.MediaAssetReferenceId == assetReferenceId, cancellationToken);
}
