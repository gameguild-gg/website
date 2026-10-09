using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Blog.Services;

namespace GameGuild.Social.Blog.Commands;

/// <summary>Creates a draft blog post; the actor becomes the primary author and the slug is auto-generated.</summary>
/// <param name="Title">Post title.</param>
/// <param name="Format">Authoring format (fixed for the life of the post).</param>
public sealed record CreateBlogPostCommand(string Title, BlogContentFormat Format) : ICommand<BlogPost>;

/// <summary>Revision-guarded draft edit; any author (primary or co-author).</summary>
public sealed record UpdateBlogPostDraftCommand(
    Guid PostId,
    int Revision,
    string? Title = null,
    string? Content = null,
    string? JsonBody = null,
    string? Excerpt = null,
    IReadOnlyList<string>? Tags = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? OgImageUrl = null,
    string? CanonicalUrlOverride = null,
    string? TwitterCard = null,
    string? StructuredDataOverride = null,
    bool? AllowComments = null) : ICommand<BlogPost>;

/// <summary>Changes the post slug (primary only); writes one slug-history row for the old route.</summary>
public sealed record ChangeBlogPostSlugCommand(Guid PostId, string NewSlug) : ICommand<BlogPost>;

/// <summary>Adds a co-author (primary only).</summary>
public sealed record AddBlogPostCoauthorCommand(Guid PostId, Guid UserId) : ICommand<Unit>;

/// <summary>Removes a co-author (primary only).</summary>
public sealed record RemoveBlogPostCoauthorCommand(Guid PostId, Guid UserId) : ICommand<Unit>;

/// <summary>Transfers primary authorship to a current co-author (primary only); writes one history row for the old route.</summary>
public sealed record TransferBlogPrimaryCommand(Guid PostId, Guid NewPrimaryUserId) : ICommand<BlogPost>;

/// <summary>Publishes the post (primary only); dispatches the publication announcement.</summary>
public sealed record PublishBlogPostCommand(Guid PostId) : ICommand<BlogPost>;

/// <summary>Unpublishes the post back to draft (primary only).</summary>
public sealed record UnpublishBlogPostCommand(Guid PostId) : ICommand<BlogPost>;

/// <summary>Soft-deletes the post (primary only).</summary>
public sealed record DeleteBlogPostCommand(Guid PostId) : ICommand<Unit>;

/// <summary>Adds a comment on a published post (any authenticated user; depth ≤ 1; block-enforced).</summary>
public sealed record AddBlogCommentCommand(Guid PostId, string Content, Guid? ParentCommentId = null) : ICommand<BlogComment>;

/// <summary>Soft-deletes a comment (comment author, post primary, or any co-author).</summary>
public sealed record DeleteBlogCommentCommand(Guid CommentId) : ICommand<Unit>;

/// <summary>Command handlers delegating to <see cref="IBlogPostService"/> with actor identity from <see cref="IActorContextAccessor"/>.</summary>
public static class BlogCommandHandlers
{
/// <summary>
/// Extracts the authenticated actor user id and optional tenant from the actor context
/// (tenant from request context, never from the body — invariant #9).
/// </summary>
public static (Guid UserId, Guid? TenantId) RequireActorUser(IActorContextAccessor actorContext)
{
    var actor = actorContext.ActorContext;
    if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("An authenticated user is required.");
        }

        return (userId, actor.TenantId);
}

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

/// <summary>Handles <see cref="CreateBlogPostCommand"/>.</summary>
public sealed class CreateBlogPostCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<CreateBlogPostCommand, BlogPost>
{
    /// <inheritdoc />
    public Task<BlogPost> Handle(CreateBlogPostCommand request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = BlogCommandHandlers.RequireActorUser(actorContext);
        return service.CreateAsync(userId, request.Title, request.Format, tenantId, cancellationToken);
    }
}

/// <summary>Handles <see cref="UpdateBlogPostDraftCommand"/>.</summary>
public sealed class UpdateBlogPostDraftCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<UpdateBlogPostDraftCommand, BlogPost>
{
    /// <inheritdoc />
    public Task<BlogPost> Handle(UpdateBlogPostDraftCommand request, CancellationToken cancellationToken)
        => service.UpdateDraftAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, request, cancellationToken);
}

/// <summary>Handles <see cref="ChangeBlogPostSlugCommand"/>.</summary>
public sealed class ChangeBlogPostSlugCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<ChangeBlogPostSlugCommand, BlogPost>
{
    /// <inheritdoc />
    public Task<BlogPost> Handle(ChangeBlogPostSlugCommand request, CancellationToken cancellationToken)
        => service.ChangeSlugAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, request.NewSlug, cancellationToken);
}

/// <summary>Handles <see cref="AddBlogPostCoauthorCommand"/>.</summary>
public sealed class AddBlogPostCoauthorCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<AddBlogPostCoauthorCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(AddBlogPostCoauthorCommand request, CancellationToken cancellationToken)
    {
        await service.AddCoauthorAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, request.UserId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

/// <summary>Handles <see cref="RemoveBlogPostCoauthorCommand"/>.</summary>
public sealed class RemoveBlogPostCoauthorCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<RemoveBlogPostCoauthorCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(RemoveBlogPostCoauthorCommand request, CancellationToken cancellationToken)
    {
        await service.RemoveCoauthorAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, request.UserId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

/// <summary>Handles <see cref="TransferBlogPrimaryCommand"/>.</summary>
public sealed class TransferBlogPrimaryCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<TransferBlogPrimaryCommand, BlogPost>
{
    /// <inheritdoc />
    public Task<BlogPost> Handle(TransferBlogPrimaryCommand request, CancellationToken cancellationToken)
        => service.TransferPrimaryAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, request.NewPrimaryUserId, cancellationToken);
}

/// <summary>Handles <see cref="PublishBlogPostCommand"/>.</summary>
public sealed class PublishBlogPostCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<PublishBlogPostCommand, BlogPost>
{
    /// <inheritdoc />
    public Task<BlogPost> Handle(PublishBlogPostCommand request, CancellationToken cancellationToken)
        => service.PublishAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, cancellationToken);
}

/// <summary>Handles <see cref="UnpublishBlogPostCommand"/>.</summary>
public sealed class UnpublishBlogPostCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<UnpublishBlogPostCommand, BlogPost>
{
    /// <inheritdoc />
    public Task<BlogPost> Handle(UnpublishBlogPostCommand request, CancellationToken cancellationToken)
        => service.UnpublishAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, cancellationToken);
}

/// <summary>Handles <see cref="DeleteBlogPostCommand"/>.</summary>
public sealed class DeleteBlogPostCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<DeleteBlogPostCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(DeleteBlogPostCommand request, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

/// <summary>Handles <see cref="AddBlogCommentCommand"/>.</summary>
public sealed class AddBlogCommentCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<AddBlogCommentCommand, BlogComment>
{
    /// <inheritdoc />
    public Task<BlogComment> Handle(AddBlogCommentCommand request, CancellationToken cancellationToken)
        => service.AddCommentAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.PostId, request.Content, request.ParentCommentId, cancellationToken);
}

/// <summary>Handles <see cref="DeleteBlogCommentCommand"/>.</summary>
public sealed class DeleteBlogCommentCommandHandler(IBlogPostService service, IActorContextAccessor actorContext)
    : ICommandHandler<DeleteBlogCommentCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(DeleteBlogCommentCommand request, CancellationToken cancellationToken)
    {
        await service.DeleteCommentAsync(BlogCommandHandlers.RequireActorUserId(actorContext), request.CommentId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
