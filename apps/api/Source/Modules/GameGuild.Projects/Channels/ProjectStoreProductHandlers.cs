using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Projects;

public sealed class ProjectStoreProductHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IProjectChannelAvailabilityService availabilityService,
    IProjectAuthorizationService authorizationService,
    ILogger<ProjectStoreProductHandlers> logger,
    IProjectLifecycleLock? lifecycleLock = null)
    : ICommandHandler<LinkProjectStoreProductCommand, Result<ProjectStoreProductProjection>>,
      ICommandHandler<UnlinkProjectStoreProductCommand, Result<bool>>,
      IQueryHandler<GetProjectStoreProductsQuery, Result<IReadOnlyList<ProjectStoreProductProjection>>>,
      IQueryHandler<GetPublicStoreProductProjectsQuery, Result<IReadOnlyList<ProjectStoreProductProjection>>>
{
    private readonly IProjectLifecycleLock _lifecycleLock = lifecycleLock ?? new ProjectLifecycleLock(context);

    public async Task<Result<ProjectStoreProductProjection>> Handle(LinkProjectStoreProductCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || actorId == null || actor.TenantId == null)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Unauthorized("ProjectStoreProduct.Unauthenticated", "An authenticated tenant actor is required."));
        }

        await using var lockHandle = await _lifecycleLock.AcquireAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        if (!await authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.NotFound("ProjectStoreProduct.ProjectNotFound", "Project not found."));
        }

        var availability = await availabilityService
            .GetAsync(request.ProjectId, ProjectChannel.Store, actor.TenantId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Validation("ProjectStoreProduct.ProjectUnavailable", availability.Reason));
        }

        var product = await context.Set<Product>()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.ProductId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (product == null)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.NotFound("ProjectStoreProduct.ProductNotFound", "Product not found."));
        }

        if (product.TenantId != actor.TenantId)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Forbidden("ProjectStoreProduct.ProductTenantMismatch", "Product is outside the current tenant."));
        }

        if (product.CreatorId != actorId && !actor.HasAnyPermission(ProductsPermission.Keys.Update, ProductsPermission.Keys.Manage))
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Forbidden("ProjectStoreProduct.ProductForbidden", "Product ownership or update permission is required."));
        }

        if (!product.IsPublished)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Validation("ProjectStoreProduct.ProductUnpublished", "Only published products can be linked."));
        }

        if (product.IsBundle)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Validation("ProjectStoreProduct.BundleUnsupported", "Bundle products cannot be linked directly to projects."));
        }

        var duplicate = await context.Set<ProjectStoreProduct>()
            .AnyAsync(link =>
                link.ProjectId == request.ProjectId &&
                link.ProductId == request.ProductId &&
                link.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return Result.Failure<ProjectStoreProductProjection>(Error.Conflict("ProjectStoreProduct.LinkExists", "An active project-product link already exists."));
        }

        var link = new ProjectStoreProduct
        {
            ProjectId = request.ProjectId,
            ProductId = request.ProductId,
            TenantId = actor.TenantId
        };
        context.Set<ProjectStoreProduct>().Add(link);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Actor {ActorId} linked project {ProjectId} to product {ProductId}", actorId, request.ProjectId, request.ProductId);
        return Result.Success(ToProjection(link));
    }

    public async Task<Result<bool>> Handle(UnlinkProjectStoreProductCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || actorId == null || actor.TenantId == null)
        {
            return Result.Failure<bool>(Error.Unauthorized("ProjectStoreProduct.Unauthenticated", "An authenticated tenant actor is required."));
        }

        if (!await authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(Error.NotFound("ProjectStoreProduct.ProjectNotFound", "Project not found."));
        }

        var product = await context.Set<Product>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.ProductId, cancellationToken)
            .ConfigureAwait(false);
        if (product == null || product.TenantId != actor.TenantId)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectStoreProduct.ProductNotFound", "Product not found in the current tenant."));
        }

        if (product.CreatorId != actorId && !actor.HasAnyPermission(ProductsPermission.Keys.Update, ProductsPermission.Keys.Manage))
        {
            return Result.Failure<bool>(Error.Forbidden("ProjectStoreProduct.ProductForbidden", "Product ownership or update permission is required."));
        }

        if (product.IsBundle)
        {
            return Result.Failure<bool>(Error.Validation("ProjectStoreProduct.BundleUnsupported", "Bundle products cannot be linked directly to projects."));
        }

        var link = await context.Set<ProjectStoreProduct>()
            .FirstOrDefaultAsync(candidate =>
                candidate.ProjectId == request.ProjectId &&
                candidate.ProductId == request.ProductId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (link == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectStoreProduct.LinkNotFound", "Active project-product link not found."));
        }

        link.DeletedAt = SystemClock.UtcNow;
        link.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<IReadOnlyList<ProjectStoreProductProjection>>> Handle(GetProjectStoreProductsQuery request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || actorId == null || actor.TenantId == null ||
            !await authorizationService.IsActorActiveTenantMemberAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<ProjectStoreProductProjection>>(
                Error.Unauthorized("ProjectStoreProduct.Unauthenticated", "An active authenticated tenant actor is required."));
        }
        if (!await authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<ProjectStoreProductProjection>>(
            Error.NotFound("ProjectStoreProduct.ProjectNotFound", "Project not found."));
        }

        var availability = await availabilityService
            .GetAsync(request.ProjectId, ProjectChannel.Store, actor.TenantId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return Result.Failure<IReadOnlyList<ProjectStoreProductProjection>>(
            Error.Validation("ProjectStoreProduct.ProjectUnavailable", availability.Reason));
        }

        var tenantId = actor.TenantId.Value;
        var validLinks = context.Set<ProjectStoreProduct>()
            .AsNoTracking()
            .Where(link =>
                link.ProjectId == request.ProjectId &&
                link.TenantId == tenantId &&
                link.DeletedAt == null &&
                link.Project.DeletedAt == null &&
                link.Project.TenantId == tenantId &&
                link.Project.Status == ContentStatus.Published &&
                link.Project.Visibility == ContentVisibility.Public &&
                link.Product.DeletedAt == null &&
                link.Product.TenantId == tenantId &&
                link.Product.IsPublished &&
                !link.Product.IsBundle);

        if (!actor.HasAnyPermission(ProductsPermission.Keys.Update, ProductsPermission.Keys.Manage) &&
            await validLinks.AnyAsync(link => link.Product.CreatorId != actorId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<ProjectStoreProductProjection>>(
                Error.Forbidden("ProjectStoreProduct.ProductForbidden", "Product ownership or update permission is required."));
        }

        var links = await validLinks
            .OrderBy(link => link.CreatedAt)
            .Select(link => new ProjectStoreProductProjection(link.Id, link.ProjectId, link.ProductId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProjectStoreProductProjection>>(links);
    }

    public async Task<Result<IReadOnlyList<ProjectStoreProductProjection>>> Handle(GetPublicStoreProductProjectsQuery request, CancellationToken cancellationToken)
    {
        var product = await context.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.ProductId &&
                candidate.DeletedAt == null &&
                candidate.IsPublished &&
                !candidate.IsBundle,
                cancellationToken)
            .ConfigureAwait(false);
        if (product == null)
        {
            return Result.Failure<IReadOnlyList<ProjectStoreProductProjection>>(Error.NotFound("ProjectStoreProduct.ProductNotFound", "Published product not found."));
        }

        var links = await context.Set<ProjectStoreProduct>()
            .AsNoTracking()
            .Where(link =>
                link.ProductId == request.ProductId &&
                link.DeletedAt == null &&
                link.TenantId == product.TenantId &&
                link.Project.DeletedAt == null &&
                link.Project.TenantId == product.TenantId &&
                link.Project.Status == ContentStatus.Published &&
                link.Project.Visibility == ContentVisibility.Public)
            .OrderBy(link => link.CreatedAt)
            .Select(link => new ProjectStoreProductProjection(link.Id, link.ProjectId, link.ProductId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProjectStoreProductProjection>>(links);
    }

    private static ProjectStoreProductProjection ToProjection(ProjectStoreProduct link)
        => new(link.Id, link.ProjectId, link.ProductId);
}
