using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Products;

/// <summary>
///     REST API controller for the hierarchical product catalog categories (issue #401).
///     Reads are public storefront data; every mutation requires <c>products:manage</c>,
///     is optimistic-concurrency guarded on updates and audited through the
///     Compliance.Audit pipeline.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/product-categories")]
[Microsoft.AspNetCore.Http.Tags("product-categories")]
[Authorize]
public class ProductCategoriesController(
    IMediator mediator,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    /// List catalog categories (optionally filtered to a subtree or the roots)
    /// </summary>
    /// <param name="parentCategoryId">Only direct children of this category</param>
    /// <param name="rootsOnly">Only root categories</param>
    /// <param name="includeProductCounts">Attach per-category product counts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category list with depth and slug path</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductCategoryDto>>> GetCategories(
        [FromQuery] Guid? parentCategoryId = null,
        [FromQuery] bool rootsOnly = false,
        [FromQuery] bool includeProductCounts = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductCategoriesQuery(
            TenantId: TryTenant(out var tenantId) ? tenantId : null,
            ParentCategoryId: parentCategoryId,
            RootsOnly: rootsOnly,
            IncludeProductCounts: includeProductCounts);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Get a category by ID
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category details with depth and slug path</returns>
    [HttpGet("{categoryId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductCategoryDto>> GetCategory(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await mediator.Send(new GetProductCategoryByIdQuery(categoryId), cancellationToken).ConfigureAwait(false);
        return category is null ? NotFound() : Ok(category);
    }

    /// <summary>
    /// Create a category
    /// </summary>
    /// <param name="request">Creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created category</returns>
    [HttpPost]
    [RequirePermission(ProductsPermission.Keys.Manage)]
    public async Task<ActionResult<ProductCategoryDto>> CreateCategory(
        [FromBody] CreateProductCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();

        var command = new CreateProductCategoryCommand(
            request.Name,
            request.Slug,
            request.ParentCategoryId,
            request.Description,
            request.AttributesJson,
            request.DisplayOrder,
            tenantId,
            actorContextAccessor.ActorContext.SubjectIdAsGuid);
        var category = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetCategory), new { categoryId = category.Id }, category);
    }

    /// <summary>
    /// Update a category (rename, re-slug, reparent, attributes)
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated category</returns>
    [HttpPut("{categoryId:guid}")]
    [RequirePermission(ProductsPermission.Keys.Manage)]
    public async Task<ActionResult<ProductCategoryDto>> UpdateCategory(
        Guid categoryId,
        [FromBody] UpdateProductCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateProductCategoryCommand(
            categoryId,
            request.Name,
            request.Slug,
            request.ParentCategoryId,
            request.Reparent,
            request.Description,
            request.AttributesJson,
            request.DisplayOrder,
            request.ExpectedVersion,
            actorContextAccessor.ActorContext.SubjectIdAsGuid,
            actorContextAccessor.ActorContext.TenantId);
        var category = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(category);
    }

    /// <summary>
    /// Delete a category (soft). Rejected while children or assigned products exist.
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{categoryId:guid}")]
    [RequirePermission(ProductsPermission.Keys.Manage)]
    public async Task<IActionResult> DeleteCategory(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteProductCategoryCommand(
            categoryId,
            actorContextAccessor.ActorContext.SubjectIdAsGuid,
            actorContextAccessor.ActorContext.TenantId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue)
            return false;

        tenantId = actor.TenantId.Value;
        return true;
    }
}

/// <summary>Request model for creating a product category.</summary>
public sealed record CreateProductCategoryRequest
{
    /// <summary>Display name (tenant-unique).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>URL-safe identifier (tenant-unique; letters, digits, hyphens, spaces).</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Parent category ID (null = root).</summary>
    public Guid? ParentCategoryId { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional metadata as a JSON object of string values (≤ 20 keys).</summary>
    public string? AttributesJson { get; init; }

    /// <summary>Sort order.</summary>
    public int DisplayOrder { get; init; }
}

/// <summary>Request model for updating a product category.</summary>
public sealed record UpdateProductCategoryRequest
{
    /// <summary>New display name.</summary>
    public string? Name { get; init; }

    /// <summary>New slug (tenant-unique).</summary>
    public string? Slug { get; init; }

    /// <summary>New parent category ID (applied only when <see cref="Reparent"/> is true).</summary>
    public Guid? ParentCategoryId { get; init; }

    /// <summary>Whether to apply <see cref="ParentCategoryId"/> as a reparenting.</summary>
    public bool Reparent { get; init; }

    /// <summary>New description.</summary>
    public string? Description { get; init; }

    /// <summary>Replacement metadata JSON object (null = leave unchanged, "{}" clears).</summary>
    public string? AttributesJson { get; init; }

    /// <summary>New sort order.</summary>
    public int? DisplayOrder { get; init; }

    /// <summary>Expected version for optimistic concurrency.</summary>
    public long? ExpectedVersion { get; init; }
}
