using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to activate a product
/// </summary>
/// <param name="ProductId">Product ID</param>
public sealed record ActivateProductCommand(Guid ProductId) : ICommand<ProductDto>;

/// <summary>
/// Handler for ActivateProductCommand
/// </summary>
public sealed class ActivateProductHandler(IProductRepository repository) : ICommandHandler<ActivateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(ActivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        product.IsPublished = true;
        product.Touch();

        await repository.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return product.ToDto();
    }
}

/// <summary>
/// Command to deactivate a product
/// </summary>
/// <param name="ProductId">Product ID</param>
public sealed record DeactivateProductCommand(Guid ProductId) : ICommand<ProductDto>;

/// <summary>
/// Handler for DeactivateProductCommand
/// </summary>
public sealed class DeactivateProductHandler(IProductRepository repository) : ICommandHandler<DeactivateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        product.IsPublished = false;
        product.Touch();

        await repository.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return product.ToDto();
    }
}

/// <summary>
/// Command to archive a product
/// </summary>
/// <param name="ProductId">Product ID</param>
public sealed record ArchiveProductCommand(Guid ProductId) : ICommand<ProductDto>;

/// <summary>
/// Handler for ArchiveProductCommand
/// </summary>
public sealed class ArchiveProductHandler(IProductRepository repository) : ICommandHandler<ArchiveProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        // Archive the product using soft delete
        product.SoftDelete();

        await repository.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return product.ToDto();
    }
}

/// <summary>
/// Command to patch (partial update) a product
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="Name">Optional new name</param>
/// <param name="Description">Optional new description</param>
/// <param name="ShortDescription">Optional new short description</param>
/// <param name="ImageUrl">Optional new image URL</param>
/// <param name="Type">Optional new type</param>
/// <param name="IsBundle">Optional bundle flag</param>
/// <param name="BundleItems">Optional bundle items</param>
/// <param name="ReferralCommissionPercentage">Optional referral commission</param>
/// <param name="MaxAffiliateDiscount">Optional max affiliate discount</param>
/// <param name="AffiliateCommissionPercentage">Optional affiliate commission</param>
/// <param name="ExpectedVersion">Expected version for optimistic concurrency</param>
/// <param name="CategoryId">Catalog category assignment (single category, v1; null = leave unchanged)</param>
public sealed record PatchProductCommand(
    Guid ProductId,
    string? Name = null,
    string? Description = null,
    string? ShortDescription = null,
    string? ImageUrl = null,
    ProductType? Type = null,
    bool? IsBundle = null,
    List<Guid>? BundleItems = null,
    decimal? ReferralCommissionPercentage = null,
    decimal? MaxAffiliateDiscount = null,
    decimal? AffiliateCommissionPercentage = null,
    long? ExpectedVersion = null,
    Guid? CategoryId = null
) : ICommand<ProductDto>;

/// <summary>
/// Handler for PatchProductCommand
/// </summary>
public sealed class PatchProductHandler(IProductRepository repository, IProductCategoryRepository categoryRepository) : ICommandHandler<PatchProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(PatchProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        // Check optimistic concurrency
        if (request.ExpectedVersion.HasValue && product.Version != request.ExpectedVersion.Value)
        {
            throw new InvalidOperationException($"Product version mismatch. Expected {request.ExpectedVersion}, actual {product.Version}");
        }

        // Category assignment (issue #401): validated against the tenant's catalog first.
        if (request.CategoryId.HasValue)
            await ProductCategoryAssignment.ValidateAsync(categoryRepository, product, request.CategoryId.Value, cancellationToken).ConfigureAwait(false);

        // Apply only non-null values (partial update)
        if (request.Name != null) product.Name = request.Name;
        if (request.Description != null) product.Description = request.Description;
        if (request.ShortDescription != null) product.ShortDescription = request.ShortDescription;
        if (request.ImageUrl != null) product.ImageUrl = request.ImageUrl;
        if (request.Type.HasValue) product.Type = request.Type.Value;
        if (request.IsBundle.HasValue) product.IsBundle = request.IsBundle.Value;
        if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;

        product.Touch();

        await repository.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return product.ToDto();
    }
}

/// <summary>
/// Command to batch create products
/// </summary>
/// <param name="Products">List of products to create</param>
/// <param name="TenantId">Optional tenant ID</param>
public sealed record BatchCreateProductsCommand(
    List<BatchProductCreateItem> Products,
    Guid? TenantId = null
) : ICommand<List<ProductDto>>;

/// <summary>
/// Item for batch product creation
/// </summary>
public record BatchProductCreateItem(
    string Name,
    string? Description = null,
    string? ShortDescription = null,
    string? ImageUrl = null,
    ProductType Type = ProductType.Program,
    bool IsBundle = false,
    Guid? CreatorId = null,
    List<Guid>? BundleItems = null,
    decimal ReferralCommissionPercentage = 30m,
    decimal MaxAffiliateDiscount = 0m,
    decimal AffiliateCommissionPercentage = 30m
);

/// <summary>
/// Handler for BatchCreateProductsCommand
/// </summary>
public sealed class BatchCreateProductsHandler(IProductRepository repository) : ICommandHandler<BatchCreateProductsCommand, List<ProductDto>>
{
    public async Task<List<ProductDto>> Handle(BatchCreateProductsCommand request, CancellationToken cancellationToken)
    {
        var createdProducts = new List<ProductDto>();

        foreach (var item in request.Products)
        {
            var product = Product.Create(
                item.Name,
                item.Type,
                item.Description,
                item.ShortDescription,
                item.ImageUrl,
                item.CreatorId,
                item.IsBundle,
                request.TenantId
            );

            await repository.AddAsync(product, cancellationToken).ConfigureAwait(false);
            createdProducts.Add(product.ToDto());
        }

        return createdProducts;
    }
}

/// <summary>
/// Query to check if a product exists
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="IncludeUnpublished">Whether drafts should be considered</param>
public sealed record ProductExistsQuery(Guid ProductId, bool IncludeUnpublished = false) : IQuery<bool>;

/// <summary>
/// Handler for ProductExistsQuery
/// </summary>
public sealed class ProductExistsHandler(IProductRepository repository) : IQueryHandler<ProductExistsQuery, bool>
{
    public async Task<bool> Handle(ProductExistsQuery request, CancellationToken cancellationToken)
    {
        return await repository.ExistsAsync(
            request.ProductId,
            cancellationToken,
            isPublished: request.IncludeUnpublished ? null : true).ConfigureAwait(false);
    }
}
