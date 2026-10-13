namespace GameGuild.Commerce.Payments;

/// <summary>
///     Progressive (utility-style) usage-pricing math for the unified pricing engine
///     (issue #417). Quantity bands are priced at their own per-unit price; quantities
///     beyond the highest band's maximum price at that band's rate. Pure and deterministic
///     so identical inputs always produce identical outputs (idempotency requirement).
/// </summary>
public static class UsageTierCalculator
{
    /// <summary>
    ///     Computes the charge for <paramref name="quantity"/> units. With no tiers the flat
    ///     <paramref name="perUnitPrice"/> applies to the whole quantity; with tiers each
    ///     half-open band [<see cref="PricingUsageTier.MinQuantity"/>,
    ///     <see cref="PricingUsageTier.MaxQuantity"/>) is priced at its own rate.
    /// </summary>
    /// <param name="quantity">Units consumed (must be ≥ 0)</param>
    /// <param name="perUnitPrice">Flat per-unit price used when no tiers are supplied</param>
    /// <param name="tiers">Progressive tier bands (null/empty = flat pricing)</param>
    /// <returns>Total charge, rounded half-away-from-zero to 2 decimals.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when quantity is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when a tier has a negative price or an inverted band.</exception>
    public static decimal Calculate(decimal quantity, decimal perUnitPrice, IReadOnlyList<PricingUsageTier>? tiers)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Usage quantity cannot be negative.");
        }

        if (tiers is null || tiers.Count == 0)
        {
            return decimal.Round(quantity * perUnitPrice, 2, MidpointRounding.AwayFromZero);
        }

        var ordered = tiers.OrderBy(t => t.MinQuantity).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var tier = ordered[i];
            if (tier.PerUnitPrice < 0)
            {
                throw new ArgumentException($"Tier {i} has a negative per-unit price.", nameof(tiers));
            }

            if (tier.MaxQuantity.HasValue && tier.MaxQuantity.Value <= tier.MinQuantity)
            {
                throw new ArgumentException($"Tier {i} band is empty or inverted.", nameof(tiers));
            }
        }

        decimal total = 0;
        var remaining = quantity;

        foreach (var tier in ordered)
        {
            if (remaining <= 0)
            {
                break;
            }

            // Band width: bounded by MaxQuantity when set, otherwise open-ended (absorbs the remainder).
            var bandStart = tier.MinQuantity;
            var bandWidth = tier.MaxQuantity.HasValue
                ? Math.Max(0, tier.MaxQuantity.Value - bandStart)
                : decimal.MaxValue;

            var unitsInBand = Math.Min(remaining, bandWidth);
            if (unitsInBand <= 0)
            {
                continue;
            }

            total += unitsInBand * tier.PerUnitPrice;
            remaining -= unitsInBand;
        }

        return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}
