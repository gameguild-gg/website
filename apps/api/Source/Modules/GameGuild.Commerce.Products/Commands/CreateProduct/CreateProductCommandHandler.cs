using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command handler for creating a new product
/// </summary>
public sealed class CreateProductCommandHandler(IProductRepository productRepository, IProductCategoryRepository categoryRepository)
    : ICommandHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Create new product entity with commission configuration
        var (product, commissionConfig) = Product.CreateWithCommission(
            request.Name,
            request.Type,
            request.Description,
            request.ShortDescription,
            request.ImageUrl,
            request.CreatorId,
            request.IsBundle,
            request.ReferralCommissionPercentage,
            request.AffiliateCommissionPercentage,
            request.MaxAffiliateDiscount,
            request.TenantId
        );

        // Associate the commission config
        product.CommissionConfig = commissionConfig;

        // Category assignment (issue #401): validated against the tenant's catalog first.
        if (request.CategoryId.HasValue)
        {
            await ProductCategoryAssignment.ValidateAsync(categoryRepository, product, request.CategoryId.Value, cancellationToken).ConfigureAwait(false);
            product.CategoryId = request.CategoryId.Value;
        }

        // Set bundle items if applicable
        if (request.IsBundle && request.BundleItems?.Count > 0)
        {
#pragma warning disable CS0618 // Suppress obsolete warning for backwards compatibility
            product.SetBundleItemIds(request.BundleItems);
#pragma warning restore CS0618
        }

        // Add to repository
        await productRepository.AddAsync(product, cancellationToken).ConfigureAwait(false);
        await productRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return product.ToDto();
    }
}
