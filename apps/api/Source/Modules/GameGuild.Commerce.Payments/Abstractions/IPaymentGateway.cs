namespace GameGuild.Commerce.Payments;

/// <summary>
///     Abstraction for payment gateway providers (Stripe, PayPal, etc.)
///     Enables testability and gateway switching without code changes.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    ///     Unique identifier for this payment gateway
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    ///     Display name for this payment gateway
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Whether this gateway is currently enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Processes a payment through this gateway
    /// </summary>
    /// <param name="request">Payment request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the payment processing</returns>
    Task<GatewayPaymentResult> ProcessPaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves the provider's authoritative state for an existing payment attempt.
    ///     <paramref name="providerId" /> routes the inquiry to the gateway that owns the
    ///     external object when multiple gateways are registered (issue #413).
    /// </summary>
    Task<GatewayPaymentResult> GetPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default,
        string? providerId = null);

    /// <summary>
    ///     Cancels a provider payment attempt before a replacement attempt may be created.
    ///     <paramref name="providerId" /> routes the cancellation to the gateway that owns the
    ///     external object when multiple gateways are registered (issue #413).
    /// </summary>
    Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default,
        string? providerId = null);

    /// <summary>
    ///     Processes a refund through this gateway
    /// </summary>
    /// <param name="request">Refund request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the refund processing</returns>
    Task<GatewayRefundResult> ProcessRefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Accepts a gateway dispute, acknowledging it as lost so the disputed funds are
    ///     returned to the customer (issue #419).
    /// </summary>
    /// <param name="request">Dispute action request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the dispute action</returns>
    Task<GatewayDisputeActionResult> AcceptDisputeAsync(
        GatewayDisputeActionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Challenges a gateway dispute by submitting the merchant evidence (issue #419).
    /// </summary>
    /// <param name="request">Dispute challenge request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the dispute action</returns>
    Task<GatewayDisputeActionResult> ChallengeDisputeAsync(
        GatewayDisputeChallengeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates a webhook signature from this gateway
    /// </summary>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="signature">Signature header value</param>
    /// <param name="secret">Webhook signing secret</param>
    /// <returns>True if signature is valid</returns>
    Task<bool> ValidateWebhookSignatureAsync(
        string payload,
        string signature,
        string secret);

    /// <summary>
    ///     Creates a customer in the gateway's system
    /// </summary>
    /// <param name="request">Customer creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>External customer ID</returns>
    Task<GatewayCustomerResult> CreateCustomerAsync(
        GatewayCustomerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a payment method for a customer
    /// </summary>
    /// <param name="request">Payment method request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>External payment method ID</returns>
    Task<GatewayPaymentMethodResult> CreatePaymentMethodAsync(
        GatewayPaymentMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a subscription in the gateway's system
    /// </summary>
    /// <param name="externalSubscriptionId">External subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GatewayCancellationResult> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Request for processing a payment
/// </summary>
public sealed record GatewayPaymentRequest(
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    string? CustomerId,
    string? PaymentMethodId,
    string? Description,
    Dictionary<string, string>? Metadata = null);

/// <summary>
///     Result of a payment processing attempt
/// </summary>
public sealed record GatewayPaymentResult(
    bool Success,
    string? TransactionId,
    string? ExternalPaymentId,
    string? ErrorCode,
    string? ErrorMessage,
    PaymentStatus Status,
    DateTime ProcessedAt,
    string? ClientActionToken = null,
    GatewayProviderMapping? ProviderMapping = null,
    string? ProviderId = null,
    GatewayFailureKind FailureKind = GatewayFailureKind.NotApplicable);

/// <summary>
///     Immutable provider identity for one payment object and monetary leg.
/// </summary>
public sealed record GatewayProviderMapping(
    string ProviderEnvironment,
    string ProviderAccountId,
    string ProviderObjectId,
    string ProviderObjectType,
    string ProviderMonetaryLeg);

/// <summary>
///     Failure classification used by the multi-gateway failover orchestration
///     (issue #413). Adapters must classify every failed result; the fail-closed
///     default is <see cref="NotApplicable" />, meaning "not eligible for any
///     automated retry or failover" — an unclassified failure never reroutes.
/// </summary>
public enum GatewayFailureKind
{
    /// <summary>
    ///     The result is not a failure, or the adapter does not classify failures.
    ///     Never retried and never failed over (fail-closed default).
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    ///     The gateway itself failed transiently (transport error, rate limit, 5xx)
    ///     without confirming a provider payment object. Eligible for a same-gateway
    ///     retry and, once that budget is spent, failover to the next-priority gateway.
    /// </summary>
    RetriableGatewayFailure = 1,

    /// <summary>
    ///     The gateway answered with a definitive negative (for example a card
    ///     decline or a configuration error). Retrying — on this gateway or any
    ///     other — is not attempted automatically.
    /// </summary>
    TerminalDecline = 2,

    /// <summary>
    ///     The request may have reached the gateway but the outcome could not be
    ///     determined (timeout after send, 5xx on a confirm call). Replaying the
    ///     charge anywhere risks a double capture, so the attempt is left for
    ///     provider reconciliation instead of being retried or failed over.
    /// </summary>
    OutcomeUnknown = 3
}

/// <summary>Result of closing a provider payment attempt.</summary>
public sealed record GatewayPaymentCancellationResult(
    bool Success,
    bool OutcomeUnknown,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
///     Request for processing a refund
/// </summary>
public sealed record GatewayRefundRequest(
    string IdempotencyKey,
    string OriginalTransactionId,
    decimal? Amount,
    string? Reason,
    string? ProviderId = null,
    string? Currency = null);

/// <summary>
///     Result of a refund processing attempt
/// </summary>
public sealed record GatewayRefundResult(
    bool Success,
    string? RefundId,
    decimal AmountRefunded,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime ProcessedAt,
    GatewayFailureKind FailureKind = GatewayFailureKind.NotApplicable);

/// <summary>
///     Request to accept a gateway dispute (issue #419). Accepting acknowledges the dispute
/// as lost; the actual money movement is settled by the gateway.
/// </summary>
/// <param name="IdempotencyKey">Idempotency key for the gateway call.</param>
/// <param name="ExternalDisputeId">Gateway dispute identifier.</param>
/// <param name="ProviderId">
///     Owner gateway for the disputed payment; dispute actions are pinned to it and never
///     rerouted (issue #413 routing rules).
/// </param>
public sealed record GatewayDisputeActionRequest(
    string IdempotencyKey,
    string ExternalDisputeId,
    string? ProviderId = null);

/// <summary>
///     Request to challenge a gateway dispute by submitting merchant evidence (issue #419).
/// </summary>
/// <param name="IdempotencyKey">Idempotency key for the gateway call.</param>
/// <param name="ExternalDisputeId">Gateway dispute identifier.</param>
/// <param name="EvidenceSummary">Free-text evidence bundle submitted with the challenge.</param>
/// <param name="ProviderId">Owner gateway for the disputed payment (issue #413 routing rules).</param>
public sealed record GatewayDisputeChallengeRequest(
    string IdempotencyKey,
    string ExternalDisputeId,
    string? EvidenceSummary = null,
    string? ProviderId = null);

/// <summary>Result of a dispute accept or challenge action (issue #419).</summary>
public sealed record GatewayDisputeActionResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
///     Request for creating a customer
/// </summary>
public sealed record GatewayCustomerRequest(
    string Email,
    string? Name,
    string? Phone,
    Dictionary<string, string>? Metadata = null);

/// <summary>
///     Result of customer creation
/// </summary>
public sealed record GatewayCustomerResult(
    bool Success,
    string? ExternalCustomerId,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
///     Request for creating a payment method
/// </summary>
public sealed record GatewayPaymentMethodRequest(
    string CustomerId,
    string PaymentMethodToken,
    bool SetAsDefault = true);

/// <summary>
///     Result of payment method creation
/// </summary>
public sealed record GatewayPaymentMethodResult(
    bool Success,
    string? ExternalPaymentMethodId,
    string? CardLast4,
    string? CardBrand,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
///     Result of subscription cancellation
/// </summary>
public sealed record GatewayCancellationResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime? EffectiveDate);

/// <summary>
///     Payment processing status
/// </summary>
public enum PaymentStatus
{
    /// <summary>Payment is pending processing</summary>
    Pending,
    /// <summary>Payment is being processed</summary>
    Processing,
    /// <summary>Payment completed successfully</summary>
    Succeeded,
    /// <summary>Payment failed</summary>
    Failed,
    /// <summary>Payment was cancelled</summary>
    Cancelled,
    /// <summary>Payment requires additional action (e.g., 3DS)</summary>
    RequiresAction,
    /// <summary>Payment was refunded</summary>
    Refunded,
    /// <summary>Payment is disputed</summary>
    Disputed,
    /// <summary>Payment was partially refunded; further refunds allowed up to the original amount (issue #419)</summary>
    PartiallyRefunded
}
