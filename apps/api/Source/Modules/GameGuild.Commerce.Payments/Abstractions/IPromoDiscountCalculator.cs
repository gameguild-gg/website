namespace GameGuild.Commerce.Payments;

/// <summary>Discount request for <see cref="IPromoDiscountCalculator"/> (issue #417).</summary>
/// <param name="OrderAmount">Amount the codes are applied to</param>
/// <param name="PromoCodes">Requested promo codes</param>
/// <param name="PlanId">Plan correlation for plan-scoped eligibility</param>
/// <param name="UserId">User for per-user usage limits</param>
public sealed record PromoDiscountRequest(
    decimal OrderAmount,
    IReadOnlyList<string> PromoCodes,
    Guid? PlanId = null,
    Guid? UserId = null);

/// <summary>
///     Promo resolution outcome (issue #417): the discounts the promo engine actually applied,
///     in application order, plus rejections with reasons and the governing stacking rule.
/// </summary>
public sealed record PromoDiscountResult(
    decimal TotalDiscount,
    IReadOnlyList<AppliedPromoDiscount> Applied,
    IReadOnlyList<RejectedPromoDiscount> Rejected,
    string? StackingRuleName);

/// <summary>One applied promo discount, already mapped to engine discount semantics.</summary>
/// <param name="Code">Normalized code</param>
/// <param name="Description">Human-readable description</param>
/// <param name="Amount">Amount discounted</param>
/// <param name="Percentage">Percentage equivalent when percentage-based</param>
/// <param name="Type">Engine discount type</param>
/// <param name="Priority">Stacking priority (higher first)</param>
/// <param name="IsStackable">Whether the code may stack</param>
/// <param name="Sequence">Application order (1-based)</param>
public sealed record AppliedPromoDiscount(
    string Code,
    string Description,
    decimal Amount,
    decimal? Percentage,
    DiscountType Type,
    int Priority,
    bool IsStackable,
    int Sequence);

/// <summary>One rejected promo code with the fail-closed reason.</summary>
/// <param name="Code">Requested code</param>
/// <param name="Reason">Why it did not apply</param>
public sealed record RejectedPromoDiscount(string Code, string Reason);

/// <summary>
///     Promo-discount seam owned by the Payments module (issue #417): the Products module
///     registers the implementation on top of its promo engine
///     (<c>IPricingEngineService.ApplyPromoCodesAsync</c> — validation, eligibility,
///     stacking rules, caps), so the unified pricing engine applies real promo codes with the
///     landed #795 semantics instead of parsing code strings.
/// </summary>
public interface IPromoDiscountCalculator
{
    /// <summary>Validates and stacks promo codes against an amount via the Products promo engine.</summary>
    /// <param name="request">Discount request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<PromoDiscountResult> ApplyAsync(PromoDiscountRequest request, CancellationToken cancellationToken = default);
}
