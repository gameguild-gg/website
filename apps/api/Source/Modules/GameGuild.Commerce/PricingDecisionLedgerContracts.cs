using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.Commerce;

/// <summary>Well-known pricing decision types recorded in the ledger (stored as strings).</summary>
public static class PricingDecisionType
{
    /// <summary>Checkout: pricing decision for one order line item added to an order.</summary>
    public const string CheckoutLineItem = "CheckoutLineItem";

    /// <summary>Subscription renewal: quote for the next billing cycle.</summary>
    public const string RenewalQuote = "RenewalQuote";

    /// <summary>Order-level promo application that produced a final payable amount.</summary>
    public const string OrderPromoApply = "OrderPromoApply";

    /// <summary>Unified pricing-engine quote (issue #417): one full plan-level calculation.</summary>
    public const string CalculationQuote = "CalculationQuote";

    /// <summary>All recognized decision types.</summary>
    public static readonly IReadOnlyList<string> All = [CheckoutLineItem, RenewalQuote, OrderPromoApply, CalculationQuote];
}

/// <summary>Drift-validation outcomes stamped on ledger entries.</summary>
public static class PricingDecisionDriftStatus
{
    /// <summary>Entry has not been through drift validation yet.</summary>
    public const string NotValidated = "NotValidated";

    /// <summary>Recomputed final matches the stored final within tolerance.</summary>
    public const string Verified = "Verified";

    /// <summary>Recomputed final diverges from the stored final — flagged for review.</summary>
    public const string Drifted = "Drifted";

    /// <summary>All recognized drift statuses.</summary>
    public static readonly IReadOnlyList<string> All = [NotValidated, Verified, Drifted];
}

/// <summary>Engine version stamp recorded on every ledger entry.</summary>
public static class PricingDecisionLedgerDefaults
{
    /// <summary>Version of the pricing pipeline that produces ledger entries (issue #400 initial revision).</summary>
    public const string EngineVersion = "pricing-v1";
}

/// <summary>One pricing-rule adjustment inside a decision breakdown.</summary>
/// <param name="RuleId">Identifier of the applied rule.</param>
/// <param name="RuleName">Name of the applied rule.</param>
/// <param name="RuleType">Rule type (e.g. VolumeDiscount).</param>
/// <param name="UnitDiscount">Per-unit reduction produced by the rule.</param>
/// <param name="TotalAdjustment">Reduction for the whole quantity.</param>
public sealed record PricingRuleAdjustment(
    Guid RuleId,
    string RuleName,
    string? RuleType = null,
    decimal UnitDiscount = 0,
    decimal TotalAdjustment = 0);

/// <summary>One promo-code application inside a decision breakdown.</summary>
/// <param name="Code">Normalized promo code.</param>
/// <param name="PromoCodeId">Identifier of the promo code, when known.</param>
/// <param name="Type">Discount type (e.g. PercentageOff).</param>
/// <param name="Percentage">Discount percentage, for percentage-based codes.</param>
/// <param name="Amount">Discount amount actually applied by this code.</param>
/// <param name="Sequence">Order in which the code applied during stacking.</param>
public sealed record PromoApplicationBreakdown(
    string Code,
    Guid? PromoCodeId = null,
    string? Type = null,
    decimal? Percentage = null,
    decimal Amount = 0,
    int Sequence = 1);

/// <summary>One cap applied during discount resolution (stacking caps, line floors).</summary>
/// <param name="Kind">Cap kind (e.g. MaxTotalDiscountPercentage).</param>
/// <param name="Value">Cap value as configured.</param>
/// <param name="AmountBeforeCap">Discount total when the cap was evaluated.</param>
/// <param name="AmountAfterCap">Discount total after the cap applied.</param>
public sealed record CapApplication(
    string Kind,
    decimal? Value = null,
    decimal AmountBeforeCap = 0,
    decimal AmountAfterCap = 0);

/// <summary>
/// Full breakdown of one pricing decision, serialized into
/// <see cref="PricingDecisionLedgerEntry.BreakdownJson"/>. The drift validator
/// recomputes the final amount from these parts (see <see cref="PricingDecisionBreakdown.RecomputeFinalAmount"/>).
/// </summary>
/// <param name="Version">Breakdown schema version: 1 = checkout/renewal decisions (issue #400); 2 adds the unified-engine plan-level fields (issue #417).</param>
/// <param name="Currency">Currency of all amounts.</param>
/// <param name="Quantity">Quantity priced.</param>
/// <param name="BaseUnitPrice">List or sale unit price used as the baseline.</param>
/// <param name="SaleUnitPrice">Sale unit price, when a sale was active.</param>
/// <param name="SaleActive">Whether the sale price was used as the baseline.</param>
/// <param name="BaseAmount">Baseline total. Version 1: effective unit price × quantity. Version 2: base plan + add-ons + usage (the discountable subtotal).</param>
/// <param name="RuleAdjustments">Pricing-rule adjustments applied after the base.</param>
/// <param name="RuleAdjustmentTotal">Sum of rule adjustment totals.</param>
/// <param name="PromoApplications">Promo code applications applied after the rules.</param>
/// <param name="PromoDiscountTotal">Sum of promo application amounts.</param>
/// <param name="Caps">Caps evaluated during resolution (with before/after amounts).</param>
/// <param name="FinalAmount">Final committed price (Version 2: the amount due after proration, tax, and credits).</param>
/// <param name="AddOnsAmount">Version 2: add-ons total (already included in BaseAmount).</param>
/// <param name="UsageAmount">Version 2: usage charges total (already included in BaseAmount).</param>
/// <param name="ProrationAdjustment">Version 2: signed net proration adjustment (negative = credit).</param>
/// <param name="TaxAmount">Version 2: tax applied after discounts and proration.</param>
/// <param name="CreditsApplied">Version 2: wallet credits applied against the total.</param>
public sealed record PricingDecisionBreakdown(
    int Version = 1,
    string Currency = "USD",
    int Quantity = 1,
    decimal BaseUnitPrice = 0,
    decimal? SaleUnitPrice = null,
    bool SaleActive = false,
    decimal BaseAmount = 0,
    List<PricingRuleAdjustment> RuleAdjustments = null!,
    decimal RuleAdjustmentTotal = 0,
    List<PromoApplicationBreakdown> PromoApplications = null!,
    decimal PromoDiscountTotal = 0,
    List<CapApplication> Caps = null!,
    decimal FinalAmount = 0,
    decimal AddOnsAmount = 0,
    decimal UsageAmount = 0,
    decimal ProrationAdjustment = 0,
    decimal TaxAmount = 0,
    decimal CreditsApplied = 0)
{
    /// <summary>JSON serializer settings used for the breakdown document (round-trip stable).</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>Creates a breakdown with empty adjustment lists.</summary>
    public static PricingDecisionBreakdown Empty(string currency, int quantity = 1) => new(
        Version: 1,
        Currency: currency,
        Quantity: Math.Max(1, quantity),
        RuleAdjustments: [],
        PromoApplications: [],
        Caps: []);

    /// <summary>Serializes the breakdown to its durable JSON form.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>Deserializes a breakdown document; returns null when the payload is not a valid breakdown.</summary>
    public static PricingDecisionBreakdown? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PricingDecisionBreakdown>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Recomputes the final amount from the breakdown parts, mirroring the pricing
    /// engine's arithmetic: sequential subtraction (base → rules → promos → caps) with
    /// two-decimal rounding at each stage and a zero floor. Used by drift validation to
    /// flag entries whose stored <c>FinalAmount</c> no longer follows from their parts.
    /// Version 2 documents (issue #417) additionally fold in the signed proration
    /// adjustment, tax, and wallet credits; Version 1 entries keep their original
    /// arithmetic untouched.
    /// </summary>
    public decimal RecomputeFinalAmount()
    {
        var ruleTotal = Math.Round(RuleAdjustmentTotal, 2, MidpointRounding.AwayFromZero);
        var promoTotal = Math.Round(PromoDiscountTotal, 2, MidpointRounding.AwayFromZero);

        var afterRules = Round(BaseAmount - ruleTotal);
        var afterPromos = Round(afterRules - promoTotal);

        // Caps never add discount; they clamp the discounted amount back up toward the
        // pre-cap level when a cap truncated the discount (AmountAfterCap < AmountBeforeCap).
        var capReduction = Caps?.Sum(cap => Math.Max(0, cap.AmountAfterCap - cap.AmountBeforeCap)) ?? 0;
        capReduction = Math.Round(capReduction, 2, MidpointRounding.AwayFromZero);

        var afterCaps = Round(afterPromos + capReduction);

        if (Version >= 2)
        {
            // Version 2 (unified pricing engine, issue #417): proration (signed) + tax − credits.
            var proration = Math.Round(ProrationAdjustment, 2, MidpointRounding.AwayFromZero);
            var tax = Math.Round(TaxAmount, 2, MidpointRounding.AwayFromZero);
            var credits = Math.Round(CreditsApplied, 2, MidpointRounding.AwayFromZero);

            return Round(afterCaps + proration + tax - credits);
        }

        return afterCaps;

        static decimal Round(decimal value) =>
            value <= 0 ? 0 : Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Outcome of a ledger write.</summary>
public enum PricingDecisionRecordingStatus
{
    /// <summary>A new ledger entry was appended.</summary>
    Recorded,

    /// <summary>The idempotency key already existed; the original entry is returned.</summary>
    Replay,

    /// <summary>The decision was rejected before persistence (validation failure).</summary>
    Rejected
}

/// <summary>Input to <see cref="IPricingDecisionLedgerWriter.RecordAsync"/>.</summary>
/// <param name="DecisionType">Decision type (see <see cref="PricingDecisionType"/>).</param>
/// <param name="TenantId">Tenant scope (null = platform-level).</param>
/// <param name="ActorUserId">User the price was computed for (null = system actor).</param>
/// <param name="ActorIpAddress">Client IP, when the flow captured one.</param>
/// <param name="Order">Order correlation.</param>
/// <param name="OrderLineItem">Order line item correlation.</param>
/// <param name="PaymentId">Payment correlation.</param>
/// <param name="Subscription">Subscription correlation.</param>
/// <param name="BillingCycleNumber">Billing cycle for renewal quotes.</param>
/// <param name="Product">Product correlation.</param>
/// <param name="Breakdown">Full calculation breakdown.</param>
/// <param name="AppliedPricingRuleId">Winning pricing rule, when one applied.</param>
/// <param name="AppliedStackingRuleName">Stacking rule name, when multi-code resolution used one.</param>
/// <param name="IdempotencyKey">Idempotency key of the calculation (required).</param>
public sealed record PricingDecisionRecord(
    string DecisionType,
    Guid? TenantId,
    Guid? ActorUserId,
    string? ActorIpAddress,
    Guid? Order,
    Guid? OrderLineItem,
    Guid? PaymentId,
    Guid? Subscription,
    int? BillingCycleNumber,
    Guid? Product,
    PricingDecisionBreakdown Breakdown,
    Guid? AppliedPricingRuleId = null,
    string? AppliedStackingRuleName = null,
    string? IdempotencyKey = null);

/// <summary>
/// Append-only writer for the pricing-decision ledger (issue #400). Every checkout or
/// renewal calculation that produces a final price must record exactly one entry; the
/// write is idempotent on the supplied key so command retries and webhook redeliveries
/// never duplicate a decision.
/// </summary>
public interface IPricingDecisionLedgerWriter
{
    /// <summary>
    /// Appends one ledger entry. Validates the decision (type, currency, positive
    /// quantities, key present), persists it transactionally and publishes the matching
    /// Compliance.Audit event. A replayed key returns the original entry untouched.
    /// </summary>
    /// <param name="decision">The decision to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Status plus the persisted (or original, on replay) entry; entry is null only for <see cref="PricingDecisionRecordingStatus.Rejected"/>.</returns>
    Task<(PricingDecisionRecordingStatus Status, PricingDecisionLedgerEntry? Entry)> RecordAsync(
        PricingDecisionRecord decision,
        CancellationToken cancellationToken = default);
}
