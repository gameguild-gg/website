using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Query to list product categories for a tenant's catalog (issue #401).
/// </summary>
/// <param name="TenantId">Tenant scope (from actor context; null for global)</param>
/// <param name="ParentCategoryId">Only direct children of this category</param>
/// <param name="RootsOnly">Only root categories</param>
/// <param name="IncludeProductCounts">Attach product counts per category</param>
public sealed record GetProductCategoriesQuery(
    Guid? TenantId = null,
    Guid? ParentCategoryId = null,
    bool RootsOnly = false,
    bool IncludeProductCounts = false
) : IQuery<List<ProductCategoryDto>>;

/// <summary>
///     Handler for <see cref="GetProductCategoriesQuery"/>: loads the full (small)
///     per-tenant category list once, then maps with depth/path and optional counts.
/// </summary>
public sealed class GetProductCategoriesQueryHandler(IProductCategoryRepository categoryRepository)
    : IQueryHandler<GetProductCategoriesQuery, List<ProductCategoryDto>>
{
    public async Task<List<ProductCategoryDto>> Handle(GetProductCategoriesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var categories = await categoryRepository
            .ListAsync(request.TenantId, request.ParentCategoryId, request.RootsOnly, cancellationToken)
            .ConfigureAwait(false);

        if (categories.Count == 0)
            return [];

        // Depth/path need the whole tree even when returning a filtered subset.
        var allCategories = await categoryRepository.ListAsync(request.TenantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var categoriesById = allCategories.ToDictionary(c => c.Id);

        Dictionary<Guid, int>? productCounts = null;
        if (request.IncludeProductCounts)
        {
            productCounts = [];
            foreach (var categoryId in categories.Select(c => c.Id).Distinct())
            {
                productCounts[categoryId] = await categoryRepository
                    .CountProductsInCategoryAsync(categoryId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return categories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => c.ToDto(categoriesById, productCounts?.GetValueOrDefault(c.Id)))
            .ToList();
    }
}

/// <summary>
///     Query to get a single product category by ID (issue #401).
/// </summary>
/// <param name="CategoryId">Category ID</param>
public sealed record GetProductCategoryByIdQuery(Guid CategoryId) : IQuery<ProductCategoryDto?>;

/// <summary>Handler for <see cref="GetProductCategoryByIdQuery"/>.</summary>
public sealed class GetProductCategoryByIdQueryHandler(IProductCategoryRepository categoryRepository)
    : IQueryHandler<GetProductCategoryByIdQuery, ProductCategoryDto?>
{
    public async Task<ProductCategoryDto?> Handle(GetProductCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken).ConfigureAwait(false);
        if (category is null)
            return null;

        var categories = await categoryRepository.ListAsync(category.TenantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var categoriesById = categories.ToDictionary(c => c.Id);
        categoriesById[category.Id] = category;

        return category.ToDto(categoriesById);
    }
}
