using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Blog.Commands;
using GameGuild.Social.Follows.Services;
using GameGuild.Social.Profiles;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Blog.Services;

/// <summary>
/// Core blog post lifecycle service: permission matrix (primary vs co-author vs stranger),
/// revision-guarded edits, slug engine with history, publication, comments, and moderation.
/// </summary>
public interface IBlogPostService
{
    Task<BlogPost> CreateAsync(Guid actorUserId, string title, BlogContentFormat format, Guid? tenantId, CancellationToken ct = default);
    Task<BlogPost> UpdateDraftAsync(Guid actorUserId, Guid postId, UpdateBlogPostDraftCommand command, CancellationToken ct = default);
    Task<BlogPost> ChangeSlugAsync(Guid actorUserId, Guid postId, string newSlug, CancellationToken ct = default);
    Task AddCoauthorAsync(Guid actorUserId, Guid postId, Guid coauthorUserId, CancellationToken ct = default);
    Task RemoveCoauthorAsync(Guid actorUserId, Guid postId, Guid coauthorUserId, CancellationToken ct = default);
    Task<BlogPost> TransferPrimaryAsync(Guid actorUserId, Guid postId, Guid newPrimaryUserId, CancellationToken ct = default);
    Task<BlogPost> PublishAsync(Guid actorUserId, Guid postId, CancellationToken ct = default);
    Task<BlogPost> UnpublishAsync(Guid actorUserId, Guid postId, CancellationToken ct = default);
    Task DeleteAsync(Guid actorUserId, Guid postId, CancellationToken ct = default);
    Task<BlogComment> AddCommentAsync(Guid actorUserId, Guid postId, string content, Guid? parentCommentId, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid actorUserId, Guid commentId, CancellationToken ct = default);

    Task<BlogPost?> GetForAuthorAsync(Guid actorUserId, Guid postId, CancellationToken ct = default);
    Task<IReadOnlyList<BlogPost>> ListMineAsync(Guid actorUserId, int page, CancellationToken ct = default);
    Task<BlogPost?> ResolveRouteAsync(string handle, string slug, CancellationToken ct = default);
    Task<BlogPost?> GetPublicByHandleAndSlugAsync(string handle, string slug, CancellationToken ct = default);
    Task<IReadOnlyList<BlogPost>> ListAuthorPublicAsync(Guid authorUserId, int page, CancellationToken ct = default);
    Task<IReadOnlyList<BlogPost>> ListIndexAsync(int page, CancellationToken ct = default);
}

/// <summary>Thrown when the acting user lacks the blog permission a command requires.</summary>
public sealed class BlogAccessDeniedException(string message) : UnauthorizedAccessException(message);

/// <summary>Default <see cref="IBlogPostService"/> implementation.</summary>
public sealed class BlogPostService(
    IApplicationDbContext context,
    IBlogSlugService slugService,
    IUserModerationService moderationService,
    IPublicationAnnouncer announcer) : IBlogPostService
{
    private const int PageSize = 12;

    public async Task<BlogPost> CreateAsync(Guid actorUserId, string title, BlogContentFormat format, Guid? tenantId, CancellationToken ct = default)
    {
        var slug = await slugService.GenerateUniqueSlugAsync(actorUserId, title, ct).ConfigureAwait(false);
        var post = BlogPost.Create(actorUserId, title, slug, format, tenantId);
        context.Set<BlogPost>().Add(post);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return post;
    }

    public async Task<BlogPost> UpdateDraftAsync(Guid actorUserId, Guid postId, UpdateBlogPostDraftCommand command, CancellationToken ct = default)
    {
        var post = await RequirePostForAuthorAsync(actorUserId, postId, ct).ConfigureAwait(false);

        var bodyish = command.Content is not null || command.JsonBody is not null;
        var readTime = bodyish
            ? BlogReadTimeEstimator.Estimate(
                post.Format,
                command.Content ?? post.Content,
                command.JsonBody ?? post.JsonBody)
            : (int?)null;

        post.ApplyDraftEdit(
            command.Revision,
            command.Title,
            command.Content,
            command.JsonBody,
            command.Excerpt,
            command.Tags,
            command.MetaTitle,
            command.MetaDescription,
            command.OgImageUrl,
            command.CanonicalUrlOverride,
            command.TwitterCard,
            command.StructuredDataOverride,
            command.AllowComments,
            readTime);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return post;
    }

    public async Task<BlogPost> ChangeSlugAsync(Guid actorUserId, Guid postId, string newSlug, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author can change the slug.");

        var normalized = slugService.Normalize(newSlug);
        if (normalized == post.Slug)
        {
            return post;
        }

        if (!await slugService.IsSlugAvailableAsync(post.PrimaryAuthorId, normalized, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Slug '{normalized}' is already in use by this author.");
        }

        // Exactly one history row per retired route (unique index backs this).
        context.Set<BlogSlugHistory>().Add(BlogSlugHistory.Create(post.Id, post.PrimaryAuthorId, post.Slug));
        post.ChangeSlug(normalized);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return post;
    }

    public async Task AddCoauthorAsync(Guid actorUserId, Guid postId, Guid coauthorUserId, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author manages co-authors.");
        if (coauthorUserId == post.PrimaryAuthorId)
        {
            throw new InvalidOperationException("The primary author cannot be added as a co-author.");
        }

        if (await IsCoauthorAsync(postId, coauthorUserId, ct).ConfigureAwait(false))
        {
            return;
        }

        context.Set<BlogPostAuthor>().Add(BlogPostAuthor.Create(postId, coauthorUserId, actorUserId));
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveCoauthorAsync(Guid actorUserId, Guid postId, Guid coauthorUserId, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author manages co-authors.");

        var row = await context.Set<BlogPostAuthor>()
            .FirstOrDefaultAsync(a => a.BlogPostId == postId && a.UserId == coauthorUserId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        context.Set<BlogPostAuthor>().Remove(row);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<BlogPost> TransferPrimaryAsync(Guid actorUserId, Guid postId, Guid newPrimaryUserId, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author can transfer primary authorship.");
        if (newPrimaryUserId == post.PrimaryAuthorId)
        {
            return post;
        }

        if (!await IsCoauthorAsync(postId, newPrimaryUserId, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The new primary must be a current co-author.");
        }

        // Retire the old route (old primary, current slug) with exactly one history row.
        context.Set<BlogSlugHistory>().Add(BlogSlugHistory.Create(post.Id, post.PrimaryAuthorId, post.Slug));
        var oldPrimary = post.PrimaryAuthorId;
        post.TransferPrimary(newPrimaryUserId);

        // The old primary becomes a co-author; the new primary leaves the co-author table.
        var demotedRow = await context.Set<BlogPostAuthor>()
            .FirstOrDefaultAsync(a => a.BlogPostId == postId && a.UserId == newPrimaryUserId, ct)
            .ConfigureAwait(false);
        if (demotedRow is not null)
        {
            context.Set<BlogPostAuthor>().Remove(demotedRow);
        }

        context.Set<BlogPostAuthor>().Add(BlogPostAuthor.Create(postId, oldPrimary, actorUserId));

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return post;
    }

    public async Task<BlogPost> PublishAsync(Guid actorUserId, Guid postId, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author can publish.");
        if (post.Status == BlogPostStatus.Published)
        {
            return post;
        }

        post.Publish();
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        await announcer.AnnounceBlogPublishedAsync(post, ct).ConfigureAwait(false);
        return post;
    }

    public async Task<BlogPost> UnpublishAsync(Guid actorUserId, Guid postId, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author can unpublish.");
        if (post.Status == BlogPostStatus.Published)
        {
            post.Unpublish();
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return post;
    }

    public async Task DeleteAsync(Guid actorUserId, Guid postId, CancellationToken ct = default)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        RequirePrimary(post, actorUserId, "Only the primary author can delete a post.");
        post.SoftDelete();
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<BlogComment> AddCommentAsync(Guid actorUserId, Guid postId, string content, Guid? parentCommentId, CancellationToken ct = default)
    {
        var post = await context.Set<BlogPost>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Blog post {postId} was not found.");
        if (post.Status != BlogPostStatus.Published)
        {
            throw new InvalidOperationException("Comments are only allowed on published posts.");
        }

        if (!post.AllowComments)
        {
            throw new InvalidOperationException("Comments are disabled on this post.");
        }

        if (parentCommentId is Guid parentId)
        {
            // Depth ≤ 1: parent must be a root comment (and still visible).
            var parent = await context.Set<BlogComment>()
                .FirstOrDefaultAsync(c => c.Id == parentId
                                          && c.BlogPostId == postId
                                          && c.DeletedAt == null, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Parent comment was not found on this post.");
            if (parent.ParentCommentId is not null)
            {
                throw new InvalidOperationException("Comment nesting is limited to one reply level.");
            }
        }

        // Block enforcement: comment-author × primary author, either direction.
        if (actorUserId != post.PrimaryAuthorId)
        {
            var blocked = await moderationService.AreUsersBlockedAsync(actorUserId, post.PrimaryAuthorId, ct).ConfigureAwait(false);
            if (blocked.IsSuccess && blocked.Value)
            {
                throw new BlogAccessDeniedException("Comments are not allowed between blocked users.");
            }
        }

        var comment = BlogComment.Create(postId, actorUserId, content, parentCommentId);
        context.Set<BlogComment>().Add(comment);
        post.IncrementComments(); // comment-count maintenance must not bump UpdatedAt
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return comment;
    }

    public async Task DeleteCommentAsync(Guid actorUserId, Guid commentId, CancellationToken ct = default)
    {
        var comment = await context.Set<BlogComment>()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Blog comment {commentId} was not found.");

        var post = await context.Set<BlogPost>()
            .FirstOrDefaultAsync(p => p.Id == comment.BlogPostId && p.DeletedAt == null, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Blog post {comment.BlogPostId} was not found.");

        var isCommentAuthor = comment.AuthorUserId == actorUserId;
        var isPrimary = post.PrimaryAuthorId == actorUserId;
        var isCoauthor = await IsCoauthorAsync(post.Id, actorUserId, ct).ConfigureAwait(false);
        if (!isCommentAuthor && !isPrimary && !isCoauthor)
        {
            throw new BlogAccessDeniedException("Only the comment author or a post author can delete a comment.");
        }

        comment.Delete(actorUserId);
        post.DecrementComments();
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<BlogPost?> GetForAuthorAsync(Guid actorUserId, Guid postId, CancellationToken ct = default)
        => context.Set<BlogPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null
                && (p.PrimaryAuthorId == actorUserId || context.Set<BlogPostAuthor>().Any(a => a.BlogPostId == p.Id && a.UserId == actorUserId)), ct);

    public async Task<IReadOnlyList<BlogPost>> ListMineAsync(Guid actorUserId, int page, CancellationToken ct = default)
        => await context.Set<BlogPost>()
            .AsNoTracking()
            .Where(p => p.DeletedAt == null
                && (p.PrimaryAuthorId == actorUserId || context.Set<BlogPostAuthor>().Any(a => a.BlogPostId == p.Id && a.UserId == actorUserId)))
            .OrderByDescending(p => p.UpdatedAt)
            .Skip(Math.Max(0, page) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<BlogPost?> ResolveRouteAsync(string handle, string slug, CancellationToken ct = default)
    {
        var userId = await ResolveHandleAsync(handle, ct).ConfigureAwait(false);
        if (userId is null)
        {
            return null;
        }

        // Current route wins first.
        var current = await context.Set<BlogPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PrimaryAuthorId == userId && p.Slug == slug && p.DeletedAt == null, ct)
            .ConfigureAwait(false);
        if (current is not null)
        {
            return current;
        }

        // Retired route: exactly one history row → the post it points to (if still live).
        var history = await context.Set<BlogSlugHistory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.PreviousPrimaryAuthorId == userId && h.PreviousSlug == slug, ct)
            .ConfigureAwait(false);
        if (history is null)
        {
            return null;
        }

        return await context.Set<BlogPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == history.BlogPostId && p.DeletedAt == null, ct)
            .ConfigureAwait(false);
    }

    public async Task<BlogPost?> GetPublicByHandleAndSlugAsync(string handle, string slug, CancellationToken ct = default)
    {
        var userId = await ResolveHandleAsync(handle, ct).ConfigureAwait(false);
        if (userId is null)
        {
            return null;
        }

        return await context.Set<BlogPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PrimaryAuthorId == userId && p.Slug == slug
                && p.DeletedAt == null && p.Status == BlogPostStatus.Published, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BlogPost>> ListAuthorPublicAsync(Guid authorUserId, int page, CancellationToken ct = default)
        => await context.Set<BlogPost>()
            .AsNoTracking()
            .Where(p => p.PrimaryAuthorId == authorUserId && p.DeletedAt == null && p.Status == BlogPostStatus.Published)
            .OrderByDescending(p => p.PublishedAt)
            .Skip(Math.Max(0, page) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<BlogPost>> ListIndexAsync(int page, CancellationToken ct = default)
        => await context.Set<BlogPost>()
            .AsNoTracking()
            .Where(p => p.DeletedAt == null && p.Status == BlogPostStatus.Published)
            .OrderByDescending(p => p.PublishedAt)
            .Skip(Math.Max(0, page) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    private async Task<BlogPost> RequirePostAsync(Guid postId, CancellationToken ct)
        => await context.Set<BlogPost>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Blog post {postId} was not found.");

    /// <summary>Loads a post and asserts the actor is primary or co-author (edit rights).</summary>
    private async Task<BlogPost> RequirePostForAuthorAsync(Guid actorUserId, Guid postId, CancellationToken ct)
    {
        var post = await RequirePostAsync(postId, ct).ConfigureAwait(false);
        if (post.PrimaryAuthorId != actorUserId && !await IsCoauthorAsync(postId, actorUserId, ct).ConfigureAwait(false))
        {
            throw new BlogAccessDeniedException("Only the primary author or a co-author can edit this post.");
        }

        return post;
    }

    private static void RequirePrimary(BlogPost post, Guid actorUserId, string message)
    {
        if (post.PrimaryAuthorId != actorUserId)
        {
            throw new BlogAccessDeniedException(message);
        }
    }

    private Task<bool> IsCoauthorAsync(Guid postId, Guid userId, CancellationToken ct)
        => context.Set<BlogPostAuthor>().AnyAsync(a => a.BlogPostId == postId && a.UserId == userId, ct);

    /// <summary>Resolves a profile handle to a user id via Social.Profiles (domain→domain reference).</summary>
    private async Task<Guid?> ResolveHandleAsync(string handle, CancellationToken ct)
    {
        var normalized = SocialProfile.NormalizeHandle(handle);
        var profile = await context.Set<SocialProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Handle == normalized && p.DeletedAt == null, ct)
            .ConfigureAwait(false);
        return profile?.UserId;
    }
}
