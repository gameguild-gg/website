namespace GameGuild.Commerce.Products;

/// <summary>
///     Bundle-price optimization hint (issue #401): compares a bundle's effective price
///     against the sum of its parts (component effective prices from default
///     <see cref="ProductPricing"/> data, adjusted by each
///     <see cref="ProductBundleItem.BundleDiscountPercentage"/>) so storefronts can show
///     "save X" badges and admins can spot mispriced bundles.
/// </summary>
public interface IBundleSavingsCalculator
{
    /// <summary>
    ///     Computes the savings hint for one bundle against the provided component products.
    /// </summary>
    /// <param name="bundle">Bundle product (its default pricing is the bundle price).</param>
    /// <param name="components">Included products keyed by ID (their default pricings are the parts).</param>
    /// <returns>Null when either side lacks a default price (nothing to compare).</returns>
    BundleSavingsHint? Calculate(Product bundle, IReadOnlyDictionary<Guid, Product> components);
}

/// <summary>
///     Deterministic savings computation. Pure function of the loaded entities so it is
///     unit-testable without infrastructure.
/// </summary>
public sealed class BundleSavingsCalculator : IBundleSavingsCalculator
{
    /// <inheritdoc />
    public BundleSavingsHint? Calculate(Product bundle, IReadOnlyDictionary<Guid, Product> components)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(components);

        var bundlePrice = GetEffectiveDefaultPrice(bundle);
        if (bundlePrice is null)
            return null;

        decimal? sumOfParts = 0m;
        var pricedParts = 0;
        foreach (var item in bundle.BundleItems)
        {
            if (!components.TryGetValue(item.IncludedProductId, out var component))
                return null;

            var partPrice = GetEffectiveDefaultPrice(component);
            if (partPrice is null)
                return null;

            var discountMultiplier = item.BundleDiscountPercentage.HasValue
                ? 1m - item.BundleDiscountPercentage.Value / 100m
                : 1m;

            sumOfParts += partPrice.Value * item.Quantity * discountMultiplier;
            pricedParts++;
        }

        if (sumOfParts is null || sumOfParts.Value <= 0)
            return null;

        var savings = sumOfParts.Value - bundlePrice.Value;
        return new BundleSavingsHint(
            bundle.Id,
            BundlePrice: bundlePrice.Value,
            SumOfParts: sumOfParts.Value,
            Savings: savings,
            SavingsPercentage: Math.Round(savings / sumOfParts.Value * 100m, 2, MidpointRounding.AwayFromZero),
            Currency: GetDefaultCurrency(bundle),
            ComponentsPriced: pricedParts);
    }

    /// <summary>
    ///     Effective unit price for a product: active sale price on the default pricing
    ///     when a sale is running, otherwise the default base price.
    /// </summary>
    internal static decimal? GetEffectiveDefaultPrice(Product product)
    {
        var defaultPricing = product.Pricing?.FirstOrDefault(p => p.IsDefault)
            ?? product.Pricing?.OrderByDescending(p => p.UpdatedAt).FirstOrDefault();
        if (defaultPricing is null)
            return null;

        return defaultPricing.IsSaleActive() && defaultPricing.SalePrice.HasValue
            ? defaultPricing.SalePrice.Value
            : defaultPricing.BasePrice;
    }

    internal static string? GetDefaultCurrency(Product product)
    {
        var defaultPricing = product.Pricing?.FirstOrDefault(p => p.IsDefault)
            ?? product.Pricing?.OrderByDescending(p => p.UpdatedAt).FirstOrDefault();
        return defaultPricing?.Currency;
    }
}

/// <summary>
///     Bundle-price optimization hint (issue #401): bundle price vs sum-of-parts.
/// </summary>
/// <param name="BundleId">Bundle product ID</param>
/// <param name="BundlePrice">Effective price of the bundle itself</param>
/// <param name="SumOfParts">Sum of component effective prices (after per-item bundle discounts)</param>
/// <param name="Savings">SumOfParts − BundlePrice (negative when the bundle costs more)</param>
/// <param name="SavingsPercentage">Savings as a percentage of SumOfParts</param>
/// <param name="Currency">Currency of the comparison</param>
/// <param name="ComponentsPriced">Number of priced components included</param>
public sealed record BundleSavingsHint(
    Guid BundleId,
    decimal BundlePrice,
    decimal SumOfParts,
    decimal Savings,
    decimal SavingsPercentage,
    string? Currency,
    int ComponentsPriced
);
