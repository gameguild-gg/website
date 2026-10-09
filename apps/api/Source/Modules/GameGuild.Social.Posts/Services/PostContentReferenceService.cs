using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Post content reference management
/// </summary>
public class PostContentReferenceService : IPostContentReferenceService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PostContentReferenceService> _logger;

    private static class PostErrors
    {
        public static Error NotFound => Error.NotFound("Post.NotFound", "Post not found");
        public static Error ReferenceNotFound => Error.NotFound("ContentReference.NotFound", "Content reference not found");
    }

    public PostContentReferenceService(IApplicationDbContext context, ILogger<PostContentReferenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PostContentReference>> AddContentReferenceAsync(
        Guid postId,
        Guid resourceId,
        string resourceType,
        string referenceType = "mention",
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
        {
            return Result.Failure<PostContentReference>(PostErrors.NotFound);
        }

        var order = await _context.Set<PostContentReference>()
            .CountAsync(r => r.PostId == postId, cancellationToken).ConfigureAwait(false);

        var reference = PostContentReference.Create(postId, resourceId, resourceType, referenceType, context, order);
        _context.Set<PostContentReference>().Add(reference);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(reference);
    }

    public async Task<Result> RemoveContentReferenceAsync(Guid referenceId, CancellationToken cancellationToken = default)
    {
        var reference = await _context.Set<PostContentReference>()
            .FirstOrDefaultAsync(r => r.Id == referenceId, cancellationToken).ConfigureAwait(false);

        if (reference is null)
        {
            return Result.Failure(PostErrors.ReferenceNotFound);
        }

        _context.Set<PostContentReference>().Remove(reference);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<PostContentReference>>> GetPostContentReferencesAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var references = await _context.Set<PostContentReference>()
            .Where(r => r.PostId == postId)
            .OrderBy(r => r.Order)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<PostContentReference>>(references);
    }
}
