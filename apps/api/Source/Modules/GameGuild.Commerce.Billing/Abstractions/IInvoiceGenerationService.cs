namespace GameGuild.Commerce.Billing;

/// <summary>
///     Request to materialize the invoice for one subscription billing cycle whose payment is already confirmed.
/// </summary>
/// <param name="SubscriptionId">Local subscription identifier.</param>
/// <param name="BillingCycleNumber">1-based billing cycle the confirmed payment belongs to.</param>
/// <param name="Amount">Confirmed payment amount (must match the authoritative subscription price).</param>
/// <param name="Currency">ISO 4217 currency of the confirmed payment.</param>
/// <param name="ProcessedAtUtc">When the payment was confirmed.</param>
/// <param name="PaymentId">
///     Local payment identifier when the cycle was paid through the first-party payment pipeline.
///     Null for provider-billed cycles (Stripe Billing), where the provider invoice id identifies the payment.
/// </param>
/// <param name="ProviderInvoiceId">
///     Provider invoice identifier (e.g. Stripe <c>in_...</c>) when the cycle was billed by the provider.
///     Stored via <see cref="Invoice.SetExternalId"/>; provider data is authoritative.
/// </param>
public sealed record ConfirmedCycleInvoiceRequest(
    Guid SubscriptionId,
    int BillingCycleNumber,
    decimal Amount,
    string Currency,
    DateTime ProcessedAtUtc,
    Guid? PaymentId = null,
    string? ProviderInvoiceId = null);

/// <summary>
///     Outcome of an invoice materialization attempt.
/// </summary>
/// <param name="Invoice">The invoice that exists for the cycle (newly created or previously materialized).</param>
/// <param name="Created"><c>true</c> when this call created the invoice; <c>false</c> when an invoice already existed (idempotent replay).</param>
public sealed record InvoiceGenerationResult(Invoice Invoice, bool Created);

/// <summary>
///     Materializes the local <see cref="Invoice"/> for a subscription billing cycle on confirmed payment.
///     This is the production invoice writer: issuing, payment recording, read-model persistence
///     (the subscription invoices read model is a SQL projection of the invoices table) and
///     invoice-issued email dispatch all happen through this service.
/// </summary>
/// <remarks>
///     Idempotency: keyed by <c>subscription:{{subscriptionId}}:cycle:{{n}}:invoice</c> with a
///     check-then-create flow and a unique index backstop, so double-confirming a cycle
///     (command replay or webhook redelivery) yields exactly one invoice.
/// </remarks>
public interface IInvoiceGenerationService
{
    /// <summary>
    ///     Materializes (or returns the already-materialized) invoice for the confirmed billing cycle.
    /// </summary>
    Task<InvoiceGenerationResult> MaterializeForConfirmedCycleAsync(
        ConfirmedCycleInvoiceRequest request,
        CancellationToken cancellationToken = default);
}
