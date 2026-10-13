using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to get a paginated list of products
/// </summary>
/// <param name="Type">Filter by product type</param>
/// <param name="CreatorId">Filter by creator ID</param>
/// <param name="SearchTerm">Search term for name/description</param>
/// <param name="IsBundle">Filter by bundle flag</param>
/// <param name="IncludeUnpublished">Whether drafts should be visible</param>
/// <param name="Skip">Number of items to skip</param>
/// <param name="Take">Number of items to take</param>
/// <param name="SortBy">Sort field (Name, CreatedAt)</param>
/// <param name="SortDirection">Sort direction (ASC, DESC)</param>
/// <param name="TenantId">Internal tenant filter for draft ownership</param>
/// <param name="CategoryId">Filter by category, including descendant categories (issue #401)</param>
/// <param name="InStockOnly">Filter by real-time availability: true = in stock only, false = out of stock only (issue #401)</param>
public sealed record GetProductsPagedQuery(
    ProductType? Type = null,
    Guid? CreatorId = null,
    string? SearchTerm = null,
    bool? IsBundle = null,
    bool IncludeUnpublished = false,
    int Skip = 0,
    int Take = 50,
    string SortBy = "CreatedAt",
    string SortDirection = "DESC",
    Guid? TenantId = null,
    Guid? CategoryId = null,
    bool? InStockOnly = null
) : IQuery<PagedResult<ProductDto>>;
