using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Post CRUD operations, queries, search, and filtering
/// </summary>
public class PostCrudService : IPostCrudService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PostCrudService> _logger;

    private static class PostErrors
    {
        public static Error NotFound => Error.NotFound("Post.NotFound", "Post not found");
        public static Error Forbidden => Error.Forbidden("Post.Forbidden", "Only the post author can perform this action");
    }

    public PostCrudService(IApplicationDbContext context, ILogger<PostCrudService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Basic CRUD Operations

    public async Task<Result<Post>> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        return post is null
            ? Result.Failure<Post>(PostErrors.NotFound)
            : Result.Success(post);
    }

    public async Task<Result<Post>> GetPostByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetPostByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IEnumerable<Post>>> GetPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<Post>> CreatePostAsync(
        Guid authorId,
        string content,
        PostVisibility visibility = PostVisibility.Public,
        string? mediaUrl = null,
        MediaType? mediaType = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var post = Post.Create(authorId, content, visibility, tenantId);
        if (!string.IsNullOrWhiteSpace(mediaUrl))
        {
            post.AttachMedia(mediaUrl, mediaType);
        }

        _context.Set<Post>().Add(post);

        var statistics = PostStatistics.Create(post.Id);
        _context.Set<PostStatistics>().Add(statistics);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created post {PostId} by author {AuthorId}", post.Id, authorId);

        return Result.Success(post);
    }

    public async Task<Result<Post>> UpdatePostAsync(Guid postId, Guid actorId, string content, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure<Post>(PostErrors.NotFound);

        if (post.AuthorId != actorId)
            return Result.Failure<Post>(PostErrors.Forbidden);

        post.Edit(content);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated post {PostId}", postId);

        return Result.Success(post);
    }

    public async Task<Result> DeletePostAsync(Guid postId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure(PostErrors.NotFound);

        if (post.AuthorId != actorId)
            return Result.Failure(PostErrors.Forbidden);

        if (post.RepostOfPostId is Guid sourcePostId)
        {
            var source = await _context.Set<Post>()
                .FirstOrDefaultAsync(p => p.Id == sourcePostId && p.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false);
            source?.DecrementShares();
        }

        post.Delete();
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Deleted post {PostId}", postId);

        return Result.Success();
    }

    public async Task<Result> RestorePostAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure(PostErrors.NotFound);

        post.Restore();
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Restored post {PostId}", postId);

        return Result.Success();
    }

    #endregion

    #region Filtered Queries

    public async Task<Result<IEnumerable<Post>>> GetPostsByAuthorAsync(Guid authorId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.AuthorId == authorId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<IEnumerable<Post>>> GetPostsByVisibilityAsync(PostVisibility visibility, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.Visibility == visibility && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<IEnumerable<Post>>> GetPinnedPostsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Post>()
            .Where(p => p.IsPinned && p.DeletedAt == null);

        if (tenantId.HasValue)
            query = query.Where(p => p.TenantId == tenantId.Value);

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<IEnumerable<Post>>> GetPublicPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.Visibility == PostVisibility.Public && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<IEnumerable<Post>>> GetPostsByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.TenantId == tenantId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    #endregion

    #region Search and Advanced Queries

    public async Task<Result<IEnumerable<Post>>> SearchPostsAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.DeletedAt == null && p.Content.Contains(searchTerm))
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<IEnumerable<Post>>> GetPostsByTagsAsync(string[] tags, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var normalizedTags = tags.Select(t => t.ToLowerInvariant().Trim()).ToArray();

        var postIds = await _context.Set<PostTagAssignment>()
            .Join(_context.Set<PostTag>(),
                assignment => assignment.TagId,
                tag => tag.Id,
                (assignment, tag) => new { assignment.PostId, tag.Name })
            .Where(x => normalizedTags.Contains(x.Name))
            .Select(x => x.PostId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var posts = await _context.Set<Post>()
            .Where(p => postIds.Contains(p.Id) && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    public async Task<Result<IEnumerable<Post>>> GetTrendingPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var postIds = await _context.Set<PostStatistics>()
            .OrderByDescending(s => s.TrendingScore)
            .Skip(skip)
            .Take(take)
            .Select(s => s.PostId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var posts = await _context.Set<Post>()
            .Where(p => postIds.Contains(p.Id) && p.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var orderedPosts = postIds
            .Select(id => posts.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Cast<Post>()
            .ToList();

        return Result.Success<IEnumerable<Post>>(orderedPosts);
    }

    public async Task<Result<IEnumerable<Post>>> GetFeedPostsAsync(Guid userId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.DeletedAt == null &&
                       (p.Visibility == PostVisibility.Public || p.AuthorId == userId))
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<Post>>(posts);
    }

    #endregion

    #region Validation and Utilities

    public async Task<Result<bool>> CanUserPerformActionAsync(Guid postId, Guid userId, string action, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure<bool>(PostErrors.NotFound);

        if (post.AuthorId == userId)
            return Result.Success(true);

        return action.ToLowerInvariant() switch
        {
            "view" => Result.Success(post.Visibility == PostVisibility.Public || post.Visibility == PostVisibility.Unlisted),
            "like" or "comment" => Result.Success(post.Visibility != PostVisibility.Private),
            "edit" or "delete" or "pin" => Result.Success(false),
            _ => Result.Success(false)
        };
    }

    #endregion
}
