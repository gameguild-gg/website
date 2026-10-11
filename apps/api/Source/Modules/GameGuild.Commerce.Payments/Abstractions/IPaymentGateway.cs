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
    /// </summary>
    Task<GatewayPaymentResult> GetPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a provider payment attempt before a replacement attempt may be created.
    /// </summary>
    Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default);

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
    GatewayProviderMapping? ProviderMapping = null);

/// <summary>
///     Immutable provider identity for one payment object and monetary leg.
/// </summary>
public sealed record GatewayProviderMapping(
    string ProviderEnvironment,
    string ProviderAccountId,
    string ProviderObjectId,
    string ProviderObjectType,
    string ProviderMonetaryLeg);

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
    string? Reason);

/// <summary>
///     Result of a refund processing attempt
/// </summary>
public sealed record GatewayRefundResult(
    bool Success,
    string? RefundId,
    decimal AmountRefunded,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime ProcessedAt);

/// <summary>
///     Request to accept a gateway dispute (issue #419). Accepting acknowledges the dispute
/// as lost; the actual money movement is settled by the gateway.
/// </summary>
public sealed record GatewayDisputeActionRequest(
    string IdempotencyKey,
    string ExternalDisputeId);

/// <summary>
///     Request to challenge a gateway dispute by submitting merchant evidence (issue #419).
/// </summary>
/// <param name="IdempotencyKey">Idempotency key for the gateway call.</param>
/// <param name="ExternalDisputeId">Gateway dispute identifier.</param>
/// <param name="EvidenceSummary">Free-text evidence bundle submitted with the challenge.</param>
public sealed record GatewayDisputeChallengeRequest(
    string IdempotencyKey,
    string ExternalDisputeId,
    string? EvidenceSummary = null);

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
