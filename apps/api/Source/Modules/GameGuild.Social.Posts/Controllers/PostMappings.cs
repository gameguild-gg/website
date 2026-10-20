using GameGuild.Social.Assets.SocialMedia;

namespace GameGuild.Social.Posts.Controllers;

/// <summary>
/// Shared DTO mapping methods and response DTOs for post controllers.
/// </summary>
internal static class PostMappings
{
    public static PostDto MapToDto(Post post) => new()
    {
        Id = post.Id,
        AuthorId = post.AuthorId,
        TenantId = post.TenantId,
        Content = post.Content,
        MediaUrl = post.MediaUrl,
        MediaAssetReferenceId = post.MediaAssetReferenceId,
        MediaType = post.MediaType?.ToString(),
        Visibility = post.Visibility.ToString(),
        IsPinned = post.IsPinned,
        IsEdited = post.IsEdited,
        EditedAt = post.EditedAt,
        LikesCount = post.LikesCount,
        CommentsCount = post.CommentsCount,
        SharesCount = post.SharesCount,
        ViewsCount = post.ViewsCount,
        ReplyToPostId = post.ReplyToPostId,
        RepostOfPostId = post.RepostOfPostId,
        CreatedAt = post.CreatedAt,
        UpdatedAt = post.UpdatedAt
    };

    /// <summary>
    /// Public-read projection: managed media is hidden when its asset is not ready
    /// to serve (fail closed). Legacy posts (no asset reference) are returned unchanged.
    /// </summary>
    public static PostDto MapPublicDto(Post post, bool mediaReady)
    {
        var dto = MapToDto(post);
        if (post.MediaAssetReferenceId.HasValue && !mediaReady)
        {
            return dto with { MediaUrl = null, MediaAssetReferenceId = null, MediaType = null };
        }

        return dto;
    }

    /// <summary>
    /// Batch public-read projection: resolves managed-media readiness once per page.
    /// </summary>
    public static async Task<IReadOnlyList<PostDto>> MapPublicDtosAsync(
        IEnumerable<Post> posts,
        ISocialMediaAssetService socialMediaAssets,
        CancellationToken cancellationToken = default)
    {
        var list = posts.ToList();

        var referenceIds = list
            .Where(p => p.MediaAssetReferenceId.HasValue)
            .Select(p => p.MediaAssetReferenceId!.Value)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<Guid, SocialMediaProcessingState> states = referenceIds.Count == 0
            ? new Dictionary<Guid, SocialMediaProcessingState>()
            : await socialMediaAssets.GetProcessingStatesAsync(referenceIds, cancellationToken).ConfigureAwait(false);

        return list
            .Select(p => PostMappings.MapPublicDto(
                p,
                !p.MediaAssetReferenceId.HasValue ||
                (states.TryGetValue(p.MediaAssetReferenceId.Value, out var state) &&
                 state == SocialMediaProcessingState.Ready)))
            .ToList();
    }

    public static CommentDto MapCommentToDto(PostComment comment) => new()
    {
        Id = comment.Id,
        PostId = comment.PostId,
        AuthorId = comment.AuthorId,
        ParentCommentId = comment.ParentCommentId,
        Content = comment.Content,
        IsEdited = comment.IsEdited,
        EditedAt = comment.EditedAt,
        LikesCount = comment.LikesCount,
        CreatedAt = comment.CreatedAt
    };

    public static TagDto MapTagToDto(PostTag tag) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        DisplayName = tag.DisplayName,
        Description = tag.Description,
        Category = tag.Category,
        Color = tag.Color,
        UsageCount = tag.UsageCount,
        IsFeatured = tag.IsFeatured
    };

    public static StatisticsDto MapStatisticsToDto(PostStatistics stats) => new()
    {
        PostId = stats.PostId,
        ViewsCount = stats.ViewsCount,
        UniqueViewersCount = stats.UniqueViewersCount,
        ExternalSharesCount = stats.ExternalSharesCount,
        AverageEngagementTime = stats.AverageEngagementTime,
        EngagementScore = stats.EngagementScore,
        TrendingScore = stats.TrendingScore,
        LastCalculatedAt = stats.LastCalculatedAt
    };

    public static FollowerDto MapFollowerToDto(PostFollower follower) => new()
    {
        PostId = follower.PostId,
        UserId = follower.UserId,
        NotifyOnComments = follower.NotifyOnComments,
        NotifyOnLikes = follower.NotifyOnLikes,
        NotifyOnShares = follower.NotifyOnShares,
        NotifyOnUpdates = follower.NotifyOnUpdates,
        CreatedAt = follower.CreatedAt
    };
}

#region Response DTOs

public sealed record PostDto
{
    public Guid Id { get; init; }
    public Guid AuthorId { get; init; }
    public Guid? TenantId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? MediaUrl { get; init; }
    public Guid? MediaAssetReferenceId { get; init; }
    public string? MediaType { get; init; }
    public string Visibility { get; init; } = "Public";
    public bool IsPinned { get; init; }
    public bool IsEdited { get; init; }
    public DateTime? EditedAt { get; init; }
    public int LikesCount { get; init; }
    public int CommentsCount { get; init; }
    public int SharesCount { get; init; }
    public int ViewsCount { get; init; }
    public Guid? ReplyToPostId { get; init; }
    public Guid? RepostOfPostId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record CommentDto
{
    public Guid Id { get; init; }
    public Guid PostId { get; init; }
    public Guid AuthorId { get; init; }
    public Guid? ParentCommentId { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsEdited { get; init; }
    public DateTime? EditedAt { get; init; }
    public int LikesCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record TagDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = "general";
    public string? Color { get; init; }
    public int UsageCount { get; init; }
    public bool IsFeatured { get; init; }
}

public sealed record StatisticsDto
{
    public Guid PostId { get; init; }
    public int ViewsCount { get; init; }
    public int UniqueViewersCount { get; init; }
    public int ExternalSharesCount { get; init; }
    public double AverageEngagementTime { get; init; }
    public double EngagementScore { get; init; }
    public double TrendingScore { get; init; }
    public DateTime LastCalculatedAt { get; init; }
}

public sealed record FollowerDto
{
    public Guid PostId { get; init; }
    public Guid UserId { get; init; }
    public bool NotifyOnComments { get; init; }
    public bool NotifyOnLikes { get; init; }
    public bool NotifyOnShares { get; init; }
    public bool NotifyOnUpdates { get; init; }
    public DateTime CreatedAt { get; init; }
}

#endregion
