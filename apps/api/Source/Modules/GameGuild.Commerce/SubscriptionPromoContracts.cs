namespace GameGuild.Commerce;

/// <summary>
///     One promo code that applied to a subscription quote (issue #418). Self-contained in
///     the base Commerce module so the Subscriptions and Billing projects can consume promo
///     outcomes without referencing the Products project graph.
/// </summary>
/// <param name="PromoCodeId">Promo code identifier.</param>
/// <param name="Code">Promo code string.</param>
/// <param name="DiscountAmount">Discount the code contributes to the current charge.</param>
/// <param name="DiscountType">Promo code type name (e.g. <c>PercentageOff</c>).</param>
/// <param name="TrialExtensionDays">Trial days granted (FreeTrialExtension codes only).</param>
/// <param name="FreeMonthsGranted">Billing periods granted (FreeMonths codes only).</param>
public sealed record SubscriptionPromoCodeApplication(
    Guid PromoCodeId,
    string Code,
    decimal DiscountAmount,
    string? DiscountType = null,
    int? TrialExtensionDays = null,
    int? FreeMonthsGranted = null);

/// <summary>
///     A promo code that was rejected during a subscription quote, with the reason.
/// </summary>
public sealed record SubscriptionPromoRejectedCode(string Code, string Reason);

/// <summary>
///     Request to quote promo codes against a subscription being created (issue #418).
///     Quoting is read-only: no usage rows are written until the subscription exists and
///     <see cref="ISubscriptionPromoCoordinator.RecordSubscriptionUsageAsync"/> is called.
/// </summary>
/// <param name="PlanId">Plan the subscription is created on; drives plan-scoped eligibility.</param>
/// <param name="Amount">Charge amount the codes are quoted against.</param>
/// <param name="PromoCodes">Promo code strings submitted at checkout.</param>
/// <param name="UserId">Creating user, for per-user limit checks.</param>
/// <param name="ProductId">Optional product context for product-scoped codes.</param>
/// <param name="OrderId">Optional fulfilled-order linkage.</param>
public sealed record SubscriptionPromoQuoteRequest(
    Guid PlanId,
    decimal Amount,
    IReadOnlyList<string> PromoCodes,
    Guid? UserId = null,
    Guid? ProductId = null,
    Guid? OrderId = null);

/// <summary>
///     Read-only quote of promo codes for a subscription creation (issue #418).
/// </summary>
/// <param name="OriginalAmount">Amount before promo codes.</param>
/// <param name="FinalAmount">Amount after applied discounts.</param>
/// <param name="TotalDiscount">Sum of applied discounts.</param>
/// <param name="AppliedCodes">Codes that applied, with their grant details.</param>
/// <param name="RejectedCodes">Codes that were rejected, with reasons.</param>
/// <param name="Currency">Currency of the quoted amounts.</param>
/// <param name="StackingRuleName">Stacking rule that governed resolution (null = single-code default).</param>
public sealed record SubscriptionPromoQuoteResult(
    decimal OriginalAmount,
    decimal FinalAmount,
    decimal TotalDiscount,
    IReadOnlyList<SubscriptionPromoCodeApplication> AppliedCodes,
    IReadOnlyList<SubscriptionPromoRejectedCode> RejectedCodes,
    string Currency = "USD",
    string? StackingRuleName = null);

/// <summary>
///     Request to persist the usage rows for promo codes applied to a created subscription
///     (issue #418). Recording is atomic per code, idempotent on the supplied key, and
///     fail-closed on usage limits, mirroring the order application path.
/// </summary>
/// <param name="SubscriptionId">The created subscription the codes were applied to.</param>
/// <param name="UserId">Creating user.</param>
/// <param name="AppliedCodes">Codes that applied at quote time.</param>
/// <param name="Currency">Currency of the applied discounts.</param>
/// <param name="OrderId">Optional fulfilled-order linkage.</param>
/// <param name="IdempotencyKey">Replay-protection key; repeated submissions never count twice.</param>
public sealed record SubscriptionPromoUsageRecordRequest(
    Guid SubscriptionId,
    Guid UserId,
    IReadOnlyList<SubscriptionPromoCodeApplication> AppliedCodes,
    string Currency = "USD",
    Guid? OrderId = null,
    string? IdempotencyKey = null);

/// <summary>
///     The not-yet-invoiced promo context of a subscription: usage rows linked to the
///     subscription that no invoice has consumed yet (issue #418).
/// </summary>
/// <param name="SubscriptionId">Subscription the usages belong to.</param>
/// <param name="TotalDiscount">Sum of uninvoiced discounts.</param>
/// <param name="Codes">Applied codes backing the discount.</param>
/// <param name="UsageIds">PromoCodeUse row identifiers, for invoice stamping.</param>
public sealed record SubscriptionPromoInvoiceContext(
    Guid SubscriptionId,
    decimal TotalDiscount,
    IReadOnlyList<SubscriptionPromoCodeApplication> Codes,
    IReadOnlyList<Guid> UsageIds);

/// <summary>
///     Bridges the promo engine (Products module) into subscription creation and invoice
///     materialization (issue #418). Implemented by the Products module so the Subscriptions
///     and Billing project graphs keep their dependency direction.
/// </summary>
public interface ISubscriptionPromoCoordinator
{
    /// <summary>
    ///     Quotes promo codes for a subscription being created on the given plan. Read-only:
    ///     validates (including plan scoping), resolves stacking, and returns the discounted
    ///     amount plus any trial/free-month grants. No usage rows are written.
    /// </summary>
    Task<SubscriptionPromoQuoteResult> QuoteForSubscriptionCreationAsync(
        SubscriptionPromoQuoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists one usage row per applied code, linked to the created subscription
    ///     (idempotent on <paramref name="request"/>'s key, fail-closed on limits).
    /// </summary>
    /// <returns>The identifiers of the usage rows that were recorded or replayed.</returns>
    Task<IReadOnlyList<Guid>> RecordSubscriptionUsageAsync(
        SubscriptionPromoUsageRecordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the subscription's promo usages that no invoice has consumed yet.
    /// </summary>
    Task<SubscriptionPromoInvoiceContext?> GetUninvoicedPromoContextAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stamps the given promo usage rows as settled on an invoice. Idempotent: rows
    ///     already carrying an invoice id are left untouched.
    /// </summary>
    /// <returns>The number of usage rows that were stamped.</returns>
    Task<int> MarkPromoUsesInvoicedAsync(
        IReadOnlyList<Guid> usageIds,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
