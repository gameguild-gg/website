using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Per-product inventory state (issue #401). One row per product (optional —
///     a missing row means the product's stock is not tracked, which preserves the
///     existing digital-goods behavior of unlimited availability).
///     When <see cref="TrackInventory"/> is enabled, add-to-order reserves stock,
///     completing the order converts the reservation into a decrement, cancelling a
///     pending order releases it, and refunds restore it.
/// </summary>
[Table("product_inventory")]
[Index(nameof(ProductId), IsUnique = true)]
[Index(nameof(TrackInventory))]
public class ProductInventory : EntityBase
{
    /// <summary>Default constructor (EF Core).</summary>
    public ProductInventory() { }

    /// <summary>Constructor for partial initialization.</summary>
    public ProductInventory(object partial) : base(partial) { }

    /// <summary>Product this inventory row belongs to (one-to-one).</summary>
    [Required]
    public Guid ProductId { get; set; }

    /// <summary>Navigation property to the product.</summary>
    [ForeignKey(nameof(ProductId))]
    public virtual Product Product { get; set; } = null!;

    /// <summary>
    ///     Units currently on hand. Only meaningful when <see cref="TrackInventory"/>
    ///     is enabled.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    ///     Units reserved by pending orders (added at add-to-order, released on
    ///     cancel, converted into a stock decrement on completion).
    /// </summary>
    public int ReservedQuantity { get; set; }

    /// <summary>
    ///     Whether stock is tracked for this product. Defaults to <c>false</c>
    ///     (unlimited), keeping purely digital products unaffected.
    /// </summary>
    public bool TrackInventory { get; set; }

    /// <summary>
    ///     Units below (or equal to) which the product is flagged low on stock in
    ///     the availability endpoint. Only meaningful when tracking is enabled.
    /// </summary>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>
    ///     Units still sellable: on-hand minus reserved. Untracked inventory is
    ///     treated as unbounded (callers must check <see cref="TrackInventory"/> first).
    /// </summary>
    public int AvailableQuantity => StockQuantity - ReservedQuantity;

    /// <summary>Whether at least <paramref name="quantity"/> units are available.</summary>
    public bool IsInStock(int quantity = 1) => TrackInventory && AvailableQuantity >= quantity && quantity > 0;

    /// <summary>Whether available stock fell to the configured threshold or below.</summary>
    public bool IsLowStock => TrackInventory && AvailableQuantity <= LowStockThreshold;

    /// <summary>
    ///     Factory method. Stock starts at zero and tracking disabled unless requested.
    /// </summary>
    public static ProductInventory Create(
        Guid productId,
        int stockQuantity = 0,
        bool trackInventory = false,
        int lowStockThreshold = 5,
        Guid? tenantId = null)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID is required", nameof(productId));

        if (stockQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock quantity cannot be negative");

        if (lowStockThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(lowStockThreshold), "Low-stock threshold cannot be negative");

        return new ProductInventory
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            StockQuantity = stockQuantity,
            ReservedQuantity = 0,
            TrackInventory = trackInventory,
            LowStockThreshold = lowStockThreshold,
            TenantId = tenantId
        };
    }

    /// <summary>
    ///     Reserves <paramref name="quantity"/> units for a pending order line.
    ///     Untracked inventory is a no-op (unlimited). Throws
    ///     <see cref="InvalidOperationException"/> when insufficient stock is available.
    /// </summary>
    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Reservation quantity must be positive");

        if (!TrackInventory)
            return;

        if (AvailableQuantity < quantity)
            throw new InvalidOperationException(
                $"Insufficient stock for product {ProductId}: requested {quantity}, available {AvailableQuantity}");

        ReservedQuantity += quantity;
        Touch();
    }

    /// <summary>
    ///     Releases a reservation (order cancelled or item removed before payment).
    ///     Untracked inventory is a no-op.
    /// </summary>
    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Release quantity must be positive");

        if (!TrackInventory)
            return;

        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        Touch();
    }

    /// <summary>
    ///     Converts an existing reservation into a definitive stock decrement
    ///     (order completed / fulfilled). Untracked inventory is a no-op. Throws
    ///     <see cref="InvalidOperationException"/> when the reservation cannot be
    ///     honored (for example stock was corrected downward in the meantime).
    /// </summary>
    public void CommitReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Commit quantity must be positive");

        if (!TrackInventory)
            return;

        if (ReservedQuantity < quantity || StockQuantity < quantity)
            throw new InvalidOperationException(
                $"Cannot commit reservation of {quantity} for product {ProductId}: reserved {ReservedQuantity}, on hand {StockQuantity}");

        ReservedQuantity -= quantity;
        StockQuantity -= quantity;
        Touch();
    }

    /// <summary>
    ///     Restores units to stock after a refund. Untracked inventory is a no-op.
    /// </summary>
    public void Restock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Restock quantity must be positive");

        if (!TrackInventory)
            return;

        StockQuantity += quantity;
        Touch();
    }

    /// <summary>
    ///     Replaces stock configuration (admin). Reserved units are preserved and
    ///     validated against the new on-hand quantity when tracking stays enabled.
    /// </summary>
    public void Configure(int stockQuantity, bool trackInventory, int lowStockThreshold)
    {
        if (stockQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock quantity cannot be negative");

        if (lowStockThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(lowStockThreshold), "Low-stock threshold cannot be negative");

        if (trackInventory && stockQuantity < ReservedQuantity)
            throw new InvalidOperationException(
                $"Cannot set stock to {stockQuantity}: {ReservedQuantity} units are already reserved");

        StockQuantity = stockQuantity;
        TrackInventory = trackInventory;
        LowStockThreshold = lowStockThreshold;
        Touch();
    }
}
