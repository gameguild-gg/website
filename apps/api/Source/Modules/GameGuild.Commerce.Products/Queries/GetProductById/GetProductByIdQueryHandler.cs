using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query handler for getting a product by ID
/// </summary>
public sealed class GetProductByIdQueryHandler(IProductRepository productRepository)
    : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await productRepository.GetByIdAsync(
            request.ProductId,
            cancellationToken,
            includePricing: request.IncludePricing,
            includeCreator: request.IncludeCreator,
            isPublished: request.IncludeUnpublished ? null : true
        ).ConfigureAwait(false);

        if (product == null)
            return null;

        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.ShortDescription,
            product.ImageUrl,
            product.Type,
            product.IsBundle,
            product.IsPublished,
            product.CreatorId,
            product.GetBundleProductIds().ToList(),
            product.CommissionConfig?.ReferralCommissionPercentage ?? 0m,
            product.CommissionConfig?.MaxAffiliateDiscount ?? 0m,
            product.CommissionConfig?.AffiliateCommissionPercentage ?? 30m,
            product.CreatedAt,
            product.UpdatedAt,
            request.IncludePricing ? product.Pricing.Select(p => new ProductPricingDto(
                p.Id,
                p.ProductId,
                p.Name,
                p.BasePrice,
                p.SalePrice,
                p.Currency,
                p.SaleStartDate,
                p.SaleEndDate,
                p.IsDefault,
                p.GetCurrentPrice(),
                p.IsSaleActive(),
                p.GetCurrentActiveVersion()?.Id
            )).ToList() : null,
            product.TenantId,
            product.CategoryId
        );
    }
}
