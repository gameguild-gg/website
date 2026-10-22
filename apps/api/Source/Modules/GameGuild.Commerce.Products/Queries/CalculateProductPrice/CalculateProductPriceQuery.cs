using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to calculate the final price for a product
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="PricingId">Optional specific pricing ID to use</param>
/// <param name="PromoCodes">Optional list of promo codes to apply</param>
/// <param name="UserId">Optional user ID for user-specific discounts</param>
/// <param name="UsageMetrics">Optional usage metric values; gate usage-conditioned pricing rules (issue #395)</param>
/// <param name="ExperimentBucketKey">Optional A/B experiment bucket key (issue #395)</param>
public sealed record CalculateProductPriceQuery(
    Guid ProductId,
    Guid? PricingId = null,
    List<string>? PromoCodes = null,
    Guid? UserId = null,
    Dictionary<string, decimal>? UsageMetrics = null,
    string? ExperimentBucketKey = null
) : IQuery<PricingCalculationResult>;
