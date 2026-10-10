using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Posts;

/// <summary>
/// Statistics tracking for a post (engagement metrics, trending scores)
/// </summary>
[Table("post_statistics")]
[Index(nameof(PostId), IsUnique = true)]
[Index(nameof(TrendingScore))]
public class PostStatistics : EntityBase
{
    public Guid PostId { get; private set; }

    /// <summary>Total view count</summary>
    public int ViewsCount { get; private set; }

    /// <summary>Unique viewers count</summary>
    public int UniqueViewersCount { get; private set; }

    /// <summary>External shares (social media, etc.)</summary>
    public int ExternalSharesCount { get; private set; }

    /// <summary>Average time spent viewing (in seconds)</summary>
    public double AverageEngagementTime { get; private set; }

    /// <summary>Calculated engagement score based on interactions</summary>
    public double EngagementScore { get; private set; }

    /// <summary>Trending score for feed algorithms</summary>
    public double TrendingScore { get; private set; }

    /// <summary>When statistics were last calculated</summary>
    public DateTime LastCalculatedAt { get; private set; } = SystemClock.UtcNow;

    private PostStatistics() { } // EF Core

    public static PostStatistics Create(Guid postId)
    {
        return new PostStatistics
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ViewsCount = 0,
            UniqueViewersCount = 0,
            ExternalSharesCount = 0,
            AverageEngagementTime = 0,
            EngagementScore = 0,
            TrendingScore = 0,
            LastCalculatedAt = SystemClock.UtcNow
        };
    }

    public void IncrementViews(bool isUnique = false)
    {
        ViewsCount++;
        if (isUnique)
        {
            UniqueViewersCount++;
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    public void IncrementExternalShares()
    {
        ExternalSharesCount++;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void UpdateEngagementTime(double seconds)
    {
        // Rolling average calculation
        AverageEngagementTime = ViewsCount > 0
            ? ((AverageEngagementTime * (ViewsCount - 1)) + seconds) / ViewsCount
            : seconds;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void RecalculateScores(int likesCount, int commentsCount, int sharesCount, int hoursOld)
    {
        // Engagement score: weighted sum of interactions
        EngagementScore = (likesCount * 1.0) + (commentsCount * 2.0) + (sharesCount * 3.0) + (UniqueViewersCount * 0.1);

        // Trending score: engagement with time decay (similar to Hacker News algorithm)
        var gravity = 1.8;
        TrendingScore = EngagementScore / Math.Pow(hoursOld + 2, gravity);

        LastCalculatedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Links a post to other resources (courses, projects, programs, etc.)
/// </summary>
[Table("post_content_references")]
[Index(nameof(PostId))]
[Index(nameof(ReferencedResourceId))]
[Index(nameof(ReferenceType))]
public class PostContentReference : EntityBase
{
    public Guid PostId { get; private set; }
    public Guid ReferencedResourceId { get; private set; }

    /// <summary>Type of reference: mention, embed, link, share</summary>
    [MaxLength(50)]
    public string ReferenceType { get; private set; } = "mention";

    /// <summary>Type of the referenced resource: Course, Project, Program, etc.</summary>
    [MaxLength(100)]
    public string ResourceType { get; private set; } = string.Empty;

    /// <summary>Optional context or description for the reference</summary>
    [MaxLength(500)]
    public string? Context { get; private set; }

    /// <summary>Display order if multiple references</summary>
    public int Order { get; private set; }

    private PostContentReference() { } // EF Core

    public static PostContentReference Create(
        Guid postId,
        Guid resourceId,
        string resourceType,
        string referenceType = "mention",
        string? context = null,
        int order = 0)
    {
        return new PostContentReference
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ReferencedResourceId = resourceId,
            ResourceType = resourceType,
            ReferenceType = referenceType,
            Context = context,
            Order = order
        };
    }
}

/// <summary>
/// Tracks users following a specific post for notifications
/// </summary>
[Table("post_followers")]
[Index(nameof(PostId))]
[Index(nameof(UserId))]
[Index(nameof(PostId), nameof(UserId), IsUnique = true)]
public class PostFollower : EntityBase
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Notify when someone comments</summary>
    public bool NotifyOnComments { get; private set; } = true;

    /// <summary>Notify when someone likes</summary>
    public bool NotifyOnLikes { get; private set; }

    /// <summary>Notify when post is shared</summary>
    public bool NotifyOnShares { get; private set; }

    /// <summary>Notify when post is updated</summary>
    public bool NotifyOnUpdates { get; private set; } = true;

    private PostFollower() { } // EF Core

    public static PostFollower Create(
        Guid postId,
        Guid userId,
        bool notifyOnComments = true,
        bool notifyOnLikes = false,
        bool notifyOnShares = false,
        bool notifyOnUpdates = true)
    {
        return new PostFollower
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            NotifyOnComments = notifyOnComments,
            NotifyOnLikes = notifyOnLikes,
            NotifyOnShares = notifyOnShares,
            NotifyOnUpdates = notifyOnUpdates
        };
    }

    public void UpdatePreferences(bool? notifyOnComments, bool? notifyOnLikes, bool? notifyOnShares, bool? notifyOnUpdates)
    {
        if (notifyOnComments.HasValue)
        {
            NotifyOnComments = notifyOnComments.Value;
        }

        if (notifyOnLikes.HasValue)
        {
            NotifyOnLikes = notifyOnLikes.Value;
        }

        if (notifyOnShares.HasValue)
        {
            NotifyOnShares = notifyOnShares.Value;
        }

        if (notifyOnUpdates.HasValue)
        {
            NotifyOnUpdates = notifyOnUpdates.Value;
        }

        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Reusable tag for categorizing posts
/// </summary>
[Table("post_tags")]
[Index(nameof(Name), IsUnique = true)]
[Index(nameof(Category))]
[Index(nameof(UsageCount))]
public class PostTag : EntityBase
{
    [Required]
    [MaxLength(50)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; private set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; private set; }

    /// <summary>Category grouping: general, technology, art, design, etc.</summary>
    [MaxLength(50)]
    public string Category { get; private set; } = "general";

    /// <summary>Color for UI display (hex code)</summary>
    [MaxLength(7)]
    public string? Color { get; private set; }

    /// <summary>Number of posts using this tag</summary>
    public int UsageCount { get; private set; }

    /// <summary>Whether to feature this tag in discovery</summary>
    public bool IsFeatured { get; private set; }

    private PostTag() { } // EF Core

    public static PostTag Create(string name, string? displayName = null, string category = "general", string? description = null, string? color = null)
    {
        return new PostTag
        {
            Id = Guid.NewGuid(),
            Name = name.ToLowerInvariant().Trim(),
            DisplayName = displayName ?? name,
            Description = description,
            Category = category,
            Color = color,
            UsageCount = 0,
            IsFeatured = false
        };
    }

    public void IncrementUsage() { UsageCount++; UpdatedAt = SystemClock.UtcNow; }
    public void DecrementUsage() { if (UsageCount > 0) { UsageCount--; } UpdatedAt = SystemClock.UtcNow; }
    public void SetFeatured(bool featured) { IsFeatured = featured; UpdatedAt = SystemClock.UtcNow; }
}

/// <summary>
/// Many-to-many relationship between posts and tags
/// </summary>
[Table("post_tag_assignments")]
[Index(nameof(PostId))]
[Index(nameof(TagId))]
[Index(nameof(PostId), nameof(TagId), IsUnique = true)]
public class PostTagAssignment : EntityBase
{
    public Guid PostId { get; private set; }
    public Guid TagId { get; private set; }

    /// <summary>Display order for the tag on this post</summary>
    public int Order { get; private set; }

    private PostTagAssignment() { } // EF Core

    public static PostTagAssignment Create(Guid postId, Guid tagId, int order = 0)
    {
        return new PostTagAssignment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            TagId = tagId,
            Order = order
        };
    }
}

/// <summary>
/// Tracks individual post views for analytics
/// </summary>
[Table("post_views")]
[Index(nameof(PostId))]
[Index(nameof(UserId))]
[Index(nameof(ViewedAt))]
[Index(nameof(IpAddress))]
public class PostView : EntityBase
{
    public Guid PostId { get; private set; }
    public Guid? UserId { get; private set; }

    public DateTime ViewedAt { get; private set; } = SystemClock.UtcNow;

    /// <summary>IP address for anonymous tracking</summary>
    [MaxLength(45)]
    public string? IpAddress { get; private set; }

    /// <summary>User agent string</summary>
    [MaxLength(500)]
    public string? UserAgent { get; private set; }

    /// <summary>Referrer URL</summary>
    [MaxLength(500)]
    public string? Referrer { get; private set; }

    /// <summary>Time spent viewing (in seconds)</summary>
    public int DurationSeconds { get; private set; }

    /// <summary>Whether the user engaged (scrolled, clicked links, etc.)</summary>
    public bool IsEngaged { get; private set; }

    private PostView() { } // EF Core

    public static PostView Create(
        Guid postId,
        Guid? userId,
        string? ipAddress = null,
        string? userAgent = null,
        string? referrer = null)
    {
        return new PostView
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            ViewedAt = SystemClock.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Referrer = referrer,
            DurationSeconds = 0,
            IsEngaged = false
        };
    }

    public void UpdateDuration(int seconds, bool engaged = false)
    {
        DurationSeconds = seconds;
        IsEngaged = engaged;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Represents a like/reaction on a post
/// </summary>
[Table("post_likes")]
[Index(nameof(PostId))]
[Index(nameof(UserId))]
[Index(nameof(PostId), nameof(UserId), IsUnique = true)]
public class PostLike : EntityBase
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Type of reaction: like, love, celebrate, support, curious</summary>
    [MaxLength(20)]
    public string ReactionType { get; private set; } = "like";

    private PostLike() { } // EF Core

    public static PostLike Create(Guid postId, Guid userId, string reactionType = "like")
    {
        return new PostLike
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            ReactionType = reactionType
        };
    }

    public void ChangeReactionType(string newType)
    {
        ReactionType = newType;
        UpdatedAt = SystemClock.UtcNow;
    }
}
