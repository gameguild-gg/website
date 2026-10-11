using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Shared mutation helpers for pricing rule commands (issue #395).
/// </summary>
internal static class PricingRuleMutationHelpers
{
    /// <summary>
    /// Replaces the rule's volume tiers with the requested set. <see cref="PricingRule.PricingTiers"/>
    /// has a get-only initializer, so tiers are mutated in place; EF Core cascades the removal of
    /// orphaned tiers on save.
    /// </summary>
    public static void ApplyTiers(PricingRule rule, IReadOnlyList<PricingRuleTierRequest>? tiers)
    {
        ArgumentNullException.ThrowIfNull(rule);

        rule.PricingTiers.Clear();

        if (tiers is null || tiers.Count == 0)
        {
            return;
        }

        foreach (var tier in tiers)
        {
            rule.PricingTiers.Add(new PricingRuleTier
            {
                Id = Guid.NewGuid(),
                PricingRuleId = rule.Id,
                MinQuantity = tier.MinQuantity,
                MaxQuantity = tier.MaxQuantity,
                Price = tier.Price,
                DiscountPercentage = tier.DiscountPercentage
            });
        }
    }
}
