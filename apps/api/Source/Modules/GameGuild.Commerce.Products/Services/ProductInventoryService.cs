namespace GameGuild.Commerce.Products;

/// <summary>
///     Inventory coordination for the order pipeline (issue #401). All operations are
///     additive no-ops for products whose stock is not tracked, so existing digital-goods
///     flows behave exactly as before.
/// </summary>
public interface IProductInventoryService
{
    /// <summary>
    ///     Verifies availability and reserves <paramref name="quantity"/> units for a
    ///     pending order line. Untracked products always succeed.
    /// </summary>
    /// <returns>False when tracked stock is insufficient (nothing is written).</returns>
    Task<bool> TryReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Converts reservations into definitive stock decrements when an order is
    ///     fulfilled. Untracked products are skipped. Persists nothing by itself.
    /// </summary>
    /// <returns>False when a reservation can no longer be honored (stock shrank meanwhile).</returns>
    Task<bool> TryCommitSaleAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Releases reservations when a pending order is cancelled. Untracked products
    ///     are skipped. Best-effort per product; never throws for missing rows.
    /// </summary>
    Task ReleaseAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Restores stock after a refund. Untracked products are skipped.
    /// </summary>
    Task RestoreAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default implementation over <see cref="IProductInventoryRepository"/>. Mutations
///     ride the caller's unit of work: the order handlers save within their transaction,
///     and <see cref="EntityBase{TKey}.Version"/> optimistic concurrency on the inventory
///     row guards concurrent checkouts from overselling.
/// </summary>
public class ProductInventoryService(IProductInventoryRepository inventoryRepository)
    : IProductInventoryService
{
    public async Task<bool> TryReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var inventory = await inventoryRepository.GetByProductIdAsync(productId, cancellationToken).ConfigureAwait(false);
        if (inventory is null || !inventory.TrackInventory)
            return true;

        if (inventory.AvailableQuantity < quantity)
            return false;

        inventory.Reserve(quantity);
        await inventoryRepository.UpdateAsync(inventory, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryCommitSaleAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var inventories = (await inventoryRepository.GetByProductIdsAsync(productIds, cancellationToken).ConfigureAwait(false))
            .Where(inventory => inventory.TrackInventory)
            .ToDictionary(inventory => inventory.ProductId);

        foreach (var (productId, quantity) in lines)
        {
            if (!inventories.TryGetValue(productId, out var inventory))
                continue;

            if (inventory.ReservedQuantity < quantity || inventory.StockQuantity < quantity)
                return false;
        }

        foreach (var (productId, quantity) in lines)
        {
            if (inventories.TryGetValue(productId, out var inventory))
            {
                inventory.CommitReservation(quantity);
                await inventoryRepository.UpdateAsync(inventory, cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }

    public async Task ReleaseAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var inventories = (await inventoryRepository.GetByProductIdsAsync(productIds, cancellationToken).ConfigureAwait(false))
            .Where(inventory => inventory.TrackInventory)
            .ToDictionary(inventory => inventory.ProductId);

        foreach (var (productId, quantity) in lines)
        {
            if (inventories.TryGetValue(productId, out var inventory))
            {
                inventory.ReleaseReservation(quantity);
                await inventoryRepository.UpdateAsync(inventory, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task RestoreAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var inventories = (await inventoryRepository.GetByProductIdsAsync(productIds, cancellationToken).ConfigureAwait(false))
            .Where(inventory => inventory.TrackInventory)
            .ToDictionary(inventory => inventory.ProductId);

        foreach (var (productId, quantity) in lines)
        {
            if (inventories.TryGetValue(productId, out var inventory))
            {
                inventory.Restock(quantity);
                await inventoryRepository.UpdateAsync(inventory, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
