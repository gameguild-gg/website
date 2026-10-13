using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query handler for getting a paginated list of products. Category filtering (issue #401)
/// resolves the requested category plus all descendants before querying products so a
/// parent category returns its whole subtree; the availability filter excludes (or
/// isolates) products whose tracked inventory is exhausted.
/// </summary>
public sealed class GetProductsPagedQueryHandler(
    IProductRepository productRepository,
    IProductCategoryRepository categoryRepository)
    : IQueryHandler<GetProductsPagedQuery, PagedResult<ProductDto>>
{
    public async Task<PagedResult<ProductDto>> Handle(GetProductsPagedQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<Guid>? categoryIds = null;
        if (request.CategoryId.HasValue)
        {
            categoryIds = await categoryRepository
                .GetCategoryAndDescendantIdsAsync(request.CategoryId.Value, request.TenantId, cancellationToken)
                .ConfigureAwait(false);
            if (categoryIds.Count == 0)
            {
                // Unknown category: deterministic empty result instead of an unfiltered listing.
                return new PagedResult<ProductDto>([], 0, request.Skip, request.Take);
            }
        }

        var (products, totalCount) = await productRepository.GetPagedAsync(
            type: request.Type,
            creatorId: request.CreatorId,
            searchTerm: request.SearchTerm,
            isBundle: request.IsBundle,
            isPublished: request.IncludeUnpublished ? null : true,
            skip: request.Skip,
            take: request.Take,
            sortBy: request.SortBy,
            sortDirection: request.SortDirection,
            tenantId: request.TenantId,
            cancellationToken: cancellationToken,
            categoryIds: categoryIds,
            inStockOnly: request.InStockOnly
        ).ConfigureAwait(false);

        var dtos = products.Select(product => new ProductDto(
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
            TenantId: product.TenantId,
            CategoryId: product.CategoryId
        )).ToList();

        return new PagedResult<ProductDto>(
            dtos,
            totalCount,
            request.Skip,
            request.Take
        );
    }
}
