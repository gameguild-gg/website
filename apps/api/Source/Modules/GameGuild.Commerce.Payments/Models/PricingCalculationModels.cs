namespace GameGuild.Commerce.Payments;

/// <summary>
///     Discount type enumeration for applied discounts in the unified pricing engine.
///     Consolidated (issue #417): the Payments and Subscriptions module duplicates are
///     merged into this single definition covering every discount type the promo engine
///     can apply plus wallet credits.
/// </summary>
public enum DiscountType
{
    /// <summary>Percentage discount (e.g., 20% off)</summary>
    Percentage = 0,

    /// <summary>Fixed amount discount (e.g., $20 off)</summary>
    FixedAmount = 1,

    /// <summary>Wallet/account credit applied against the total</summary>
    Credits = 2,

    /// <summary>Free billing periods granted (charge waived)</summary>
    FreeMonths = 3,

    /// <summary>Trial extension days granted (charge waived)</summary>
    TrialExtension = 4,

    /// <summary>Set-price override (final price pinned by the code)</summary>
    SetPrice = 5
}

/// <summary>
///     Applied discount information produced by the unified pricing engine (issue #417).
///     Concrete (the abstract Payments predecessor and the Subscriptions duplicate are gone).
/// </summary>
public class AppliedDiscount
{
    /// <summary>Normalized discount code</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Amount actually discounted by this code</summary>
    public Money Amount { get; init; } = Money.Zero();

    /// <summary>Percentage equivalent, for percentage-based codes</summary>
    public decimal Percentage { get; init; }

    /// <summary>Discount type</summary>
    public DiscountType Type { get; init; }

    /// <summary>Stacking priority (higher applies first; promo-engine StackingPriority)</summary>
    public int Priority { get; init; }

    /// <summary>Whether this discount may stack with others (promo-engine !IsExclusive)</summary>
    public bool IsStackable { get; init; }
}

/// <summary>
///     One usage-based charge input with optional progressive tiers (issue #417).
///     Without tiers the charge is <paramref name="Quantity"/> × <paramref name="PerUnitPrice"/>;
///     with tiers each quantity band is priced at its own per-unit price (utility-style
///     progressive banding), the last open-ended band absorbing any remainder.
/// </summary>
/// <param name="Metric">Metric name (display/line-item key)</param>
/// <param name="Quantity">Quantity consumed in the period</param>
/// <param name="PerUnitPrice">Flat per-unit price used when no tiers are supplied</param>
/// <param name="Tiers">Optional progressive tier bands</param>
public sealed record PricingUsageChargeInput(
    string Metric,
    decimal Quantity,
    decimal PerUnitPrice = 0,
    IReadOnlyList<PricingUsageTier>? Tiers = null);

/// <summary>
///     One progressive usage-pricing band (issue #417). Bands are half-open quantity
///     intervals [<paramref name="MinQuantity"/>, <paramref name="MaxQuantity"/>); quantities
///     beyond the highest band's maximum are priced at that band's per-unit price.
/// </summary>
/// <param name="MinQuantity">Inclusive lower bound of the band</param>
/// <param name="MaxQuantity">Exclusive upper bound of the band (null = open-ended)</param>
/// <param name="PerUnitPrice">Per-unit price inside this band</param>
public sealed record PricingUsageTier(decimal MinQuantity, decimal? MaxQuantity, decimal PerUnitPrice);

/// <summary>
///     Add-on priced independently of the plan (issue #417): own unit price, own quantity,
///     own currency (converted to the calculation currency through the FX core when they differ).
/// </summary>
/// <param name="AddOnId">Caller-side add-on identifier (line-item key)</param>
/// <param name="Name">Display name</param>
/// <param name="Quantity">Quantity purchased</param>
/// <param name="UnitPrice">Unit price</param>
public sealed record PricingAddOnInput(string AddOnId, string Name, int Quantity, Money UnitPrice);

/// <summary>Which items promo discounts apply to (issue #417 line-item scoping).</summary>
public enum PricingDiscountScope
{
    /// <summary>Discounts apply to the full subtotal (plan + add-ons + usage).</summary>
    AllItems = 0,

    /// <summary>Discounts apply to the base plan price only.</summary>
    PlanOnly = 1
}

/// <summary>
///     Tax inputs for a pricing calculation (issue #417). Carried through to the existing
///     <see cref="ITaxCalculationService"/> — jurisdiction, customer type (B2B reverse charge),
///     VAT number, tax-inclusive flag, product category, and exemptions.
/// </summary>
public sealed record PricingTaxInfo(
    string JurisdictionCode,
    CustomerType CustomerType = CustomerType.B2C,
    string? CustomerVatNumber = null,
    string? ProductCategory = null,
    bool IsTaxInclusive = false,
    IReadOnlyList<string>? ApplicableExemptions = null);

/// <summary>
///     Proration inputs for a mid-cycle plan change (issue #417). The unified engine wires
///     these into the existing <c>BillingCalculator.CalculateProration</c> through
///     <see cref="IPlanProrationCalculator"/>.
/// </summary>
/// <param name="CurrentPlanId">Plan the subscription is on today</param>
/// <param name="NewPlanId">Plan being switched to</param>
/// <param name="PeriodStart">Current billing period start</param>
/// <param name="PeriodEnd">Current billing period end</param>
/// <param name="EffectiveDate">When the change takes effect (mid-cycle)</param>
public sealed record PricingProrationInput(
    Guid CurrentPlanId,
    Guid NewPlanId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime EffectiveDate);

/// <summary>
///     Proration outcome attached to a calculation (issue #417): mirrors
///     <c>PlanChangeProration</c> plus the period context needed for line items.
/// </summary>
public sealed record PricingProrationResult(
    decimal CreditForUnused,
    decimal ChargeForNew,
    decimal NetAdjustment,
    DateTime EffectiveDate,
    int TotalDays,
    int DaysRemaining)
{
    /// <summary>Whether the change nets to a credit for the customer.</summary>
    public bool IsCredit => NetAdjustment < 0;

    /// <summary>Whether the change nets to an immediate charge.</summary>
    public bool IsCharge => NetAdjustment > 0;
}

/// <summary>Credit (wallet) application outcome inside a calculation (issue #417).</summary>
public sealed record CreditApplicationResult(
    decimal CreditsAvailable,
    decimal CreditsApplied,
    decimal RemainingCredits);

/// <summary>One display/audit line of the calculation (issue #417).</summary>
public sealed record PricingLineItem(
    string Type,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    Money Amount);

/// <summary>
///     Unified pricing calculation request (issue #417). Exactly one of
///     <paramref name="PlanId"/>/<paramref name="BaseAmount"/> must be supplied.
/// </summary>
/// <param name="PlanId">Subscription plan to price (resolved via IPlanPricingResolver)</param>
/// <param name="BaseAmount">Custom base amount (plan-less calculation)</param>
/// <param name="TenantId">Tenant correlation (audit)</param>
/// <param name="UserId">Acting user (promo eligibility, wallet credits)</param>
/// <param name="BillingCycle">Billing interval for the plan price</param>
/// <param name="Currency">Target currency (ISO 4217); plan/add-on amounts are converted via the FX core</param>
/// <param name="EffectiveDate">Effective date of the calculation (defaults to now)</param>
/// <param name="PromoCodes">Promo codes to apply (validated + stacked by the promo engine)</param>
/// <param name="DiscountScope">Line-item scope discounts apply to</param>
/// <param name="AddOns">Independently priced add-ons</param>
/// <param name="UsageCharges">Usage-based charges with optional progressive tiers</param>
/// <param name="Proration">Optional mid-cycle plan-change proration</param>
/// <param name="TaxInfo">Tax inputs (required when <paramref name="IncludeTax"/> is true)</param>
/// <param name="IncludeTax">Whether to run tax through ITaxCalculationService</param>
/// <param name="ApplyCredits">Whether to apply wallet credits after tax</param>
/// <param name="MaxCreditUsage">Upper bound on credits to apply</param>
/// <param name="ConsumeCredits">When true, deducted from the wallet (quote-only by default)</param>
/// <param name="RecordDecision">When true, appended to the pricing-decision ledger (issue #400)</param>
/// <param name="SubscriptionId">Subscription correlation (ledger)</param>
/// <param name="OrderId">Order correlation (ledger)</param>
/// <param name="PaymentId">Payment correlation (ledger)</param>
public sealed record PricingCalculationRequest(
    Guid? PlanId = null,
    decimal? BaseAmount = null,
    Guid? TenantId = null,
    Guid? UserId = null,
    BillingCycle BillingCycle = BillingCycle.Monthly,
    string Currency = "USD",
    DateTime? EffectiveDate = null,
    IReadOnlyList<string>? PromoCodes = null,
    PricingDiscountScope DiscountScope = PricingDiscountScope.AllItems,
    IReadOnlyList<PricingAddOnInput>? AddOns = null,
    IReadOnlyList<PricingUsageChargeInput>? UsageCharges = null,
    PricingProrationInput? Proration = null,
    PricingTaxInfo? TaxInfo = null,
    bool IncludeTax = true,
    bool ApplyCredits = false,
    decimal? MaxCreditUsage = null,
    bool ConsumeCredits = false,
    bool RecordDecision = true,
    Guid? SubscriptionId = null,
    Guid? OrderId = null,
    Guid? PaymentId = null);

/// <summary>
///     The single pricing calculation result (issue #417). Consolidates the former abstract
///     Payments shape and the Subscriptions duplicate into one engine-owned model.
/// </summary>
public sealed record PricingCalculationResult
{
    /// <summary>Unique calculation id (also the ledger idempotency key).</summary>
    public Guid CalculationId { get; init; } = Guid.NewGuid();

    /// <summary>When the calculation ran (UTC).</summary>
    public DateTime CalculatedAt { get; init; } = SystemClock.UtcNow;

    /// <summary>Calculation currency (all Money fields).</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Base plan (or custom) price for the billing interval.</summary>
    public Money BasePlanPrice { get; init; } = Money.Zero();

    /// <summary>Total of independently priced add-ons.</summary>
    public Money AddOnsTotal { get; init; } = Money.Zero();

    /// <summary>Total of usage-based (tiered) charges.</summary>
    public Money UsageCharges { get; init; } = Money.Zero();

    /// <summary>Base + add-ons + usage.</summary>
    public Money Subtotal { get; init; } = Money.Zero();

    /// <summary>Discounts the promo engine applied, in application order.</summary>
    public List<AppliedDiscount> AppliedDiscounts { get; init; } = [];

    /// <summary>Requested promo codes that did not apply, with the fail-closed reason.</summary>
    public List<RejectedPromoDiscount> RejectedPromoCodes { get; init; } = [];

    /// <summary>Sum of applied discount amounts.</summary>
    public Money TotalDiscounts { get; init; } = Money.Zero();

    /// <summary>Subtotal − discounts.</summary>
    public Money SubtotalAfterDiscounts { get; init; } = Money.Zero();

    /// <summary>Proration outcome, when the request carried proration inputs.</summary>
    public PricingProrationResult? Proration { get; init; }

    /// <summary>
    ///     Net proration adjustment in the calculation currency, signed: positive = immediate
    ///     charge, negative = credit (<see cref="Money"/> cannot hold negative amounts).
    /// </summary>
    public decimal ProrationAdjustment { get; init; }

    /// <summary>Full tax result from ITaxCalculationService (breakdown by jurisdiction).</summary>
    public TaxCalculationResult? Tax { get; init; }

    /// <summary>Tax amount.</summary>
    public Money TaxAmount { get; init; } = Money.Zero();

    /// <summary>Wallet credit application outcome, when credits were applied.</summary>
    public CreditApplicationResult? Credits { get; init; }

    /// <summary>Credits applied against the total.</summary>
    public Money CreditsApplied { get; init; } = Money.Zero();

    /// <summary>Taxable total (post-discount + proration + tax).</summary>
    public Money Total { get; init; } = Money.Zero();

    /// <summary>Total − credits: what the customer owes.</summary>
    public Money AmountDue { get; init; } = Money.Zero();

    /// <summary>Itemized breakdown for display and audit.</summary>
    public List<PricingLineItem> LineItems { get; init; } = [];

    /// <summary>Human-readable step-by-step explanation.</summary>
    public string CalculationExplanation { get; init; } = string.Empty;

    /// <summary>Ledger entry id when the decision was recorded (issue #400 ledger).</summary>
    public Guid? LedgerEntryId { get; init; }

    /// <summary>Sticky validation notes (invariant violations never silently pass).</summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    /// <summary>Billing cycle used for the plan price.</summary>
    public BillingCycle BillingCycle { get; init; } = BillingCycle.Monthly;
}

/// <summary>
///     Price-change preview request (issue #417): everything needed to show the customer what
///     a plan switch costs before they confirm. The preview never records a ledger decision
///     and never consumes credits.
/// </summary>
/// <param name="CurrentPlanId">Plan the subscription is on today</param>
/// <param name="NewPlanId">Plan being considered</param>
/// <param name="PeriodStart">Current billing period start</param>
/// <param name="PeriodEnd">Current billing period end</param>
/// <param name="EffectiveDate">When the change would take effect (defaults to now)</param>
/// <param name="BillingCycle">Billing interval for the new plan price</param>
/// <param name="Currency">Target currency</param>
/// <param name="UserId">Acting user (promo eligibility)</param>
/// <param name="PromoCodes">Promo codes to preview</param>
/// <param name="TaxInfo">Tax inputs</param>
/// <param name="IncludeTax">Whether to include tax</param>
public sealed record PricingPreviewRequest(
    Guid CurrentPlanId,
    Guid NewPlanId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime? EffectiveDate = null,
    BillingCycle BillingCycle = BillingCycle.Monthly,
    string Currency = "USD",
    Guid? UserId = null,
    IReadOnlyList<string>? PromoCodes = null,
    PricingTaxInfo? TaxInfo = null,
    bool IncludeTax = true);

/// <summary>
///     Price-change preview outcome (issue #417): the immediate (prorated) charge plus the
///     next-renewal amount, so the customer sees both before confirming.
/// </summary>
public sealed record PricingPreviewResult
{
    /// <summary>Effective date the change would take place.</summary>
    public DateTime EffectiveDate { get; init; } = SystemClock.UtcNow;

    /// <summary>The prorated charge/credit due immediately (mid-cycle change).</summary>
    public PricingCalculationResult ImmediateCharge { get; init; } = new();

    /// <summary>Full-cycle price of the new plan at the next renewal.</summary>
    public PricingCalculationResult NextRenewal { get; init; } = new();

    /// <summary>Savings from discounts on the next renewal (0 when no codes applied).</summary>
    public Money SavingsFromDiscounts { get; init; } = Money.Zero();
}
