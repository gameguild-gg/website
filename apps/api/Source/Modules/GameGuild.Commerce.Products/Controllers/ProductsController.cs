using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Products;

/// <summary>
/// REST API controller for managing products
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/products")]
[Microsoft.AspNetCore.Http.Tags("products")]
[Authorize]
public class ProductsController(IMediator mediator, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    /// Get product by ID
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="includePricing">Include pricing information</param>
    /// <param name="includeUnpublished">Include drafts when authenticated</param>
    /// <returns>Product details</returns>
    [HttpGet("{productId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> GetProduct(
        Guid productId,
        [FromQuery] bool includePricing = true,
        [FromQuery] bool includeUnpublished = false)
    {
        var query = new GetProductByIdQuery(productId, includePricing, IncludeUnpublished: includeUnpublished && HasActor());
        var product = await mediator.Send(query).ConfigureAwait(false);

        if (product == null || (!product.IsPublished && !CanAccessUnpublished(product)))
            return NotFound();

        return Ok(product);
    }

    /// <summary>
    /// Check if a product exists
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="includeUnpublished">Include drafts when authenticated</param>
    /// <returns>200 if exists, 404 if not</returns>
    [HttpHead("{productId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> ProductExists(Guid productId, [FromQuery] bool includeUnpublished = false)
    {
        if (includeUnpublished && HasActor())
        {
            var product = await mediator.Send(new GetProductByIdQuery(productId, IncludePricing: false, IncludeUnpublished: true)).ConfigureAwait(false);
            return product is not null && (product.IsPublished || CanAccessUnpublished(product)) ? Ok() : NotFound();
        }

        var exists = await mediator.Send(new ProductExistsQuery(productId)).ConfigureAwait(false);
        return exists ? Ok() : NotFound();
    }

    /// <summary>
    /// Get pricing options for a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="includeUnpublished">Include drafts when authenticated</param>
    /// <returns>List of pricing options</returns>
    [HttpGet("{productId:guid}/pricing")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductPricingDto>>> GetProductPricing(
        Guid productId,
        [FromQuery] bool includeUnpublished = false)
    {
        var canInclude = false;
        if (includeUnpublished && HasActor())
        {
            var product = await mediator.Send(new GetProductByIdQuery(productId, IncludePricing: false, IncludeUnpublished: true)).ConfigureAwait(false);
            if (product is null || (!product.IsPublished && !CanAccessUnpublished(product))) return NotFound();
            canInclude = true;
        }

        var query = new GetProductPricingQuery(productId, canInclude);
        var pricing = await mediator.Send(query).ConfigureAwait(false);
        return Ok(pricing);
    }

    [HttpPut("{productId:guid}/pricing")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<ProductPricingDto>> SetProductPricing(
        Guid productId,
        [FromBody] SetProductPricingRequest request)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false) || !TryActor(out _, out var actorId)) return Forbid();

        var pricing = await mediator.Send(new SetProductPricingCommand(
            productId,
            request.Name,
            request.BasePrice,
            request.Currency,
            request.SalePrice,
            request.SaleStartDate,
            request.SaleEndDate,
            request.IsDefault,
            request.PricingId,
            actorId)).ConfigureAwait(false);
        return Ok(pricing);
    }

    /// <summary>
    /// Get a product's real-time availability (issue #401)
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <returns>Stock state; untracked products report always-in-stock</returns>
    [HttpGet("{productId:guid}/availability")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductAvailabilityDto>> GetProductAvailability(Guid productId)
    {
        var availability = await mediator.Send(new GetProductAvailabilityQuery(productId)).ConfigureAwait(false);
        return availability is null ? NotFound() : Ok(availability);
    }

    /// <summary>
    /// Create or replace a product's inventory configuration (issue #401)
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="request">Inventory configuration request</param>
    /// <returns>Inventory state after the change</returns>
    [HttpPut("{productId:guid}/inventory")]
    [RequirePermission(ProductsPermission.Keys.Manage)]
    public async Task<ActionResult<ProductInventoryDto>> SetProductInventory(
        Guid productId,
        [FromBody] SetProductInventoryRequest request)
    {
        var command = new SetProductInventoryCommand(
            productId,
            request.StockQuantity,
            request.TrackInventory,
            request.LowStockThreshold,
            actorContextAccessor.ActorContext.SubjectIdAsGuid);
        var inventory = await mediator.Send(command).ConfigureAwait(false);
        return Ok(inventory);
    }

    /// <summary>
    /// Get paginated list of products
    /// </summary>
    /// <param name="type">Filter by product type</param>
    /// <param name="creatorId">Filter by creator ID</param>
    /// <param name="searchTerm">Search term</param>
    /// <param name="isBundle">Filter by bundle status</param>
    /// <param name="categoryId">Filter by category, including all descendant categories</param>
    /// <param name="inStock">Filter by real-time availability (true = in stock only)</param>
    /// <param name="includeUnpublished">Include drafts when authenticated</param>
    /// <param name="skip">Items to skip</param>
    /// <param name="take">Items to take</param>
    /// <param name="sortBy">Sort field</param>
    /// <param name="sortDirection">Sort direction</param>
    /// <returns>Paginated products</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
        [FromQuery] ProductType? type = null,
        [FromQuery] Guid? creatorId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isBundle = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool? inStock = null,
        [FromQuery] bool includeUnpublished = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDirection = "DESC")
    {
        var tenantId = Guid.Empty;
        var actorId = Guid.Empty;
        var includeDrafts = includeUnpublished && TryActor(out tenantId, out actorId);
        if (includeDrafts && !actorContextAccessor.ActorContext.HasPermission(ProductsPermission.Keys.Manage))
            creatorId = actorId;

        var query = new GetProductsPagedQuery(
            type,
            creatorId,
            searchTerm,
            isBundle,
            includeDrafts,
            skip,
            take,
            sortBy,
            sortDirection,
            includeDrafts ? tenantId : null,
            CategoryId: categoryId,
            InStockOnly: inStock);
        var result = await mediator.Send(query).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Create a new product
    /// </summary>
    /// <param name="request">Product creation request</param>
    /// <returns>Created product</returns>
    [HttpPost]
    [RequirePermission(ProductsPermission.Keys.Create)]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        var command = new CreateProductCommand(
            request.Name,
            request.Description,
            request.ShortDescription,
            request.ImageUrl,
            request.Type,
            request.IsBundle,
            actorId,
            request.BundleItems,
            request.ReferralCommissionPercentage,
            request.MaxAffiliateDiscount,
            request.AffiliateCommissionPercentage,
            tenantId,
            request.CategoryId
        );

        var product = await mediator.Send(command).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetProduct), new { productId = product.Id }, product);
    }

    /// <summary>
    /// Batch create multiple products
    /// </summary>
    /// <param name="request">Batch create request</param>
    /// <returns>Created products</returns>
    [HttpPost(":batch-create")]
    [RequirePermission(ProductsPermission.Keys.Create)]
    public async Task<ActionResult<List<ProductDto>>> BatchCreateProducts([FromBody] BatchCreateProductsRequest request)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        var productsToCreate = request.Products.Select(item => item with { CreatorId = actorId }).ToList();
        var command = new BatchCreateProductsCommand(productsToCreate, tenantId);
        var products = await mediator.Send(command).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, products);
    }

    /// <summary>
    /// Update an existing product (full update)
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="request">Product update request</param>
    /// <returns>Updated product</returns>
    [HttpPut("{productId:guid}")]
    [RequirePermission(ProductsPermission.Keys.Update)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(Guid productId, [FromBody] UpdateProductRequest request)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false)) return Forbid();
        var command = new UpdateProductCommand(
            productId,
            request.Name,
            request.Description,
            request.ShortDescription,
            request.ImageUrl,
            request.Type,
            request.IsBundle,
            request.BundleItems,
            request.ReferralCommissionPercentage,
            request.MaxAffiliateDiscount,
            request.AffiliateCommissionPercentage,
            request.ExpectedVersion,
            request.CategoryId
        );

        var product = await mediator.Send(command).ConfigureAwait(false);
        return Ok(product);
    }

    /// <summary>
    /// Partially update a product (PATCH)
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="request">Partial update request</param>
    /// <returns>Updated product</returns>
    [HttpPatch("{productId:guid}")]
    [RequirePermission(ProductsPermission.Keys.Update)]
    public async Task<ActionResult<ProductDto>> PatchProduct(Guid productId, [FromBody] PatchProductRequest request)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false)) return Forbid();
        var command = new PatchProductCommand(
            productId,
            request.Name,
            request.Description,
            request.ShortDescription,
            request.ImageUrl,
            request.Type,
            request.IsBundle,
            request.BundleItems,
            request.ReferralCommissionPercentage,
            request.MaxAffiliateDiscount,
            request.AffiliateCommissionPercentage,
            request.ExpectedVersion,
            request.CategoryId
        );

        var product = await mediator.Send(command).ConfigureAwait(false);
        return Ok(product);
    }

    /// <summary>
    /// Activate a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <returns>Activated product</returns>
    [HttpPost("{productId:guid}:activate")]
    [RequirePermission(ProductsPermission.Keys.Update)]
    public async Task<ActionResult<ProductDto>> ActivateProduct(Guid productId)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false)) return Forbid();
        var command = new ActivateProductCommand(productId);
        var product = await mediator.Send(command).ConfigureAwait(false);
        return Ok(product);
    }

    /// <summary>
    /// Deactivate a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <returns>Deactivated product</returns>
    [HttpPost("{productId:guid}:deactivate")]
    [RequirePermission(ProductsPermission.Keys.Update)]
    public async Task<ActionResult<ProductDto>> DeactivateProduct(Guid productId)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false)) return Forbid();
        var command = new DeactivateProductCommand(productId);
        var product = await mediator.Send(command).ConfigureAwait(false);
        return Ok(product);
    }

    /// <summary>
    /// Archive a product (soft delete)
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <returns>Archived product</returns>
    [HttpPost("{productId:guid}:archive")]
    [RequirePermission(ProductsPermission.Keys.Delete)]
    public async Task<ActionResult<ProductDto>> ArchiveProduct(Guid productId)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false)) return Forbid();
        var command = new ArchiveProductCommand(productId);
        var product = await mediator.Send(command).ConfigureAwait(false);
        return Ok(product);
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="softDelete">Soft delete (default) or hard delete</param>
    /// <param name="reason">Deletion reason</param>
    /// <returns>No content</returns>
    [HttpDelete("{productId:guid}")]
    [RequirePermission(ProductsPermission.Keys.Delete)]
    public async Task<ActionResult> DeleteProduct(
        Guid productId,
        [FromQuery] bool softDelete = true,
        [FromQuery] string? reason = null)
    {
        if (!await CanMutateAsync(productId).ConfigureAwait(false)) return Forbid();
        var command = new DeleteProductCommand(productId, softDelete, reason);
        await mediator.Send(command).ConfigureAwait(false);
        return NoContent();
    }

    private bool HasActor()
        => TryActor(out _, out _);

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue)
            return false;

        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }

    private bool CanAccessUnpublished(ProductDto product)
    {
        if (!TryActor(out var tenantId, out var actorId) || product.TenantId != tenantId)
            return false;

        return product.CreatorId == actorId || actorContextAccessor.ActorContext.HasPermission(ProductsPermission.Keys.Manage);
    }

    private async Task<bool> CanMutateAsync(Guid productId)
    {
        var product = await mediator.Send(new GetProductByIdQuery(productId, IncludePricing: false, IncludeUnpublished: true)).ConfigureAwait(false);
        return product is not null && CanAccessUnpublished(product);
    }
}

/// <summary>
/// Request model for creating a product
/// </summary>
public sealed record CreateProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public string? ImageUrl { get; init; }
    public ProductType Type { get; init; } = ProductType.Program;
    public bool IsBundle { get; init; }
    public Guid? CategoryId { get; init; }
    public List<Guid>? BundleItems { get; init; }
    public decimal ReferralCommissionPercentage { get; init; } = 30m;
    public decimal MaxAffiliateDiscount { get; init; }
    public decimal AffiliateCommissionPercentage { get; init; } = 30m;
}

/// <summary>
/// Request model for updating a product
/// </summary>
public sealed record UpdateProductRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public string? ImageUrl { get; init; }
    public ProductType? Type { get; init; }
    public bool? IsBundle { get; init; }
    public Guid? CategoryId { get; init; }
    public List<Guid>? BundleItems { get; init; }
    public decimal? ReferralCommissionPercentage { get; init; }
    public decimal? MaxAffiliateDiscount { get; init; }
    public decimal? AffiliateCommissionPercentage { get; init; }
    public long? ExpectedVersion { get; init; }
}

/// <summary>
/// Request model for partial product update (PATCH)
/// </summary>
public sealed record PatchProductRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public string? ImageUrl { get; init; }
    public ProductType? Type { get; init; }
    public bool? IsBundle { get; init; }
    public Guid? CategoryId { get; init; }
    public List<Guid>? BundleItems { get; init; }
    public decimal? ReferralCommissionPercentage { get; init; }
    public decimal? MaxAffiliateDiscount { get; init; }
    public decimal? AffiliateCommissionPercentage { get; init; }
    public long? ExpectedVersion { get; init; }
}

/// <summary>
/// Request model for batch creating products
/// </summary>
public sealed record BatchCreateProductsRequest
{
    public List<BatchProductCreateItem> Products { get; init; } = new();
}

public sealed record SetProductPricingRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    decimal? SalePrice,
    DateTime? SaleStartDate,
    DateTime? SaleEndDate,
    bool IsDefault,
    Guid? PricingId);

/// <summary>
/// Request model for configuring a product's inventory (issue #401)
/// </summary>
public sealed record SetProductInventoryRequest
{
    /// <summary>Units on hand.</summary>
    public int StockQuantity { get; init; }

    /// <summary>Whether stock should be tracked for this product.</summary>
    public bool TrackInventory { get; init; }

    /// <summary>Low-stock threshold.</summary>
    public int LowStockThreshold { get; init; } = 5;
}

