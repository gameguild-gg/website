namespace GameGuild.Commerce.Products;

/// <summary>
///     Repository interface for <see cref="ProductCategory"/> entities (issue #401).
/// </summary>
public interface IProductCategoryRepository
{
    /// <summary>Get a category by ID (non-deleted only).</summary>
    Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get a category by slug within a tenant (non-deleted only).</summary>
    Task<ProductCategory?> GetBySlugAsync(string slug, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     List non-deleted categories for a tenant, optionally filtered to direct
    ///     children of <paramref name="parentCategoryId"/> (null = all, including roots).
    /// </summary>
    Task<List<ProductCategory>> ListAsync(
        Guid? tenantId,
        Guid? parentCategoryId = null,
        bool rootsOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Id → parent-id map of all non-deleted categories in a tenant, used for
    ///     cycle detection, depth computation and descendant resolution.
    /// </summary>
    Task<Dictionary<Guid, Guid?>> GetParentMapAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves <paramref name="categoryId"/> plus every descendant category ID
    ///     (any depth) for catalog filtering.
    /// </summary>
    Task<List<Guid>> GetCategoryAndDescendantIdsAsync(
        Guid categoryId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Whether any non-deleted category uses <paramref name="categoryId"/> as its parent.</summary>
    Task<bool> HasChildrenAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>Count of non-deleted products assigned to a category.</summary>
    Task<int> CountProductsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>Add a new category.</summary>
    Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default);

    /// <summary>Update an existing category.</summary>
    Task UpdateAsync(ProductCategory category, CancellationToken cancellationToken = default);

    /// <summary>Delete (soft) a category.</summary>
    Task DeleteAsync(ProductCategory category, CancellationToken cancellationToken = default);

    /// <summary>Save changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
