namespace GameGuild.Commerce.Products;

/// <summary>
///     Validates product → category assignments (issue #401): the category must exist,
///     be non-deleted, and belong to the same tenant as the product. Kept as one static
///     helper so create/update/patch product handlers apply identical rules.
/// </summary>
public static class ProductCategoryAssignment
{
    /// <summary>
    ///     Throws <see cref="ProductCategoryValidationException"/> when
    ///     <paramref name="categoryId"/> is not a usable category for <paramref name="product"/>.
    /// </summary>
    public static async Task ValidateAsync(
        IProductCategoryRepository categoryRepository,
        Product product,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(product);

        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            throw new ProductCategoryValidationException(
                $"Product category '{categoryId}' does not exist", CategoryValidationError.UnknownParent);
        }

        if (category.TenantId != product.TenantId)
        {
            throw new ProductCategoryValidationException(
                $"Product category '{categoryId}' belongs to a different tenant", CategoryValidationError.None);
        }
    }
}
