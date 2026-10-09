using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Blog.Services;

namespace GameGuild.Social.Blog.Queries;

/// <summary>Fetches a post by id for an author (primary or co-author) — full draft access.</summary>
public sealed record GetBlogPostForAuthorQuery(Guid PostId) : IQuery<BlogPost?>;

/// <summary>Lists the acting user's posts (authored + co-authored), newest edit first.</summary>
public sealed record ListMyBlogPostsQuery(int Page = 0) : IQuery<IReadOnlyList<BlogPost>>;

/// <summary>Resolves a (handle, slug) route to the current canonical post (history-aware) or null.</summary>
public sealed record ResolveBlogRouteQuery(string Handle, string Slug) : IQuery<BlogPost?>;

/// <summary>Public author listing: published posts of one author, newest first.</summary>
public sealed record GetBlogAuthorPublicQuery(Guid AuthorUserId, int Page = 0) : IQuery<IReadOnlyList<BlogPost>>;

/// <summary>Public post detail by (handle, slug); drafts and missing posts are indistinguishable (null).</summary>
public sealed record GetBlogPostPublicQuery(string Handle, string Slug) : IQuery<BlogPost?>;

/// <summary>Global public blog index, newest published first.</summary>
public sealed record ListBlogIndexQuery(int Page = 0) : IQuery<IReadOnlyList<BlogPost>>;

/// <summary>Query handlers delegating to <see cref="IBlogPostService"/>; acting user from <see cref="IActorContextAccessor"/>.</summary>
public static class BlogCommandHandlers
{
    /// <summary>Extracts the authenticated actor user id or throws.</summary>
    public static Guid RequireActorUserId(IActorContextAccessor actorContext)
    {
        var actor = actorContext.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("An authenticated user is required.");
        }

        return userId;
    }
}

/// <summary>Handles <see cref="GetBlogPostForAuthorQuery"/>.</summary>
public sealed class GetBlogPostForAuthorQueryHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : IQueryHandler<GetBlogPostForAuthorQuery, BlogPost?>
{
    /// <inheritdoc />
    public Task<BlogPost?> Handle(GetBlogPostForAuthorQuery request, CancellationToken cancellationToken)
        => service.GetForAuthorAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, cancellationToken);
}

/// <summary>Handles <see cref="ListMyBlogPostsQuery"/>.</summary>
public sealed class ListMyBlogPostsQueryHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : IQueryHandler<ListMyBlogPostsQuery, IReadOnlyList<BlogPost>>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<BlogPost>> Handle(ListMyBlogPostsQuery request, CancellationToken cancellationToken)
        => service.ListMineAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.Page, cancellationToken);
}

/// <summary>Handles <see cref="ResolveBlogRouteQuery"/>.</summary>
public sealed class ResolveBlogRouteQueryHandler(IBlogPostService service)
    : IQueryHandler<ResolveBlogRouteQuery, BlogPost?>
{
    /// <inheritdoc />
    public Task<BlogPost?> Handle(ResolveBlogRouteQuery request, CancellationToken cancellationToken)
        => service.ResolveRouteAsync(request.Handle, request.Slug, cancellationToken);
}

/// <summary>Handles <see cref="GetBlogAuthorPublicQuery"/>.</summary>
public sealed class GetBlogAuthorPublicQueryHandler(IBlogPostService service)
    : IQueryHandler<GetBlogAuthorPublicQuery, IReadOnlyList<BlogPost>>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<BlogPost>> Handle(GetBlogAuthorPublicQuery request, CancellationToken cancellationToken)
        => service.ListAuthorPublicAsync(request.AuthorUserId, request.Page, cancellationToken);
}

/// <summary>Handles <see cref="GetBlogPostPublicQuery"/>.</summary>
public sealed class GetBlogPostPublicQueryHandler(IBlogPostService service)
    : IQueryHandler<GetBlogPostPublicQuery, BlogPost?>
{
    /// <inheritdoc />
    public Task<BlogPost?> Handle(GetBlogPostPublicQuery request, CancellationToken cancellationToken)
        => service.GetPublicByHandleAndSlugAsync(request.Handle, request.Slug, cancellationToken);
}

/// <summary>Handles <see cref="ListBlogIndexQuery"/>.</summary>
public sealed class ListBlogIndexQueryHandler(IBlogPostService service)
    : IQueryHandler<ListBlogIndexQuery, IReadOnlyList<BlogPost>>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<BlogPost>> Handle(ListBlogIndexQuery request, CancellationToken cancellationToken)
        => service.ListIndexAsync(request.Page, cancellationToken);
}
