using GameGuild.Social.Follows;
using GameGuild.Social.Posts;
using GameGuild.Social.Profiles;
using GameGuild.Social.Reactions;
using GameGuild.TestingLab;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Feed;

internal static class FeedProjection
{
    internal sealed record Candidate(Post? Post, TestingSession? Session, DateTime SortAt);

    internal static async Task<IReadOnlyList<SocialFeedItemDto>> ProjectAsync(
        IApplicationDbContext context,
        Guid viewerId,
        IReadOnlyList<Candidate> candidates,
        IReadOnlyCollection<Guid> followedAuthorIds,
        CancellationToken cancellationToken)
    {
        var posts = candidates
            .Where(candidate => candidate.Post is not null)
            .Select(candidate => candidate.Post!)
            .ToList();
        var postIds = posts.Select(post => post.Id).ToList();
        var sourceIds = posts
            .Where(post => post.RepostOfPostId.HasValue)
            .Select(post => post.RepostOfPostId!.Value)
            .Distinct()
            .ToList();
        var sourcePosts = await context.Set<Post>()
            .AsNoTracking()
            .Where(post => sourceIds.Contains(post.Id) &&
                           post.DeletedAt == null &&
                           post.Visibility == PostVisibility.Public)
            .ToDictionaryAsync(post => post.Id, cancellationToken)
            .ConfigureAwait(false);
        var authorIds = posts.Select(post => post.AuthorId)
            .Concat(sourcePosts.Values.Select(post => post.AuthorId))
            .Concat(candidates
                .Where(candidate => candidate.Session is not null)
                .Select(candidate => candidate.Session!.ManagerId))
            .Distinct()
            .ToList();
        var profiles = await context.Set<SocialProfile>()
            .AsNoTracking()
            .Where(profile => authorIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken)
            .ConfigureAwait(false);
        var reactions = await context.Set<Reaction>()
            .AsNoTracking()
            .Where(reaction => postIds.Contains(reaction.TargetId) && reaction.TargetType == ReactionTargetType.Post)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var savedPostIds = (await context.Set<SavedPost>()
                .AsNoTracking()
                .Where(saved => saved.UserId == viewerId && postIds.Contains(saved.PostId))
                .Select(saved => saved.PostId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
        var commentPostIds = await context.Set<PostComment>()
            .AsNoTracking()
            .Where(comment => postIds.Contains(comment.PostId) && comment.DeletedAt == null)
            .Select(comment => comment.PostId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var repostSourceIds = await context.Set<Post>()
            .AsNoTracking()
            .Where(post => post.RepostOfPostId.HasValue &&
                           postIds.Contains(post.RepostOfPostId.Value) &&
                           post.DeletedAt == null)
            .Select(post => post.RepostOfPostId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var tagRows = await (from assignment in context.Set<PostTagAssignment>().AsNoTracking()
                join postTag in context.Set<PostTag>().AsNoTracking() on assignment.TagId equals postTag.Id
                where postIds.Contains(assignment.PostId)
                orderby assignment.Order
                select new { assignment.PostId, postTag.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var reactionCounts = reactions
            .GroupBy(reaction => reaction.TargetId)
            .ToDictionary(group => group.Key, group => group.Count());
        var viewerReactions = reactions
            .Where(reaction => reaction.UserId == viewerId)
            .GroupBy(reaction => reaction.TargetId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(reaction => reaction.UpdatedAt).First().Type.ToString());
        var commentCounts = commentPostIds
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        var repostCounts = repostSourceIds
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        var tags = tagRows
            .GroupBy(row => row.PostId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.Name).ToList());
        var followed = followedAuthorIds.ToHashSet();

        return candidates
            .Select(candidate => candidate.Session is not null
                ? MapSession(candidate.Session, candidate.SortAt, profiles, followed)
                : MapPost(
                    candidate.Post!,
                    candidate.SortAt,
                    viewerId,
                    profiles,
                    sourcePosts,
                    reactionCounts,
                    viewerReactions,
                    savedPostIds,
                    commentCounts,
                    repostCounts,
                    tags,
                    followed))
            .ToList();
    }

    private static SocialFeedItemDto MapSession(
        TestingSession session,
        DateTime sortAt,
        IReadOnlyDictionary<Guid, SocialProfile> profiles,
        IReadOnlySet<Guid> followed)
    {
        return new SocialFeedItemDto(
            session.Id,
            SocialFeedItemKind.TestingSession,
            sortAt,
            MapAuthor(session.ManagerId, profiles),
            null,
            new TestingSessionFeedDto(
                session.SessionName,
                session.StartTime,
                session.EndTime,
                session.EventSlot?.Mode.ToString() ?? "Unspecified",
                session.Status.ToString(),
                session.MaxTesters,
                session.RegisteredTesterCount,
                Math.Max(0, session.MaxTesters - session.RegisteredTesterCount)),
            new FeedEngagementDto(0, 0, 0, 0),
            new FeedViewerStateDto(null, false, followed.Contains(session.ManagerId), false, false),
            Array.Empty<string>());
    }

    private static SocialFeedItemDto MapPost(
        Post post,
        DateTime sortAt,
        Guid viewerId,
        IReadOnlyDictionary<Guid, SocialProfile> profiles,
        IReadOnlyDictionary<Guid, Post> sourcePosts,
        IReadOnlyDictionary<Guid, int> reactionCounts,
        IReadOnlyDictionary<Guid, string> viewerReactions,
        IReadOnlySet<Guid> savedPostIds,
        IReadOnlyDictionary<Guid, int> commentCounts,
        IReadOnlyDictionary<Guid, int> repostCounts,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> tags,
        IReadOnlySet<Guid> followed)
    {
        OriginalPostDto? original = null;
        if (post.RepostOfPostId is Guid sourceId && sourcePosts.TryGetValue(sourceId, out var source))
        {
            original = new OriginalPostDto(
                source.Id,
                MapAuthor(source.AuthorId, profiles),
                source.Content,
                source.MediaUrl,
                source.MediaType?.ToString(),
                source.CreatedAt);
        }

        return new SocialFeedItemDto(
            post.Id,
            post.RepostOfPostId.HasValue ? SocialFeedItemKind.Repost : SocialFeedItemKind.Post,
            sortAt,
            MapAuthor(post.AuthorId, profiles),
            new SocialPostContentDto(
                post.Content,
                post.MediaUrl,
                post.MediaType?.ToString(),
                post.Visibility.ToString(),
                post.IsEdited,
                post.EditedAt,
                original),
            null,
            new FeedEngagementDto(
                reactionCounts.GetValueOrDefault(post.Id),
                commentCounts.GetValueOrDefault(post.Id),
                repostCounts.GetValueOrDefault(post.Id),
                post.ViewsCount),
            new FeedViewerStateDto(
                viewerReactions.GetValueOrDefault(post.Id),
                savedPostIds.Contains(post.Id),
                followed.Contains(post.AuthorId),
                post.AuthorId == viewerId,
                post.AuthorId == viewerId),
            tags.GetValueOrDefault(post.Id) ?? Array.Empty<string>());
    }

    private static FeedAuthorDto MapAuthor(
        Guid userId,
        IReadOnlyDictionary<Guid, SocialProfile> profiles)
    {
        profiles.TryGetValue(userId, out var profile);
        var handle = string.IsNullOrWhiteSpace(profile?.Handle)
            ? "member-" + userId.ToString("N")[..8]
            : profile.Handle;
        var displayName = string.IsNullOrWhiteSpace(profile?.DisplayName)
            ? "Game Guild member"
            : profile.DisplayName;

        return new FeedAuthorDto(
            userId,
            handle,
            displayName,
            profile?.AvatarUrl,
            profile?.VerifiedAt.HasValue == true);
    }
}
