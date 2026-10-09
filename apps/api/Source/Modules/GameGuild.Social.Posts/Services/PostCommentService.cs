using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Post comment CRUD operations
/// </summary>
public class PostCommentService : IPostCommentService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PostCommentService> _logger;

    private static class PostErrors
    {
        public static Error NotFound => Error.NotFound("Post.NotFound", "Post not found");
        public static Error CommentNotFound => Error.NotFound("Comment.NotFound", "Comment not found");
        public static Error ParentCommentNotFound => Error.NotFound("ParentComment.NotFound", "Parent comment not found");
        public static Error Forbidden => Error.Forbidden("Comment.Forbidden", "Only the comment author can perform this action");
    }

    public PostCommentService(IApplicationDbContext context, ILogger<PostCommentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PostComment>> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentCommentId = null, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
        {
            return Result.Failure<PostComment>(PostErrors.NotFound);
        }

        if (parentCommentId.HasValue)
        {
            var parentExists = await _context.Set<PostComment>()
                .AnyAsync(c => c.Id == parentCommentId.Value && c.PostId == postId, cancellationToken).ConfigureAwait(false);

            if (!parentExists)
            {
                return Result.Failure<PostComment>(PostErrors.ParentCommentNotFound);
            }
        }

        var comment = PostComment.Create(postId, authorId, content, parentCommentId);
        _context.Set<PostComment>().Add(comment);

        post.IncrementComments();

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Added comment {CommentId} to post {PostId}", comment.Id, postId);

        return Result.Success(comment);
    }

    public async Task<Result<PostComment>> UpdateCommentAsync(Guid commentId, Guid actorId, string content, CancellationToken cancellationToken = default)
    {
        var comment = await _context.Set<PostComment>()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (comment is null)
        {
            return Result.Failure<PostComment>(PostErrors.CommentNotFound);
        }

        if (comment.AuthorId != actorId)
        {
            return Result.Failure<PostComment>(PostErrors.Forbidden);
        }

        comment.Edit(content);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(comment);
    }

    public async Task<Result> DeleteCommentAsync(Guid commentId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var comment = await _context.Set<PostComment>()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (comment is null)
        {
            return Result.Failure(PostErrors.CommentNotFound);
        }

        if (comment.AuthorId != actorId)
        {
            return Result.Failure(PostErrors.Forbidden);
        }

        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == comment.PostId, cancellationToken).ConfigureAwait(false);

        var postComments = await _context.Set<PostComment>()
            .Where(c => c.PostId == comment.PostId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var commentsById = postComments.ToDictionary(candidate => candidate.Id);
        var childrenByParentId = postComments
            .Where(candidate => candidate.ParentCommentId.HasValue)
            .GroupBy(candidate => candidate.ParentCommentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        var deletedCount = 0;
        pending.Push(comment.Id);

        while (pending.Count > 0)
        {
            var currentId = pending.Pop();
            if (!visited.Add(currentId) || !commentsById.TryGetValue(currentId, out var current))
            {
                continue;
            }

            if (!current.IsDeleted)
            {
                current.Delete();
                deletedCount++;
            }

            if (!childrenByParentId.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                pending.Push(child.Id);
            }
        }

        for (var index = 0; index < deletedCount; index++)
        {
            post?.DecrementComments();
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<PostComment>>> GetPostCommentsAsync(Guid postId, int skip = 0, int take = 50, Guid? parentCommentId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<PostComment>()
            .Where(c => c.PostId == postId && c.DeletedAt == null);

        if (parentCommentId.HasValue)
        {
            query = query.Where(c => c.ParentCommentId == parentCommentId.Value);
        }

        var comments = await query
            .OrderBy(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<PostComment>>(comments);
    }

    public async Task<Result<PostComment>> GetCommentByIdAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var comment = await _context.Set<PostComment>()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (comment is null)
        {
            return Result.Failure<PostComment>(PostErrors.CommentNotFound);
        }

        return Result.Success(comment);
    }
}
