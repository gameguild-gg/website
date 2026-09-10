
namespace GameGuild.Social.Posts;

/// <summary>
/// Represents a social post (microblog/status update)
/// </summary>
public class Post : EntityBase
{
    public Guid AuthorId { get; private set; }
    public new Guid? TenantId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? MediaUrl { get; private set; }
    public MediaType? MediaType { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsEdited { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public int LikesCount { get; private set; }
    public int CommentsCount { get; private set; }
    public int SharesCount { get; private set; }
    public int ViewsCount { get; private set; }
    public Guid? ReplyToPostId { get; private set; }
    public Guid? RepostOfPostId { get; private set; }

    private Post() { } // EF Core

    public static Post Create(Guid authorId, string content, PostVisibility visibility = PostVisibility.Public, Guid? tenantId = null)
    {
        return new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            TenantId = tenantId,
            Content = content,
            Visibility = visibility,
            IsPinned = false,
            IsEdited = false,
            LikesCount = 0,
            CommentsCount = 0,
            SharesCount = 0,
            ViewsCount = 0
        };
    }

    public static Post CreateRepost(Guid authorId, Post source, string? content = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var repost = Create(authorId, content?.Trim() ?? string.Empty, PostVisibility.Public, source.TenantId);
        repost.RepostOfPostId = source.Id;
        return repost;
    }

    public void AttachMedia(string mediaUrl, MediaType? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl)) throw new ArgumentException("Media URL is required.", nameof(mediaUrl));
        MediaUrl = mediaUrl.Trim();
        MediaType = mediaType;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Edit(string content)
    {
        Content = content;
        IsEdited = true;
        EditedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Pin() { IsPinned = true; UpdatedAt = SystemClock.UtcNow; }
    public void Unpin() { IsPinned = false; UpdatedAt = SystemClock.UtcNow; }
    public void IncrementLikes() => LikesCount++;
    public void DecrementLikes() { if (LikesCount > 0) LikesCount--; }
    public void IncrementComments() => CommentsCount++;
    public void DecrementComments() { if (CommentsCount > 0) CommentsCount--; }
    public void IncrementShares() => SharesCount++;
    public void DecrementShares() { if (SharesCount > 0) SharesCount--; }
    public void IncrementViews() => ViewsCount++;
    public void Delete() => SoftDelete();
}

/// <summary>
/// Represents a comment on a post
/// </summary>
public class PostComment : EntityBase
{
    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsEdited { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public int LikesCount { get; private set; }

    private PostComment() { } // EF Core

    public static PostComment Create(Guid postId, Guid authorId, string content, Guid? parentCommentId = null)
    {
        return new PostComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            ParentCommentId = parentCommentId,
            Content = content,
            IsEdited = false,
            LikesCount = 0
        };
    }

    public void Edit(string content)
    {
        Content = content;
        IsEdited = true;
        EditedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void IncrementLikes() => LikesCount++;
    public void DecrementLikes() { if (LikesCount > 0) LikesCount--; }
    public void Delete() => SoftDelete();
}

public enum PostVisibility
{
    Public,
    Followers,
    Private,
    Unlisted
}

public enum MediaType
{
    Image,
    Video,
    Audio,
    Document
}
