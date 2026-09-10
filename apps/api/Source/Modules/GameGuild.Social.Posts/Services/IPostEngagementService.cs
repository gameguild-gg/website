namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Service interface for post engagement: likes, pins, shares, views, statistics, and engagement tracking
/// </summary>
public interface IPostEngagementService
{
    /// <summary>Toggles a like on a post</summary>
    Task<Result<bool>> TogglePostLikeAsync(Guid postId, Guid userId, string reactionType = "like", CancellationToken cancellationToken = default);

    /// <summary>Toggles pin status on a post</summary>
    Task<Result<bool>> TogglePostPinAsync(Guid postId, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>Creates one idempotent repost per user and source post</summary>
    Task<Result<Post>> CreateRepostAsync(Guid sourcePostId, Guid actorId, string? content = null, CancellationToken cancellationToken = default);

    /// <summary>Records a share of the post</summary>
    Task<Result> SharePostAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>Gets statistics for a post</summary>
    Task<Result<PostStatistics>> GetPostStatisticsAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>Records a view of the post</summary>
    Task<Result> RecordPostViewAsync(
        Guid postId,
        Guid? userId,
        string? ipAddress = null,
        string? userAgent = null,
        string? referrer = null,
        CancellationToken cancellationToken = default);

    /// <summary>Updates view engagement (duration, interaction)</summary>
    Task<Result> UpdateViewEngagementAsync(Guid viewId, int durationSeconds, bool engaged = false, CancellationToken cancellationToken = default);

    /// <summary>Recalculates statistics for a post</summary>
    Task<Result> RecalculateStatisticsAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>Recalculates trending scores for all posts</summary>
    Task<Result<int>> RecalculateAllTrendingScoresAsync(CancellationToken cancellationToken = default);

    /// <summary>Follows a post for notifications</summary>
    Task<Result<PostFollower>> FollowPostAsync(
        Guid postId,
        Guid userId,
        bool notifyOnComments = true,
        bool notifyOnLikes = false,
        bool notifyOnShares = false,
        bool notifyOnUpdates = true,
        CancellationToken cancellationToken = default);

    /// <summary>Unfollows a post</summary>
    Task<Result> UnfollowPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates notification preferences for a post follow</summary>
    Task<Result> UpdateFollowPreferencesAsync(
        Guid postId,
        Guid userId,
        bool? notifyOnComments = null,
        bool? notifyOnLikes = null,
        bool? notifyOnShares = null,
        bool? notifyOnUpdates = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets followers for a post</summary>
    Task<Result<IEnumerable<PostFollower>>> GetPostFollowersAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>Checks if user is following a post</summary>
    Task<Result<bool>> IsFollowingPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);
}
