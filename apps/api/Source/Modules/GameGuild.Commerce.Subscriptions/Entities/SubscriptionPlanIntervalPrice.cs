using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Interval-specific price point for a subscription plan (issue #392): the price a
///     tenant pays when billed on a given cycle (monthly, quarterly, annually, ...).
///     One row per (plan, interval). Plans without a row for an interval fall back to
///     the derived price (monthly &times; months, or the plan's annual price for the
///     annual interval), so an interval price point is always an explicit override that
///     usually encodes a discount for longer commitment. Rows are replaceable only
///     through the pricing command; consumers read them as points-in-time configuration.
/// </summary>
[Table("SubscriptionPlanIntervalPrices")]
public class SubscriptionPlanIntervalPrice : EntityBase
{
    /// <summary>
    ///     Parameterless constructor for EF Core.
    /// </summary>
    private SubscriptionPlanIntervalPrice()
    {
    }

    /// <summary>
    ///     Creates an interval price point for a plan.
    /// </summary>
    public SubscriptionPlanIntervalPrice(Guid planId, BillingCycle interval, long priceInCents)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("PlanId is required.", nameof(planId));
        }

        if (priceInCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceInCents), "Interval price cannot be negative.");
        }

        PlanId = planId;
        Interval = interval;
        PriceInCents = priceInCents;
    }

    /// <summary>
    ///     The plan this price point applies to.
    /// </summary>
    public Guid PlanId { get; private set; }

    /// <summary>
    ///     The billing interval the price applies to (monthly, quarterly, annually, ...).
    /// </summary>
    [Required]
    [MaxLength(20)]
    public BillingCycle Interval { get; private set; }

    /// <summary>
    ///     Price for the full interval in the smallest currency unit (cents for USD).
    /// </summary>
    public long PriceInCents { get; private set; }

    /// <summary>
    ///     Replaces the price of this interval point. Only invoked by the pricing command
    ///     path; readers treat rows as configuration snapshots.
    /// </summary>
    public void UpdatePrice(long priceInCents)
    {
        if (priceInCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceInCents), "Interval price cannot be negative.");
        }

        PriceInCents = priceInCents;
        Touch();
    }

    /// <summary>
    ///     Effective price as a <see cref="Money" /> value object in the plan currency.
    /// </summary>
    public Money GetPrice(string currency) => new(PriceInCents / 100m, currency);
}
