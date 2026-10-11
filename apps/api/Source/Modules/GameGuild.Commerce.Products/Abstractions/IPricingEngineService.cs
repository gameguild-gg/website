namespace GameGuild.Commerce.Products;

/// <summary>
/// Interface for pricing engine service
/// </summary>
public interface IPricingEngineService
{
    /// <summary>
    /// Calculate the final price for a product
    /// </summary>
    /// <param name="product">The product</param>
    /// <param name="pricing">The pricing option to use (null = default pricing)</param>
    /// <param name="promoCodes">Optional list of promo codes to apply</param>
    /// <param name="userId">Optional user ID for user-specific discounts</param>
    /// <param name="quantity">Quantity being priced; drives volume-tier pricing rules (issue #395)</param>
    /// <param name="customerSegment">Optional customer segment for segment-scoped rules (issue #395)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pricing calculation result</returns>
    Task<PricingCalculationResult> CalculatePriceAsync(
        Product product,
        ProductPricing? pricing = null,
        IEnumerable<string>? promoCodes = null,
        Guid? userId = null,
        int quantity = 1,
        string? customerSegment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the final price for a product by ID
    /// </summary>
    Task<PricingCalculationResult> CalculatePriceByIdAsync(
        Guid productId,
        Guid? pricingId = null,
        IEnumerable<string>? promoCodes = null,
        Guid? userId = null,
        int quantity = 1,
        string? customerSegment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply multiple promo codes to an order amount
    /// </summary>
    /// <param name="orderAmount">The order amount</param>
    /// <param name="promoCodes">List of promo codes to apply</param>
    /// <param name="productId">Optional product ID for product-specific codes</param>
    /// <param name="userId">Optional user ID for user-specific validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Promo code application result</returns>
    Task<PromoCodeApplicationResult> ApplyPromoCodesAsync(
        decimal orderAmount,
        IEnumerable<string> promoCodes,
        Guid? productId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a single promo code
    /// </summary>
    /// <param name="code">The promo code string</param>
    /// <param name="orderAmount">The order amount</param>
    /// <param name="productId">Optional product ID</param>
    /// <param name="userId">Optional user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<PromoCodeValidationResult> ValidatePromoCodeAsync(
        string code,
        decimal orderAmount,
        Guid? productId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current effective price for a product
    /// </summary>
    Task<decimal> GetCurrentPriceAsync(
        Guid productId,
        Guid? pricingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a sale is currently active for a pricing option
    /// </summary>
    bool IsSaleActive(ProductPricing pricing);
}
