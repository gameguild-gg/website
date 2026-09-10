using GameGuild.Social.Follows;
using GameGuild.Social.Follows.Services;
using GameGuild.Social.Posts;
using GameGuild.Social.Profiles;
using GameGuild.Social.Reactions;
using GameGuild.TestingLab;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Feed;

public interface ISocialFeedQueryService
{
    Task<SocialFeedPageDto> GetAsync(
        Guid viewerId,
        FeedScope scope,
        string? cursor,
        int take,
        string? tag,
        CancellationToken cancellationToken = default);

    Task<SocialFeedItemDto?> GetPostAsync(
        Guid viewerId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<SocialFeedProfileDto?> GetProfileByUserAsync(
        Guid viewerId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SocialFeedProfileDto?> GetProfileByHandleAsync(
        Guid viewerId,
        string handle,
        CancellationToken cancellationToken = default);
}

public sealed class SocialFeedQueryService(IApplicationDbContext context) : ISocialFeedQueryService
{
    private sealed record FeedCandidate(
        Post? Post,
        TestingSession? Session,
        DateTime SortAt,
        Guid CursorId);

    public async Task<SocialFeedPageDto> GetAsync(
        Guid viewerId,
        FeedScope scope,
        string? cursor,
        int take,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty)
            throw new ArgumentException("An authenticated viewer is required.", nameof(viewerId));

        take = Math.Clamp(take, 1, 30);
        FeedCursor? decodedCursor = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!FeedCursor.TryDecode(cursor, out var parsed))
                throw new InvalidFeedCursorException();
            decodedCursor = parsed;
        }

        var now = DateTime.UtcNow;
        var blockedAuthorIds = await GetBlockedAuthorIdsAsync(viewerId, cancellationToken).ConfigureAwait(false);
        var mutedAuthorIds = await context.Set<Mute>()
            .AsNoTracking()
            .Where(mute => mute.MuterId == viewerId && (!mute.ExpiresAt.HasValue || mute.ExpiresAt > now))
            .Select(mute => mute.MutedId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var excludedAuthors = blockedAuthorIds.Concat(mutedAuthorIds).ToHashSet();
        var followedAuthorIds = await context.Set<Follow>()
            .AsNoTracking()
            .Where(follow => follow.FollowerId == viewerId && follow.FollowedEntityType == FollowableEntityTypes.User)
            .Select(follow => follow.FollowedEntityId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidates = new List<FeedCandidate>();
        if (scope != FeedScope.Community)
        {
            candidates.AddRange(await GetPostCandidatesAsync(
                viewerId,
                scope,
                decodedCursor,
                take + 1,
                tag,
                excludedAuthors,
                followedAuthorIds,
                cancellationToken).ConfigureAwait(false));
        }

        if ((scope is FeedScope.ForYou or FeedScope.Community) && string.IsNullOrWhiteSpace(tag))
        {
            candidates.AddRange(await GetSessionCandidatesAsync(
                decodedCursor,
                take + 1,
                excludedAuthors,
                cancellationToken).ConfigureAwait(false));
        }

        var ordered = candidates
            .OrderByDescending(candidate => candidate.SortAt)
            .ThenByDescending(candidate => candidate.CursorId)
            .Take(take + 1)
            .ToList();
        var hasMore = ordered.Count > take;
        var pageCandidates = ordered.Take(take).ToList();
        var items = await ProjectAsync(viewerId, pageCandidates, followedAuthorIds, cancellationToken)
            .ConfigureAwait(false);
        var nextCursor = hasMore && pageCandidates.Count > 0
            ? new FeedCursor(pageCandidates[^1].SortAt, pageCandidates[^1].CursorId).Encode()
            : null;

        return new SocialFeedPageDto(items, nextCursor);
    }

    public async Task<SocialFeedItemDto?> GetPostAsync(
        Guid viewerId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty)
            throw new ArgumentException("An authenticated viewer is required.", nameof(viewerId));

        var excludedAuthors = await GetBlockedAuthorIdsAsync(viewerId, cancellationToken).ConfigureAwait(false);
        var mutedAuthorIds = await context.Set<Mute>()
            .AsNoTracking()
            .Where(mute => mute.MuterId == viewerId && (!mute.ExpiresAt.HasValue || mute.ExpiresAt > DateTime.UtcNow))
            .Select(mute => mute.MutedId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        excludedAuthors.UnionWith(mutedAuthorIds);

        var followedAuthorIds = await context.Set<Follow>()
            .AsNoTracking()
            .Where(follow => follow.FollowerId == viewerId && follow.FollowedEntityType == FollowableEntityTypes.User)
            .Select(follow => follow.FollowedEntityId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var post = await context.Set<Post>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == postId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (post is null || excludedAuthors.Contains(post.AuthorId)) return null;
        if (post.AuthorId != viewerId &&
            post.Visibility != PostVisibility.Public &&
            !(post.Visibility == PostVisibility.Followers && followedAuthorIds.Contains(post.AuthorId)))
            return null;

        var items = await FeedProjection.ProjectAsync(
            context,
            viewerId,
            [new FeedProjection.Candidate(post, null, post.CreatedAt)],
            followedAuthorIds,
            cancellationToken).ConfigureAwait(false);
        return items.SingleOrDefault();
    }

    public async Task<SocialFeedProfileDto?> GetProfileByUserAsync(
        Guid viewerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty)
            throw new ArgumentException("An authenticated viewer is required.", nameof(viewerId));

        var profile = await context.Set<SocialProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        return await ProjectProfileAsync(viewerId, profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SocialFeedProfileDto?> GetProfileByHandleAsync(
        Guid viewerId,
        string handle,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty)
            throw new ArgumentException("An authenticated viewer is required.", nameof(viewerId));

        var normalizedHandle = SocialProfile.NormalizeHandle(handle);
        var profile = await context.Set<SocialProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Handle == normalizedHandle && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        return await ProjectProfileAsync(viewerId, profile, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SocialFeedProfileDto?> ProjectProfileAsync(
        Guid viewerId,
        SocialProfile? profile,
        CancellationToken cancellationToken)
    {
        if (profile is null ||
            (profile.UserId != viewerId && profile.Visibility != ProfileVisibility.Public))
            return null;

        var isBlocked = await context.Set<Block>()
            .AsNoTracking()
            .AnyAsync(block =>
                (block.BlockerId == viewerId && block.BlockedId == profile.UserId) ||
                (block.BlockerId == profile.UserId && block.BlockedId == viewerId), cancellationToken)
            .ConfigureAwait(false);
        if (isBlocked) return null;

        var followerCount = await context.Set<Follow>()
            .AsNoTracking()
            .CountAsync(follow => follow.FollowedEntityId == profile.UserId &&
                                  follow.FollowedEntityType == FollowableEntityTypes.User, cancellationToken)
            .ConfigureAwait(false);
        var followingCount = await context.Set<Follow>()
            .AsNoTracking()
            .CountAsync(follow => follow.FollowerId == profile.UserId &&
                                  follow.FollowedEntityType == FollowableEntityTypes.User, cancellationToken)
            .ConfigureAwait(false);
        var postCount = await context.Set<Post>()
            .AsNoTracking()
            .CountAsync(post => post.AuthorId == profile.UserId && post.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        var isFollowing = await context.Set<Follow>()
            .AsNoTracking()
            .AnyAsync(follow => follow.FollowerId == viewerId &&
                                follow.FollowedEntityId == profile.UserId &&
                                follow.FollowedEntityType == FollowableEntityTypes.User, cancellationToken)
            .ConfigureAwait(false);

        return new SocialFeedProfileDto(
            profile.Id,
            profile.UserId,
            profile.Handle,
            profile.DisplayName,
            profile.Bio,
            profile.AvatarUrl,
            profile.BannerUrl,
            profile.Headline,
            profile.Location,
            profile.TimeZone,
            profile.WebsiteUrl,
            profile.AvailabilityStatus.ToString(),
            profile.VerifiedAt.HasValue,
            followerCount,
            followingCount,
            postCount,
            profile.ProjectCount,
            isFollowing);
    }

    private async Task<IReadOnlyList<FeedCandidate>> GetPostCandidatesAsync(
        Guid viewerId,
        FeedScope scope,
        FeedCursor? cursor,
        int take,
        string? tag,
        IReadOnlySet<Guid> excludedAuthors,
        IReadOnlyCollection<Guid> followedAuthorIds,
        CancellationToken cancellationToken)
    {
        var taggedPostIds = await GetTaggedPostIdsAsync(tag, cancellationToken).ConfigureAwait(false);

        if (scope == FeedScope.Saved)
        {
            var query = from saved in context.Set<SavedPost>().AsNoTracking()
                join post in context.Set<Post>().AsNoTracking() on saved.PostId equals post.Id
                where saved.UserId == viewerId && post.DeletedAt == null
                select new { Saved = saved, Post = post };

            query = query.Where(row =>
                !excludedAuthors.Contains(row.Post.AuthorId) &&
                (row.Post.AuthorId == viewerId ||
                 row.Post.Visibility == PostVisibility.Public ||
                 (row.Post.Visibility == PostVisibility.Followers && followedAuthorIds.Contains(row.Post.AuthorId))));

            if (taggedPostIds is not null)
                query = query.Where(row => taggedPostIds.Contains(row.Post.Id));

            if (cursor is { } savedCursor)
            {
                query = query.Where(row =>
                    row.Saved.CreatedAt < savedCursor.SortAt ||
                    (row.Saved.CreatedAt == savedCursor.SortAt && row.Saved.Id.CompareTo(savedCursor.Id) < 0));
            }

            var rows = await query
                .OrderByDescending(row => row.Saved.CreatedAt)
                .ThenByDescending(row => row.Saved.Id)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return rows
                .Select(row => new FeedCandidate(row.Post, null, row.Saved.CreatedAt, row.Saved.Id))
                .ToList();
        }

        var posts = context.Set<Post>()
            .AsNoTracking()
            .Where(post => post.DeletedAt == null && !excludedAuthors.Contains(post.AuthorId));

        if (scope == FeedScope.Following)
            posts = posts.Where(post => followedAuthorIds.Contains(post.AuthorId));

        posts = posts.Where(post =>
            post.AuthorId == viewerId ||
            post.Visibility == PostVisibility.Public ||
            (post.Visibility == PostVisibility.Followers && followedAuthorIds.Contains(post.AuthorId)));

        if (taggedPostIds is not null)
            posts = posts.Where(post => taggedPostIds.Contains(post.Id));

        if (cursor is { } postCursor)
        {
            posts = posts.Where(post =>
                post.CreatedAt < postCursor.SortAt ||
                (post.CreatedAt == postCursor.SortAt && post.Id.CompareTo(postCursor.Id) < 0));
        }

        return (await posts
                .OrderByDescending(post => post.CreatedAt)
                .ThenByDescending(post => post.Id)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(post => new FeedCandidate(post, null, post.CreatedAt, post.Id))
            .ToList();
    }

    private async Task<IReadOnlyList<FeedCandidate>> GetSessionCandidatesAsync(
        FeedCursor? cursor,
        int take,
        IReadOnlySet<Guid> excludedAuthors,
        CancellationToken cancellationToken)
    {
        var sessions = context.Set<TestingSession>()
            .AsNoTracking()
            .Include(session => session.EventSlot)
            .Where(session => session.DeletedAt == null &&
                              (session.Status == SessionStatus.Scheduled || session.Status == SessionStatus.Active) &&
                              !excludedAuthors.Contains(session.ManagerId));

        if (cursor is { } sessionCursor)
        {
            sessions = sessions.Where(session =>
                session.CreatedAt < sessionCursor.SortAt ||
                (session.CreatedAt == sessionCursor.SortAt && session.Id.CompareTo(sessionCursor.Id) < 0));
        }

        return (await sessions
                .OrderByDescending(session => session.CreatedAt)
                .ThenByDescending(session => session.Id)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(session => new FeedCandidate(null, session, session.CreatedAt, session.Id))
            .ToList();
    }

    private async Task<HashSet<Guid>?> GetTaggedPostIdsAsync(string? tag, CancellationToken cancellationToken)
    {
        var normalized = NormalizeTag(tag);
        if (normalized is null)
            return null;

        var tagIds = await context.Set<PostTag>()
            .AsNoTracking()
            .Where(postTag => postTag.Name == normalized)
            .Select(postTag => postTag.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (await context.Set<PostTagAssignment>()
                .AsNoTracking()
                .Where(assignment => tagIds.Contains(assignment.TagId))
                .Select(assignment => assignment.PostId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
    }

    private async Task<HashSet<Guid>> GetBlockedAuthorIdsAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        var blocks = await context.Set<Block>()
            .AsNoTracking()
            .Where(block => block.BlockerId == viewerId || block.BlockedId == viewerId)
            .Select(block => new { block.BlockerId, block.BlockedId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return blocks
            .Select(block => block.BlockerId == viewerId ? block.BlockedId : block.BlockerId)
            .ToHashSet();
    }

    private static string? NormalizeTag(string? tag)
    {
        var normalized = tag?.Trim().TrimStart('#').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private Task<IReadOnlyList<SocialFeedItemDto>> ProjectAsync(
        Guid viewerId,
        IReadOnlyList<FeedCandidate> candidates,
        IReadOnlyCollection<Guid> followedAuthorIds,
        CancellationToken cancellationToken)
        => ProjectCandidatesAsync(viewerId, candidates, followedAuthorIds, cancellationToken);

    private async Task<IReadOnlyList<SocialFeedItemDto>> ProjectCandidatesAsync(
        Guid viewerId,
        IReadOnlyList<FeedCandidate> candidates,
        IReadOnlyCollection<Guid> followedAuthorIds,
        CancellationToken cancellationToken)
    {
        return await FeedProjection.ProjectAsync(
            context,
            viewerId,
            candidates.Select(candidate => new FeedProjection.Candidate(
                candidate.Post,
                candidate.Session,
                candidate.SortAt)).ToList(),
            followedAuthorIds,
            cancellationToken).ConfigureAwait(false);
    }
}
