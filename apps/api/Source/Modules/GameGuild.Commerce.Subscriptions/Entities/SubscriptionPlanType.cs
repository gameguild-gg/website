namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Commercial classification of a subscription plan (issue #416). Trial offers are not a
///     type: every type can carry <c>TrialPeriodDays</c> configuration.
/// </summary>
public enum SubscriptionPlanType
{
    /// <summary>
    ///     Free tier: monthly and annual prices must be zero. It can still gate features and limits.
    /// </summary>
    Free = 0,

    /// <summary>
    ///     Standard paid tier: concrete non-negative prices. Default for existing plans.
    /// </summary>
    Paid = 1,

    /// <summary>
    ///     Custom-quote tier: <see cref="SubscriptionPlanPricingRules.CustomQuotePriceInCents" />
    ///     (-1) is allowed on the monthly price to mark "contact sales". Such a plan is blocked
    ///     from subscription checkout until a concrete price is configured.
    /// </summary>
    Enterprise = 2
}

/// <summary>
///     Thrown when a plan-type pricing rule is violated (issue #416). Mapped to a 422
///     ProblemDetails by the shared exception middleware.
/// </summary>
public sealed class SubscriptionPlanPricingRuleViolationException : GameGuild.DomainException
{
    public SubscriptionPlanPricingRuleViolationException(string message) : base(message) { }
}

/// <summary>
///     Fail-closed pricing rules shared by every plan write path (issue #416).
/// </summary>
public static class SubscriptionPlanPricingRules
{
    /// <summary>
    ///     The sentinel monthly price marking an enterprise plan as custom-quote ("contact sales").
    ///     It is never a billable amount.
    /// </summary>
    public const long CustomQuotePriceInCents = -1;

    /// <summary>
    ///     Maximum supported plan-level annual discount, in percent (100 = free annual billing).
    /// </summary>
    public const decimal MaximumAnnualDiscountPercent = 100m;

    /// <summary>
    ///     True when the plan is an enterprise plan still waiting for a concrete negotiated price;
    ///     such plans cannot be checked out.
    /// </summary>
    public static bool RequiresCustomQuote(SubscriptionPlanType type, long monthlyPriceInCents) =>
        type == SubscriptionPlanType.Enterprise && monthlyPriceInCents == CustomQuotePriceInCents;

    /// <summary>
    ///     Validates plan-type pricing invariants (issue #416): free plans are priced at zero;
    ///     only enterprise plans may carry the -1 custom-quote sentinel; every other price is
    ///     a non-negative concrete amount.
    /// </summary>
    /// <exception cref="SubscriptionPlanPricingRuleViolationException">Thrown with a checkout-safe message when a rule is violated.</exception>
    public static void Validate(SubscriptionPlanType type, long monthlyPriceInCents, long? annualPriceInCents)
    {
        switch (type)
        {
            case SubscriptionPlanType.Free:
                if (monthlyPriceInCents != 0)
                {
                    throw new SubscriptionPlanPricingRuleViolationException("Free plans must have a monthly price of exactly zero.");
                }
                if (annualPriceInCents is not null and not 0)
                {
                    throw new SubscriptionPlanPricingRuleViolationException("Free plans must have an annual price of exactly zero.");
                }
                break;

            case SubscriptionPlanType.Enterprise:
                if (monthlyPriceInCents < 0 && monthlyPriceInCents != CustomQuotePriceInCents)
                {
                    throw new SubscriptionPlanPricingRuleViolationException("Enterprise plan prices must be non-negative concrete amounts or the -1 custom-quote sentinel.");
                }
                if (annualPriceInCents is < 0)
                {
                    throw new SubscriptionPlanPricingRuleViolationException("Enterprise annual prices must be non-negative concrete amounts.");
                }
                break;

            default:
                if (monthlyPriceInCents < 0)
                {
                    throw new SubscriptionPlanPricingRuleViolationException("Only enterprise plans may use the -1 custom-quote sentinel; paid plans need a concrete non-negative price.");
                }
                if (annualPriceInCents is < 0)
                {
                    throw new SubscriptionPlanPricingRuleViolationException("Annual prices must be non-negative concrete amounts.");
                }
                break;
        }
    }

    /// <summary>
    ///     Validates the plan-level annual discount (issue #416): 0 (no change, default) to 100 percent.
    /// </summary>
    public static void ValidateAnnualDiscountPercent(decimal annualDiscountPercent)
    {
        if (annualDiscountPercent < 0m || annualDiscountPercent > MaximumAnnualDiscountPercent)
        {
            throw new SubscriptionPlanPricingRuleViolationException("The annual discount percentage must be between 0 and 100.");
        }
    }

    /// <summary>
    ///     Derives the annual price (issue #416): an explicit annual price wins; otherwise the
    ///     plan-level annual discount percentage is applied to twelve monthly prices. The default
    ///     discount of 0 keeps the historical <c>monthly x 12</c> behavior unchanged.
    /// </summary>
    public static long DeriveAnnualPriceInCents(long monthlyPriceInCents, long? annualPriceInCents, decimal annualDiscountPercent)
    {
        if (annualPriceInCents.HasValue)
        {
            return annualPriceInCents.Value;
        }
        if (monthlyPriceInCents <= 0)
        {
            return monthlyPriceInCents;
        }
        var undiscounted = monthlyPriceInCents * 12m;
        var discounted = undiscounted * (1m - annualDiscountPercent / 100m);
        // Round to the nearest cent, away from zero on ties, matching sibling pricing rounding.
        return (long) decimal.Round(discounted, 0, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
///     Thrown when a checkout targets an enterprise plan that still carries the -1 custom-quote
///     sentinel (issue #416). Mapped to a 422 ProblemDetails by the shared exception middleware.
/// </summary>
public sealed class SubscriptionPlanCustomQuotePendingException : GameGuild.DomainException
{
    public Guid PlanId { get; }

    public SubscriptionPlanCustomQuotePendingException(Guid planId)
        : base($"Subscription plan {planId} is an enterprise custom-quote plan: checkout is blocked until a concrete price is configured.")
    {
        PlanId = planId;
    }
}
