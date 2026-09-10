using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Post engagement: likes, pins, shares, views, statistics, engagement tracking, and following
/// </summary>
public class PostEngagementService : IPostEngagementService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PostEngagementService> _logger;

    private static class PostErrors
    {
        public static Error NotFound => Error.NotFound("Post.NotFound", "Post not found");
        public static Error Forbidden => Error.Forbidden("Post.Forbidden", "This action is not allowed for the current user");
        public static Error StatisticsNotFound => Error.NotFound("PostStatistics.NotFound", "Statistics not found for post");
        public static Error ViewNotFound => Error.NotFound("PostView.NotFound", "View not found");
        public static Error NotFollowing => Error.NotFound("PostFollower.NotFound", "Not following this post");
    }

    public PostEngagementService(IApplicationDbContext context, ILogger<PostEngagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Post Interactions

    public async Task<Result<bool>> TogglePostLikeAsync(Guid postId, Guid userId, string reactionType = "like", CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure<bool>(PostErrors.NotFound);

        var existingLike = await _context.Set<PostLike>()
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (existingLike is not null)
        {
            _context.Set<PostLike>().Remove(existingLike);
            post.DecrementLikes();
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(false);
        }
        else
        {
            var like = PostLike.Create(postId, userId, reactionType);
            _context.Set<PostLike>().Add(like);
            post.IncrementLikes();
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(true);
        }
    }

    public async Task<Result<bool>> TogglePostPinAsync(Guid postId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure<bool>(PostErrors.NotFound);

        if (post.AuthorId != actorId)
            return Result.Failure<bool>(PostErrors.Forbidden);

        if (post.IsPinned)
            post.Unpin();
        else
            post.Pin();

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(post.IsPinned);
    }

    public async Task<Result<Post>> CreateRepostAsync(
        Guid sourcePostId,
        Guid actorId,
        string? content = null,
        CancellationToken cancellationToken = default)
    {
        var source = await _context.Set<Post>()
            .FirstOrDefaultAsync(
                p => p.Id == sourcePostId && p.DeletedAt == null && p.Visibility == PostVisibility.Public,
                cancellationToken)
            .ConfigureAwait(false);

        if (source is null)
            return Result.Failure<Post>(PostErrors.NotFound);

        var existing = await _context.Set<Post>()
            .FirstOrDefaultAsync(
                p => p.AuthorId == actorId && p.RepostOfPostId == sourcePostId && p.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
            return Result.Success(existing);

        var repost = Post.CreateRepost(actorId, source, content);
        _context.Set<Post>().Add(repost);
        _context.Set<PostStatistics>().Add(PostStatistics.Create(repost.Id));
        source.IncrementShares();

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(repost);
    }

    public async Task<Result> SharePostAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure(PostErrors.NotFound);

        post.IncrementShares();

        var statistics = await _context.Set<PostStatistics>()
            .FirstOrDefaultAsync(s => s.PostId == postId, cancellationToken).ConfigureAwait(false);

        statistics?.IncrementExternalShares();

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<PostStatistics>> GetPostStatisticsAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var statistics = await _context.Set<PostStatistics>()
            .FirstOrDefaultAsync(s => s.PostId == postId, cancellationToken).ConfigureAwait(false);

        return statistics is null
            ? Result.Failure<PostStatistics>(PostErrors.StatisticsNotFound)
            : Result.Success(statistics);
    }

    public async Task<Result> RecordPostViewAsync(
        Guid postId,
        Guid? userId,
        string? ipAddress = null,
        string? userAgent = null,
        string? referrer = null,
        CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure(PostErrors.NotFound);

        bool isUnique;
        if (userId.HasValue)
        {
            isUnique = !await _context.Set<PostView>()
                .AnyAsync(v => v.PostId == postId && v.UserId == userId.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            isUnique = !await _context.Set<PostView>()
                .AnyAsync(v => v.PostId == postId && v.IpAddress == ipAddress, cancellationToken).ConfigureAwait(false);
        }

        var view = PostView.Create(postId, userId, ipAddress, userAgent, referrer);
        _context.Set<PostView>().Add(view);

        post.IncrementViews();

        var statistics = await _context.Set<PostStatistics>()
            .FirstOrDefaultAsync(s => s.PostId == postId, cancellationToken).ConfigureAwait(false);

        statistics?.IncrementViews(isUnique);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> UpdateViewEngagementAsync(Guid viewId, int durationSeconds, bool engaged = false, CancellationToken cancellationToken = default)
    {
        var view = await _context.Set<PostView>()
            .FirstOrDefaultAsync(v => v.Id == viewId, cancellationToken).ConfigureAwait(false);

        if (view is null)
            return Result.Failure(PostErrors.ViewNotFound);

        view.UpdateDuration(durationSeconds, engaged);

        var statistics = await _context.Set<PostStatistics>()
            .FirstOrDefaultAsync(s => s.PostId == view.PostId, cancellationToken).ConfigureAwait(false);

        statistics?.UpdateEngagementTime(durationSeconds);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    #endregion

    #region Statistics

    public async Task<Result> RecalculateStatisticsAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure(PostErrors.NotFound);

        var statistics = await _context.Set<PostStatistics>()
            .FirstOrDefaultAsync(s => s.PostId == postId, cancellationToken).ConfigureAwait(false);

        if (statistics is null)
        {
            statistics = PostStatistics.Create(postId);
            _context.Set<PostStatistics>().Add(statistics);
        }

        var hoursOld = (int)(SystemClock.UtcNow - post.CreatedAt).TotalHours;
        statistics.RecalculateScores(post.LikesCount, post.CommentsCount, post.SharesCount, hoursOld);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<int>> RecalculateAllTrendingScoresAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _context.Set<Post>()
            .Where(p => p.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var statistics = await _context.Set<PostStatistics>()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var statsDict = statistics.ToDictionary(s => s.PostId);
        var count = 0;

        foreach (var post in posts)
        {
            if (!statsDict.TryGetValue(post.Id, out var stats))
            {
                stats = PostStatistics.Create(post.Id);
                _context.Set<PostStatistics>().Add(stats);
                statsDict[post.Id] = stats;
            }

            var hoursOld = (int)(SystemClock.UtcNow - post.CreatedAt).TotalHours;
            stats.RecalculateScores(post.LikesCount, post.CommentsCount, post.SharesCount, hoursOld);
            count++;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Recalculated trending scores for {Count} posts", count);

        return Result.Success(count);
    }

    #endregion

    #region Post Following

    public async Task<Result<PostFollower>> FollowPostAsync(
        Guid postId,
        Guid userId,
        bool notifyOnComments = true,
        bool notifyOnLikes = false,
        bool notifyOnShares = false,
        bool notifyOnUpdates = true,
        CancellationToken cancellationToken = default)
    {
        var post = await _context.Set<Post>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (post is null)
            return Result.Failure<PostFollower>(PostErrors.NotFound);

        var existingFollow = await _context.Set<PostFollower>()
            .FirstOrDefaultAsync(f => f.PostId == postId && f.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (existingFollow is not null)
            return Result.Success(existingFollow);

        var follower = PostFollower.Create(postId, userId, notifyOnComments, notifyOnLikes, notifyOnShares, notifyOnUpdates);
        _context.Set<PostFollower>().Add(follower);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(follower);
    }

    public async Task<Result> UnfollowPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        var follow = await _context.Set<PostFollower>()
            .FirstOrDefaultAsync(f => f.PostId == postId && f.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (follow is null)
            return Result.Failure(PostErrors.NotFollowing);

        _context.Set<PostFollower>().Remove(follow);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> UpdateFollowPreferencesAsync(
        Guid postId,
        Guid userId,
        bool? notifyOnComments = null,
        bool? notifyOnLikes = null,
        bool? notifyOnShares = null,
        bool? notifyOnUpdates = null,
        CancellationToken cancellationToken = default)
    {
        var follow = await _context.Set<PostFollower>()
            .FirstOrDefaultAsync(f => f.PostId == postId && f.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (follow is null)
            return Result.Failure(PostErrors.NotFollowing);

        follow.UpdatePreferences(notifyOnComments, notifyOnLikes, notifyOnShares, notifyOnUpdates);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<PostFollower>>> GetPostFollowersAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var followers = await _context.Set<PostFollower>()
            .Where(f => f.PostId == postId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IEnumerable<PostFollower>>(followers);
    }

    public async Task<Result<bool>> IsFollowingPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        var isFollowing = await _context.Set<PostFollower>()
            .AnyAsync(f => f.PostId == postId && f.UserId == userId, cancellationToken).ConfigureAwait(false);

        return Result.Success(isFollowing);
    }

    #endregion
}
