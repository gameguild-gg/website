using System.Text.Json.Serialization;

namespace GameGuild.Commerce.Products;

/// <summary>
///     DTO for a product catalog category (issue #401).
/// </summary>
/// <param name="Id">Category ID</param>
/// <param name="Name">Display name</param>
/// <param name="Slug">URL-safe identifier (tenant-unique)</param>
/// <param name="Description">Optional description</param>
/// <param name="ParentCategoryId">Parent category (null = root)</param>
/// <param name="Depth">Depth in the tree (root = 0)</param>
/// <param name="Path">Slugs from root to this category</param>
/// <param name="Attributes">Metadata dictionary (≤ 20 keys)</param>
/// <param name="DisplayOrder">Sort order</param>
/// <param name="ProductCount">Non-deleted products assigned to this category (when requested)</param>
/// <param name="CreatedAt">Creation timestamp</param>
/// <param name="UpdatedAt">Last update timestamp</param>
/// <param name="TenantId">Internal tenant scope; omitted from JSON responses</param>
public sealed record ProductCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentCategoryId,
    int Depth,
    List<string> Path,
    Dictionary<string, string> Attributes,
    int DisplayOrder,
    int? ProductCount = null,
    DateTime CreatedAt = default,
    DateTime UpdatedAt = default,
    [property: JsonIgnore] Guid? TenantId = null
);

/// <summary>
///     DTO for a product's real-time availability (issue #401).
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="TrackInventory">Whether stock is tracked for this product</param>
/// <param name="StockQuantity">Units on hand (when tracked)</param>
/// <param name="ReservedQuantity">Units reserved by pending orders (when tracked)</param>
/// <param name="AvailableQuantity">Units sellable now (when tracked)</param>
/// <param name="IsInStock">Whether at least one unit is available (untracked = always true)</param>
/// <param name="IsLowStock">Whether available stock fell to the low-stock threshold</param>
/// <param name="LowStockThreshold">Configured low-stock threshold</param>
public sealed record ProductAvailabilityDto(
    Guid ProductId,
    bool TrackInventory,
    int? StockQuantity,
    int? ReservedQuantity,
    int? AvailableQuantity,
    bool IsInStock,
    bool IsLowStock,
    int LowStockThreshold
);

/// <summary>
///     DTO for a product's inventory configuration (issue #401).
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="TrackInventory">Whether stock is tracked</param>
/// <param name="StockQuantity">Units on hand</param>
/// <param name="ReservedQuantity">Units reserved by pending orders</param>
/// <param name="AvailableQuantity">Units sellable now</param>
/// <param name="LowStockThreshold">Low-stock threshold</param>
public sealed record ProductInventoryDto(
    Guid ProductId,
    bool TrackInventory,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int LowStockThreshold
);
