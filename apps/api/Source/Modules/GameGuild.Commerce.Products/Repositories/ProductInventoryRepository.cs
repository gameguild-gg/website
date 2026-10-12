using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Repository interface for <see cref="ProductInventory"/> (issue #401).
/// </summary>
public interface IProductInventoryRepository
{
    /// <summary>Get the inventory row for a product (null = stock not tracked).</summary>
    Task<ProductInventory?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Get inventory rows for several products at once.</summary>
    Task<List<ProductInventory>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);

    /// <summary>Add a new inventory row.</summary>
    Task AddAsync(ProductInventory inventory, CancellationToken cancellationToken = default);

    /// <summary>Update an existing inventory row.</summary>
    Task UpdateAsync(ProductInventory inventory, CancellationToken cancellationToken = default);

    /// <summary>Save changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Repository implementation for <see cref="ProductInventory"/> (issue #401).
/// </summary>
public class ProductInventoryRepository(IApplicationDbContext context) : IProductInventoryRepository
{
    public Task<ProductInventory?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return Context.Set<ProductInventory>()
            .Where(i => i.ProductId == productId && i.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<ProductInventory>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        return Context.Set<ProductInventory>()
            .Where(i => i.DeletedAt == null && productIds.Contains(i.ProductId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ProductInventory inventory, CancellationToken cancellationToken = default)
    {
        await Context.Set<ProductInventory>().AddAsync(inventory, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ProductInventory inventory, CancellationToken cancellationToken = default)
    {
        Context.Set<ProductInventory>().Update(inventory);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
