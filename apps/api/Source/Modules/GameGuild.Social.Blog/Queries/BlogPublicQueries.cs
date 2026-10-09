using GameGuild.CQRS;
using GameGuild.Social.Blog.Services;
using GameGuild.Social.Follows.Services;
using GameGuild.Social.Profiles;
using GameGuild.Social.Reactions;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Blog.Queries;

/// <summary>
/// Feed/list card summary of a published post. Deliberately excludes body fields
/// (Content/JsonBody) — list surfaces never ship the body.
/// </summary>
public sealed record BlogPostSummaryDto(
    Guid Id,
    string PrimaryAuthorHandle,
    string PrimaryAuthorDisplayName,
    IReadOnlyList<string> CoAuthorHandles,
    string Title,
    string Slug,
    string? Excerpt,
    int ReadTimeMinutes,
    DateTime? PublishedAt,
    string? OgImageUrl,
    IReadOnlyList<string> Tags,
    int CommentCount,
    int ReactionCount);

/// <summary>Full public post detail: body, format, SEO fields, revision, and updated timestamp.</summary>
public sealed record BlogPostDetailDto(
    Guid Id,
    string PrimaryAuthorHandle,
    string PrimaryAuthorDisplayName,
    IReadOnlyList<string> CoAuthorHandles,
    string Title,
    string Slug,
    string? Excerpt,
    string Content,
    string? JsonBody,
    BlogContentFormat Format,
    int ReadTimeMinutes,
    DateTime? PublishedAt,
    string? OgImageUrl,
    IReadOnlyList<string> Tags,
    int CommentCount,
    int ReactionCount,
    bool AllowComments,
    string? MetaTitle,
    string? MetaDescription,
    string? CanonicalUrlOverride,
    string TwitterCard,
    string? StructuredDataOverride,
    int Revision,
    DateTime UpdatedAt,
    int ViewsCount);

/// <summary>A published comment with its author handle resolved via Social.Profiles.</summary>
public sealed record BlogCommentDto(
    Guid Id,
    Guid? ParentCommentId,
    string AuthorHandle,
    string? AuthorDisplayName,
    string Content,
    DateTime CreatedAt);

/// <summary>Page of published posts ordered newest-first (keyset on PublishedAt, then Id).</summary>
public sealed record BlogPostSummaryPage(IReadOnlyList<BlogPostSummaryDto> Items, bool HasMore);

/// <summary>Page of comments, oldest-first, with depth-1 replies flattened under their parents.</summary>
public sealed record BlogCommentPage(IReadOnlyList<BlogCommentDto> Items, bool HasMore);

/// <summary>Public global blog index (newest published first), keyset-paged.</summary>
public sealed record ListPublicBlogSummariesQuery(DateTime? BeforePublishedAt = null, Guid? BeforeId = null) : IQuery<BlogPostSummaryPage>;

/// <summary>Public author listing by handle (published only), keyset-paged.</summary>
public sealed record ListAuthorBlogSummariesQuery(string Handle, DateTime? BeforePublishedAt = null, Guid? BeforeId = null) : IQuery<BlogPostSummaryPage>;

/// <summary>
/// Public post detail by (handle, slug). Drafts, deleted posts, unknown handles, and unknown
/// slugs are indistinguishable (null) — no existence oracle (invariant #10).
/// </summary>
public sealed record GetPublicBlogPostDetailQuery(string Handle, string Slug) : IQuery<BlogPostDetailDto?>;

/// <summary>
/// Comments of a published post, oldest-first, paginated (page size 20). Soft-deleted comments
/// are excluded. When <paramref name="ViewerId"/> is provided, comments from users muted by the
/// viewer are hidden; anonymous viewers see all non-deleted comments.
/// </summary>
public sealed record ListBlogCommentsQuery(Guid PostId, Guid? ViewerId = null, DateTime? AfterCreatedAt = null, Guid? AfterId = null) : IQuery<BlogCommentPage>;

/// <summary>Canonical (handle, slug) route a stale URL resolves to, for permanent redirects.</summary>
public sealed record BlogRouteResolutionDto(string Handle, string Slug);

/// <summary>
/// Resolves a possibly-stale (handle, slug) route to the current canonical route (history-aware),
/// or null when no live post matches. The payload is redirect-target metadata only.
/// </summary>
public sealed record ResolveBlogRouteForRedirectQuery(string Handle, string Slug) : IQuery<BlogRouteResolutionDto?>;

/// <summary>
/// True when a live published post exists with the given id. Drafts, deleted posts, and unknown
/// ids are indistinguishable (false) — the controller maps that to 404 like the detail query.
/// </summary>
public sealed record IsBlogPostPublishedQuery(Guid PostId) : IQuery<bool>;

/// <summary>
/// Public read-side handlers. All queries filter <c>Status == Published &amp;&amp; DeletedAt == null</c>;
/// reaction counts come from a batched group-count over Social.Reactions (domain→domain, direct
/// entity access like the profile lookup — avoids N+1 target-summary calls).
/// </summary>
public static class BlogPublicQueries
{
    internal const int PageSize = 12;
    internal const int CommentPageSize = 20;

    internal static readonly IReadOnlyList<string> NoTags = [];

    /// <summary>True when the candidate sorts strictly before the keyset cursor (newest-first).</summary>
    internal static bool IsBeforeCursor(BlogPost post, DateTime? beforePublishedAt, Guid? beforeId)
        => beforePublishedAt is not { } cursor
            || post.PublishedAt! < cursor
            || (post.PublishedAt == cursor && beforeId is { } id && post.Id.CompareTo(id) < 0);

    internal static bool IsAfterCursor(BlogComment comment, DateTime? afterCreatedAt, Guid? afterId)
        => afterCreatedAt is not { } cursor
            || comment.CreatedAt > cursor
            || (comment.CreatedAt == cursor && afterId is { } id && comment.Id.CompareTo(id) > 0);

    internal static async Task<Dictionary<Guid, (string Handle, string? DisplayName)>> LoadProfilesAsync(
        IApplicationDbContext context, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var profiles = await context.Set<SocialProfile>()
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId) && p.DeletedAt == null)
            .Select(p => new { p.UserId, p.Handle, p.DisplayName })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return profiles.ToDictionary(p => p.UserId, p => (p.Handle, (string?)p.DisplayName));
    }

    internal static async Task<Dictionary<Guid, int>> LoadReactionTotalsAsync(
        IApplicationDbContext context, IReadOnlyCollection<Guid> postIds, CancellationToken ct)
    {
        if (postIds.Count == 0)
        {
            return [];
        }

        var totals = await context.Set<Reaction>()
            .AsNoTracking()
            .Where(r => r.TargetType == ReactionTargetType.BlogPost && postIds.Contains(r.TargetId))
            .GroupBy(r => r.TargetId)
            .Select(g => new { TargetId = g.Key, Total = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return totals.ToDictionary(t => t.TargetId, t => t.Total);
    }

    internal static async Task<Dictionary<Guid, List<string>>> LoadCoAuthorHandlesAsync(
        IApplicationDbContext context, IReadOnlyCollection<Guid> postIds, CancellationToken ct)
    {
        if (postIds.Count == 0)
        {
            return [];
        }

        var rows = await context.Set<BlogPostAuthor>()
            .AsNoTracking()
            .Where(a => postIds.Contains(a.BlogPostId))
            .OrderBy(a => a.AddedAt)
            .Select(a => new { a.BlogPostId, a.UserId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var perPost = rows.GroupBy(r => r.BlogPostId).ToDictionary(g => g.Key, _ => new List<string>());
        var profiles = await LoadProfilesAsync(context, rows.Select(r => r.UserId).Distinct().ToList(), ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            if (profiles.TryGetValue(row.UserId, out var profile))
            {
                perPost[row.BlogPostId].Add(profile.Handle);
            }
        }

        return perPost;
    }

    internal static BlogPostSummaryDto ToSummary(
        BlogPost post,
        (string Handle, string? DisplayName) primary,
        IReadOnlyList<string> coAuthorHandles,
        int reactionCount)
        => new(
            post.Id,
            primary.Handle,
            primary.DisplayName ?? primary.Handle,
            coAuthorHandles,
            post.Title,
            post.Slug,
            post.Excerpt,
            post.ReadTimeMinutes,
            post.PublishedAt,
            post.OgImageUrl,
            post.Tags,
            post.CommentsCount,
            reactionCount);

    /// <summary>Applies the published-only invariant (#10) shared by every public read.</summary>
    internal static IQueryable<BlogPost> PublishedPosts(IApplicationDbContext context)
        => context.Set<BlogPost>()
            .AsNoTracking()
            .Where(p => p.Status == BlogPostStatus.Published && p.DeletedAt == null);
}

/// <summary>Handles <see cref="ListPublicBlogSummariesQuery"/>.</summary>
public sealed class ListPublicBlogSummariesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ListPublicBlogSummariesQuery, BlogPostSummaryPage>
{
    /// <inheritdoc />
    public async Task<BlogPostSummaryPage> Handle(ListPublicBlogSummariesQuery request, CancellationToken cancellationToken)
    {
        var posts = await BlogPublicQueries.PublishedPosts(context)
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var page = posts
            .Where(p => BlogPublicQueries.IsBeforeCursor(p, request.BeforePublishedAt, request.BeforeId))
            .Take(BlogPublicQueries.PageSize + 1)
            .ToList();

        var items = await ProjectSummariesAsync(context, page.Take(BlogPublicQueries.PageSize).ToList(), cancellationToken)
            .ConfigureAwait(false);
        return new BlogPostSummaryPage(items, page.Count > BlogPublicQueries.PageSize);
    }

    internal static async Task<IReadOnlyList<BlogPostSummaryDto>> ProjectSummariesAsync(
        IApplicationDbContext context, IReadOnlyList<BlogPost> page, CancellationToken ct)
    {
        var profiles = await BlogPublicQueries.LoadProfilesAsync(
            context, [.. page.Select(p => p.PrimaryAuthorId)], ct).ConfigureAwait(false);
        var reactions = await BlogPublicQueries.LoadReactionTotalsAsync(
            context, [.. page.Select(p => p.Id)], ct).ConfigureAwait(false);
        var coAuthors = await BlogPublicQueries.LoadCoAuthorHandlesAsync(
            context, [.. page.Select(p => p.Id)], ct).ConfigureAwait(false);

        return [.. page.Select(p => BlogPublicQueries.ToSummary(
            p,
            profiles.GetValueOrDefault(p.PrimaryAuthorId, (string.Empty, null)),
            coAuthors.GetValueOrDefault(p.Id, []),
            reactions.GetValueOrDefault(p.Id)))];
    }
}

/// <summary>Handles <see cref="ListAuthorBlogSummariesQuery"/>.</summary>
public sealed class ListAuthorBlogSummariesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ListAuthorBlogSummariesQuery, BlogPostSummaryPage>
{
    /// <inheritdoc />
    public async Task<BlogPostSummaryPage> Handle(ListAuthorBlogSummariesQuery request, CancellationToken cancellationToken)
    {
        var normalized = SocialProfile.NormalizeHandle(request.Handle);
        var authorUserId = await context.Set<SocialProfile>()
            .AsNoTracking()
            .Where(p => p.Handle == normalized && p.DeletedAt == null)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (authorUserId is null)
        {
            return new BlogPostSummaryPage([], false);
        }

        var posts = await BlogPublicQueries.PublishedPosts(context)
            .Where(p => p.PrimaryAuthorId == authorUserId)
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var page = posts
            .Where(p => BlogPublicQueries.IsBeforeCursor(p, request.BeforePublishedAt, request.BeforeId))
            .Take(BlogPublicQueries.PageSize + 1)
            .ToList();

        var items = await ListPublicBlogSummariesQueryHandler.ProjectSummariesAsync(
            context, page.Take(BlogPublicQueries.PageSize).ToList(), cancellationToken)
            .ConfigureAwait(false);
        return new BlogPostSummaryPage(items, page.Count > BlogPublicQueries.PageSize);
    }
}

/// <summary>Handles <see cref="GetPublicBlogPostDetailQuery"/>.</summary>
public sealed class GetPublicBlogPostDetailQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPublicBlogPostDetailQuery, BlogPostDetailDto?>
{
    /// <inheritdoc />
    public async Task<BlogPostDetailDto?> Handle(GetPublicBlogPostDetailQuery request, CancellationToken cancellationToken)
    {
        var normalized = SocialProfile.NormalizeHandle(request.Handle);
        var post = await BlogPublicQueries.PublishedPosts(context)
            .FirstOrDefaultAsync(p => p.PrimaryAuthorId == context.Set<SocialProfile>()
                .Where(profile => profile.Handle == normalized && profile.DeletedAt == null)
                .Select(profile => profile.UserId)
                .FirstOrDefault()
                && p.Slug == request.Slug, cancellationToken)
            .ConfigureAwait(false);
        if (post is null)
        {
            return null;
        }

        var profile = await context.Set<SocialProfile>()
            .AsNoTracking()
            .Where(p => p.UserId == post.PrimaryAuthorId && p.DeletedAt == null)
            .Select(p => new { p.Handle, p.DisplayName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        var reactions = await BlogPublicQueries.LoadReactionTotalsAsync(context, [post.Id], cancellationToken)
            .ConfigureAwait(false);
        var coAuthorIds = await context.Set<BlogPostAuthor>()
            .AsNoTracking()
            .Where(a => a.BlogPostId == post.Id)
            .OrderBy(a => a.AddedAt)
            .Select(a => a.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var coAuthorProfiles = await BlogPublicQueries.LoadProfilesAsync(context, coAuthorIds, cancellationToken)
            .ConfigureAwait(false);

        return new BlogPostDetailDto(
            post.Id,
            profile.Handle,
            profile.DisplayName ?? profile.Handle,
            [.. coAuthorIds.Where(id => coAuthorProfiles.ContainsKey(id)).Select(id => coAuthorProfiles[id].Handle)],
            post.Title,
            post.Slug,
            post.Excerpt,
            post.Content,
            post.JsonBody,
            post.Format,
            post.ReadTimeMinutes,
            post.PublishedAt,
            post.OgImageUrl,
            post.Tags,
            post.CommentsCount,
            reactions.GetValueOrDefault(post.Id),
            post.AllowComments,
            post.MetaTitle,
            post.MetaDescription,
            post.CanonicalUrlOverride,
            post.TwitterCard,
            post.StructuredDataOverride,
            post.Revision,
            post.UpdatedAt,
            post.ViewsCount);
    }
}

/// <summary>Handles <see cref="ListBlogCommentsQuery"/>.</summary>
public sealed class ListBlogCommentsQueryHandler(IApplicationDbContext context, IUserModerationService moderationService)
    : IQueryHandler<ListBlogCommentsQuery, BlogCommentPage>
{
    /// <inheritdoc />
    public async Task<BlogCommentPage> Handle(ListBlogCommentsQuery request, CancellationToken cancellationToken)
    {
        var postPublished = await BlogPublicQueries.PublishedPosts(context)
            .AnyAsync(p => p.Id == request.PostId, cancellationToken)
            .ConfigureAwait(false);
        if (!postPublished)
        {
            return new BlogCommentPage([], false);
        }

        var comments = await context.Set<BlogComment>()
            .AsNoTracking()
            .Where(c => c.BlogPostId == request.PostId && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (request.ViewerId is { } viewerId)
        {
            var muted = await moderationService.GetMutedUsersAsync(viewerId, ct: cancellationToken).ConfigureAwait(false);
            if (muted.IsSuccess)
            {
                comments = [.. comments.Where(c => !muted.Value.Select(m => m.MutedId).Contains(c.AuthorUserId))];
            }
        }

        // Depth-1 flattening: drop replies whose (muted or soft-deleted) parent is absent,
        // then order roots oldest-first with each root's replies immediately after it.
        var roots = comments.Where(c => c.ParentCommentId is null).ToList();
        var repliesByParent = comments
            .Where(c => c.ParentCommentId is not null)
            .ToLookup(c => c.ParentCommentId!.Value);
        var flattened = roots
            .SelectMany(root => repliesByParent[root.Id].Prepend(root))
            .ToList();

        var page = flattened
            .Where(c => BlogPublicQueries.IsAfterCursor(c, request.AfterCreatedAt, request.AfterId))
            .Take(BlogPublicQueries.CommentPageSize + 1)
            .ToList();

        var profiles = await BlogPublicQueries.LoadProfilesAsync(
            context, [.. page.Select(c => c.AuthorUserId)], cancellationToken).ConfigureAwait(false);

        var items = page
            .Take(BlogPublicQueries.CommentPageSize)
            .Select(c =>
            {
                var profile = profiles.GetValueOrDefault(c.AuthorUserId);
                return new BlogCommentDto(
                    c.Id,
                    c.ParentCommentId,
                    profile.Handle ?? string.Empty,
                    profile.DisplayName,
                    c.Content,
                    c.CreatedAt);
            })
            .ToList();

        return new BlogCommentPage(items, page.Count > BlogPublicQueries.CommentPageSize);
    }
}

/// <summary>Handles <see cref="ResolveBlogRouteForRedirectQuery"/>.</summary>
public sealed class ResolveBlogRouteForRedirectQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ResolveBlogRouteForRedirectQuery, BlogRouteResolutionDto?>
{
    /// <inheritdoc />
    public async Task<BlogRouteResolutionDto?> Handle(ResolveBlogRouteForRedirectQuery request, CancellationToken cancellationToken)
    {
        var normalized = SocialProfile.NormalizeHandle(request.Handle);
        var authorUserId = await context.Set<SocialProfile>()
            .AsNoTracking()
            .Where(p => p.Handle == normalized && p.DeletedAt == null)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (authorUserId is null)
        {
            return null;
        }

        // Current route first; retired route via slug history otherwise.
        var post = await BlogPublicQueries.PublishedPosts(context)
            .Where(p => p.PrimaryAuthorId == authorUserId && p.Slug == request.Slug)
            .Select(p => new { p.PrimaryAuthorId, p.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (post is not null)
        {
            return await ToResolutionAsync(context, post.PrimaryAuthorId, post.Slug, cancellationToken).ConfigureAwait(false);
        }

        var history = await context.Set<BlogSlugHistory>()
            .AsNoTracking()
            .Where(h => h.PreviousPrimaryAuthorId == authorUserId && h.PreviousSlug == request.Slug)
            .Select(h => h.BlogPostId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (history == Guid.Empty)
        {
            return null;
        }

        var target = await BlogPublicQueries.PublishedPosts(context)
            .Where(p => p.Id == history)
            .Select(p => new { p.PrimaryAuthorId, p.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return target is null
            ? null
            : await ToResolutionAsync(context, target.PrimaryAuthorId, target.Slug, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BlogRouteResolutionDto?> ToResolutionAsync(
        IApplicationDbContext context, Guid primaryAuthorId, string slug, CancellationToken ct)
    {
        var handle = await context.Set<SocialProfile>()
            .AsNoTracking()
            .Where(p => p.UserId == primaryAuthorId && p.DeletedAt == null)
            .Select(p => p.Handle)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return handle is null ? null : new BlogRouteResolutionDto(handle, slug);
    }
}

/// <summary>Handles <see cref="IsBlogPostPublishedQuery"/>.</summary>
public sealed class IsBlogPostPublishedQueryHandler(IApplicationDbContext context)
    : IQueryHandler<IsBlogPostPublishedQuery, bool>
{
    /// <inheritdoc />
    public Task<bool> Handle(IsBlogPostPublishedQuery request, CancellationToken cancellationToken)
        => BlogPublicQueries.PublishedPosts(context)
            .AnyAsync(p => p.Id == request.PostId, cancellationToken);
}
