namespace GameGuild.Commerce.Products;

/// <summary>
/// Rule-driven promo stacking resolution.
///
/// Resolution contract:
/// - Codes are applied in <c>StackingPriority</c> order (higher first; ties broken alphabetically).
/// - With no active <see cref="PromoStackingRule"/>, only a single code (the highest-priority valid one) applies.
/// - With an active rule, resolution honors the rule's conflict strategy, exclusive-code
///   stop-at-non-stackable behavior, <c>MaxStackableCount</c>, same-type restriction, and
///   total-discount caps. Percentage discounts apply to the already-reduced amount under the
///   <see cref="ConflictResolutionStrategy.Sequential"/> strategy.
/// </summary>
public static class PromoStackingResolver
{
    /// <summary>
    /// Resolve which promo codes apply and the discount each contributes.
    /// </summary>
    /// <param name="orderAmount">Amount the codes are being applied to (must be positive)</param>
    /// <param name="candidates">Validated promo codes eligible for selection</param>
    /// <param name="rule">Active stacking rule, or null for the single-code default</param>
    /// <returns>Applied codes with discounts, rejected codes with reasons, and the governing rule name</returns>
    public static (List<AppliedPromoCode> Applied, List<RejectedPromoCode> Rejected, string? RuleName) Resolve(
        decimal orderAmount,
        IReadOnlyList<PromoCode> candidates,
        PromoStackingRule? rule)
    {
        var applied = new List<AppliedPromoCode>();
        var rejected = new List<RejectedPromoCode>();

        if (orderAmount <= 0 || candidates.Count == 0)
        {
            return (applied, rejected, rule?.Name);
        }

        // Priority-based application order: highest StackingPriority first, ties alphabetical.
        var ordered = candidates
            .OrderByDescending(c => c.StackingPriority)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();

        if (rule is null)
        {
            // Single-code default: only the highest-priority code applies.
            var winner = ordered[0];
            applied.Add(CreateApplied(winner, winner.CalculateDiscount(orderAmount)));
            foreach (var loser in ordered.Skip(1))
            {
                rejected.Add(new RejectedPromoCode(loser.Code, "Stacking is not enabled for these codes (single code applies by default)"));
            }

            return (applied, rejected, null);
        }

        var selected = rule.ConflictStrategy switch
        {
            ConflictResolutionStrategy.HighestDiscount => SingleByStandaloneDiscount(ordered, orderAmount, ascending: false, rejected),
            ConflictResolutionStrategy.LowestDiscount => SingleByStandaloneDiscount(ordered, orderAmount, ascending: true, rejected),
            ConflictResolutionStrategy.FirstCodeOnly => SingleByPosition(ordered, first: true, rejected),
            ConflictResolutionStrategy.LastCodeOnly => SingleByPosition(ordered, first: false, rejected),
            _ => SelectStackable(ordered, rule, rejected)
        };

        if (selected.Count == 0)
        {
            return (applied, rejected, rule.Name);
        }

        // Compute discounts. Sequential applies each code to the already-reduced amount;
        // additive computes each on the original amount and sums.
        var additive = rule.ConflictStrategy == ConflictResolutionStrategy.Additive;
        var remaining = orderAmount;
        var rawTotal = 0m;
        foreach (var code in selected)
        {
            var baseForCode = additive ? orderAmount : remaining;
            var discount = code.CalculateDiscount(baseForCode);
            applied.Add(CreateApplied(code, discount));
            rawTotal += discount;
            remaining = Math.Max(0, remaining - discount);
        }

        // Total-discount caps (percentage of the original amount and/or absolute amount).
        var cap = orderAmount;
        if (rule.MaxTotalDiscountPercentage.HasValue)
        {
            cap = Math.Min(cap, orderAmount * rule.MaxTotalDiscountPercentage.Value / 100m);
        }

        if (rule.MaxTotalDiscountAmount.HasValue)
        {
            cap = Math.Min(cap, rule.MaxTotalDiscountAmount.Value);
        }

        if (rawTotal > cap && rawTotal > 0)
        {
            applied = ClampToCap(applied, cap, orderAmount);
        }

        return (applied, rejected, rule.Name);
    }

    private static List<PromoCode> SingleByStandaloneDiscount(
        List<PromoCode> ordered,
        decimal orderAmount,
        bool ascending,
        List<RejectedPromoCode> rejected)
    {
        PromoCode? winner = null;
        var winnerDiscount = ascending ? decimal.MaxValue : decimal.MinValue;

        // ordered is already priority-sorted; the first tie wins by using strict comparison.
        foreach (var code in ordered)
        {
            var discount = code.CalculateDiscount(orderAmount);
            var wins = ascending ? discount < winnerDiscount : discount > winnerDiscount;
            if (wins)
            {
                winner = code;
                winnerDiscount = discount;
            }
        }

        foreach (var loser in ordered.Where(c => !ReferenceEquals(c, winner)))
        {
            rejected.Add(new RejectedPromoCode(loser.Code, "Conflict strategy selected a single best code"));
        }

        return winner is null ? [] : [winner];
    }

    private static List<PromoCode> SingleByPosition(
        List<PromoCode> ordered,
        bool first,
        List<RejectedPromoCode> rejected)
    {
        var winner = first ? ordered[0] : ordered[^1];
        foreach (var loser in ordered.Where(c => !ReferenceEquals(c, winner)))
        {
            rejected.Add(new RejectedPromoCode(loser.Code, "Conflict strategy selected a single code by position"));
        }

        return [winner];
    }

    private static List<PromoCode> SelectStackable(
        List<PromoCode> ordered,
        PromoStackingRule rule,
        List<RejectedPromoCode> rejected)
    {
        var selected = new List<PromoCode>();
        var maxCount = rule.MaxStackableCount > 0 ? rule.MaxStackableCount : 1;

        foreach (var code in ordered)
        {
            // Stop at non-stackable: an exclusive code either applies alone (when first)
            // or terminates accumulation.
            if (code.IsExclusive && !rule.AllowExclusiveStacking)
            {
                if (selected.Count == 0)
                {
                    selected.Add(code);
                    RejectRest(ordered, code, rejected, "Cannot stack with exclusive promo code");
                    return selected;
                }

                RejectRest(ordered, code, rejected, "Stopped at non-stackable (exclusive) promo code");
                return selected;
            }

            if (!rule.AllowSameTypeStacking && selected.Any(s => s.Type == code.Type))
            {
                rejected.Add(new RejectedPromoCode(code.Code, "Cannot stack multiple codes of the same discount type"));
                continue;
            }

            if (selected.Count >= maxCount)
            {
                rejected.Add(new RejectedPromoCode(code.Code, $"Stacking limit of {maxCount} codes reached"));
                continue;
            }

            selected.Add(code);
        }

        return selected;
    }

    private static void RejectRest(
        List<PromoCode> ordered,
        PromoCode stopCode,
        List<RejectedPromoCode> rejected,
        string stopReason)
    {
        var stopIndex = ordered.IndexOf(stopCode);
        rejected.Add(new RejectedPromoCode(stopCode.Code, stopReason));
        foreach (var rest in ordered.Skip(stopIndex + 1))
        {
            rejected.Add(new RejectedPromoCode(rest.Code, $"Not applied after {stopCode.Code} stopped stacking"));
        }
    }

    private static AppliedPromoCode CreateApplied(PromoCode code, decimal discount)
    {
        return new AppliedPromoCode(
            code.Code,
            decimal.Round(discount, 2, MidpointRounding.AwayFromZero),
            code.DiscountPercentage,
            code.Type,
            code.Type == PromoCodeType.FreeTrialExtension ? code.FreeTrialExtensionDays : null,
            code.Type == PromoCodeType.FreeMonths ? code.DurationMonths : null);
    }

    private static List<AppliedPromoCode> ClampToCap(List<AppliedPromoCode> applied, decimal cap, decimal orderAmount)
    {
        var rawTotal = applied.Sum(a => a.DiscountAmount);
        if (rawTotal <= 0)
        {
            return applied;
        }

        cap = Math.Min(cap, orderAmount);
        var clamped = new List<AppliedPromoCode>(applied.Count);
        var allocated = 0m;
        for (var i = 0; i < applied.Count; i++)
        {
            var entry = applied[i];
            decimal scaled;
            if (i == applied.Count - 1)
            {
                // Last entry absorbs rounding residue so the sum is exactly the cap.
                scaled = cap - allocated;
            }
            else
            {
                scaled = decimal.Round(entry.DiscountAmount * cap / rawTotal, 2, MidpointRounding.AwayFromZero);
                scaled = Math.Min(scaled, cap - allocated);
            }

            allocated += scaled;
            clamped.Add(entry with { DiscountAmount = Math.Max(0, scaled) });
        }

        return clamped;
    }
}
