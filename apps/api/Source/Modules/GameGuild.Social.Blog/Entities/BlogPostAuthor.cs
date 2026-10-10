namespace GameGuild.Social.Blog;

/// <summary>
/// A co-author of a blog post. The primary author lives on the post row itself;
/// this table holds everyone else with edit rights.
/// </summary>
public sealed class BlogPostAuthor : EntityBase
{
    public Guid BlogPostId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AddedByUserId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    private BlogPostAuthor() { } // EF Core

    public static BlogPostAuthor Create(Guid blogPostId, Guid userId, Guid addedByUserId, DateTimeOffset? now = null)
    {
        if (blogPostId == Guid.Empty || userId == Guid.Empty || addedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Post, user, and acting user IDs are required.");
        }

        var at = now ?? DateTimeOffset.UtcNow;
        return new BlogPostAuthor
        {
            Id = Guid.NewGuid(),
            BlogPostId = blogPostId,
            UserId = userId,
            AddedByUserId = addedByUserId,
            AddedAt = at,
            CreatedAt = at.UtcDateTime,
            UpdatedAt = at.UtcDateTime,
        };
    }
}

/// <summary>
/// One row per retired (primary author, slug) route. Powers permanent redirects
/// from old URLs to the current canonical post.
/// </summary>
public sealed class BlogSlugHistory : EntityBase
{
    public Guid BlogPostId { get; private set; }
    public Guid PreviousPrimaryAuthorId { get; private set; }
    public string PreviousSlug { get; private set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; private set; }

    private BlogSlugHistory() { } // EF Core

    public static BlogSlugHistory Create(
        Guid blogPostId,
        Guid previousPrimaryAuthorId,
        string previousSlug,
        DateTimeOffset? now = null)
    {
        if (blogPostId == Guid.Empty || previousPrimaryAuthorId == Guid.Empty)
        {
            throw new ArgumentException("Post and previous author IDs are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(previousSlug);

        var at = now ?? DateTimeOffset.UtcNow;
        return new BlogSlugHistory
        {
            Id = Guid.NewGuid(),
            BlogPostId = blogPostId,
            PreviousPrimaryAuthorId = previousPrimaryAuthorId,
            PreviousSlug = previousSlug.Trim(),
            ChangedAt = at,
            CreatedAt = at.UtcDateTime,
            UpdatedAt = at.UtcDateTime,
        };
    }
}

/// <summary>
/// A comment on a published blog post. Depth ≤ 1 (top-level or one reply level) is
/// enforced by the comment service. Soft delete is moderation.
/// </summary>
public sealed class BlogComment : EntityBase
{
    public Guid BlogPostId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public Guid? ParentCommentId { get; private set; }
    public new DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

    private BlogComment() { } // EF Core

    public static BlogComment Create(Guid blogPostId, Guid authorUserId, string content, Guid? parentCommentId = null)
    {
        if (blogPostId == Guid.Empty || authorUserId == Guid.Empty)
        {
            throw new ArgumentException("Post and author IDs are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (content.Length > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(content), "Comments are limited to 2000 characters.");
        }

        return new BlogComment
        {
            Id = Guid.NewGuid(),
            BlogPostId = blogPostId,
            AuthorUserId = authorUserId,
            Content = content.Trim(),
            ParentCommentId = parentCommentId,
        };
    }

    /// <summary>Moderation soft delete — callable by the comment author or any post author (service enforces roles).</summary>
    public void Delete(Guid deletedByUserId, DateTimeOffset? now = null)
    {
        if (deletedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Acting user ID is required.", nameof(deletedByUserId));
        }

        if (DeletedAt.HasValue)
        {
            return;
        }

        var at = now ?? DateTimeOffset.UtcNow;
        DeletedAt = at;
        DeletedByUserId = deletedByUserId;
        UpdatedAt = at.UtcDateTime;
    }
}
