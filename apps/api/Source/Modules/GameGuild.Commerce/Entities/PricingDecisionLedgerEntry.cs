using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce;

/// <summary>
/// Durable record of one pricing decision that produced a final price at checkout or
/// renewal (issue #400). Every calculation that commits an economic price — an order
/// line item with discounts, a subscription renewal quote, an order-level promo
/// application — appends one immutable row here with the full calculation breakdown so
/// the decision can be audited, analyzed for discount effectiveness and re-verified for
/// drift after the fact. Rows are append-only: the only permitted post-insert mutation
/// is the drift-validation outcome stamped by the verification command.
/// </summary>
[Table("pricing_decision_ledger")]
public class PricingDecisionLedgerEntry : EntityBase
{
    /// <summary>Default constructor for EF Core.</summary>
    public PricingDecisionLedgerEntry() { }

    /// <summary>Kind of calculation that produced the decision (see <see cref="PricingDecisionType"/>).</summary>
    [Required]
    [MaxLength(32)]
    public string DecisionType { get; set; } = PricingDecisionType.CheckoutLineItem;

    /// <summary>Order the decision was made for, when the flow had one.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Order line item the decision priced, when the flow had one.</summary>
    public Guid? OrderLineItemId { get; set; }

    /// <summary>Payment correlated with the decision, when known at calculation time.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Subscription the decision was made for (renewal quotes).</summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>Billing cycle the renewal quote was produced for.</summary>
    public int? BillingCycleNumber { get; set; }

    /// <summary>Product the decision priced, when the flow had one.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Quantity the decision priced.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Currency of every monetary field in the row (ISO 4217).</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Baseline amount before any adjustment (unit price × quantity, list or sale).</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal BaseAmount { get; set; }

    /// <summary>Total reduction produced by pricing rules (already included in <see cref="BaseAmount"/>? no: subtracted after the base).</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal RuleAdjustmentTotal { get; set; }

    /// <summary>Total discount produced by promo codes.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal PromoDiscountTotal { get; set; }

    /// <summary>Final authoritative price the flow committed to.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal FinalAmount { get; set; }

    /// <summary>
    /// Full calculation breakdown serialized as JSON: base, per-rule adjustments,
    /// per-code promo applications, caps and the final amount. The drift validator
    /// recomputes <see cref="FinalAmount"/> from this document.
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string BreakdownJson { get; set; } = "{}";

    /// <summary>Comma-separated normalized applied promo codes (index-friendly filter column).</summary>
    [MaxLength(512)]
    public string? AppliedPromoCodes { get; set; }

    /// <summary>Pricing rule that won resolution, when one applied.</summary>
    public Guid? AppliedPricingRuleId { get; set; }

    /// <summary>Name of the promo stacking rule that governed multi-code resolution, when recorded.</summary>
    [MaxLength(200)]
    public string? AppliedStackingRuleName { get; set; }

    /// <summary>Pricing engine version that produced the decision (semver-ish stamp).</summary>
    [Required]
    [MaxLength(20)]
    public string PricingEngineVersion { get; set; } = PricingDecisionLedgerDefaults.EngineVersion;

    /// <summary>User on whose behalf the price was computed (null = system/batch actor).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Client IP captured with the decision, when the flow had one.</summary>
    [MaxLength(45)]
    public string? ActorIpAddress { get; set; }

    /// <summary>
    /// Idempotency key of the calculation. A replayed flow (command retry, webhook
    /// redelivery) returns the original row instead of appending a duplicate; the
    /// unique index enforces this at the database level.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>When the decision was made (mirrors CreatedAt; explicit for query clarity).</summary>
    public DateTime DecidedAtUtc { get; set; } = SystemClock.UtcNow;

    /// <summary>Outcome of drift validation (see <see cref="PricingDecisionDriftStatus"/>).</summary>
    [Required]
    [MaxLength(16)]
    public string DriftStatus { get; private set; } = PricingDecisionDriftStatus.NotValidated;

    /// <summary>Final amount recomputed from <see cref="BreakdownJson"/> by the drift validator.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? RecomputedFinalAmount { get; private set; }

    /// <summary>When drift validation last stamped this entry.</summary>
    public DateTime? ValidatedAtUtc { get; private set; }

    /// <summary>Why the entry drifted, when it did (short human-readable reason).</summary>
    [MaxLength(500)]
    public string? DriftReason { get; private set; }

    /// <summary>Immutable snapshot of the drift status for read paths.</summary>
    [NotMapped]
    public string DriftStatusValue => DriftStatus;

    /// <summary>
    /// Stamps the drift-validation outcome. Append-only ledger invariant: this is the
    /// single permitted post-insert mutation, reserved for the verification command.
    /// </summary>
    public void StampValidation(string driftStatus, decimal recomputedFinalAmount, string? reason)
    {
        if (string.IsNullOrWhiteSpace(driftStatus))
        {
            throw new ArgumentException("Drift status is required.", nameof(driftStatus));
        }

        DriftStatus = driftStatus;
        RecomputedFinalAmount = recomputedFinalAmount;
        ValidatedAtUtc = SystemClock.UtcNow;
        DriftReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Touch();
    }
}
