namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handles Stripe customer lifecycle: creation, payment methods, and subscriptions.
/// </summary>
public interface IStripeCustomerService
{
    /// <summary>
    ///     Creates a customer in Stripe.
    /// </summary>
    Task<GatewayCustomerResult> CreateCustomerAsync(
        GatewayCustomerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attaches a payment method to a Stripe customer.
    /// </summary>
    Task<GatewayPaymentMethodResult> CreatePaymentMethodAsync(
        GatewayPaymentMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a Stripe SetupIntent for collecting a reusable payment method.
    /// </summary>
    Task<GatewaySetupIntentResult> CreateSetupIntentAsync(
        GatewaySetupIntentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets the default payment method for a Stripe customer.
    /// </summary>
    Task<GatewayDefaultPaymentMethodResult> SetDefaultPaymentMethodAsync(
        GatewayDefaultPaymentMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves the gateway's authoritative payment-method details (display metadata
    ///     and card fingerprint) for a previously tokenized payment method.
    /// </summary>
    Task<GatewayPaymentMethodDetailsResult> GetPaymentMethodAsync(
        string paymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a subscription in Stripe.
    /// </summary>
    Task<GatewayCancellationResult> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default);
}

public sealed record GatewaySetupIntentRequest(
    string CustomerId,
    Dictionary<string, string>? Metadata = null);

public sealed record GatewaySetupIntentResult(
    bool Success,
    string? ExternalSetupIntentId,
    string? ClientSecret,
    string? CustomerId,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record GatewayDefaultPaymentMethodRequest(
    string CustomerId,
    string PaymentMethodId);

public sealed record GatewayDefaultPaymentMethodResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
///     Display-safe payment-method details resolved from the gateway (issue #414).
///     Contains only gateway references and display metadata — never raw card data.
/// </summary>
public sealed record GatewayPaymentMethodDetails(
    string Type,
    string? CardBrand,
    string? CardLast4,
    int? CardExpMonth,
    int? CardExpYear,
    string? CardFingerprint,
    string? BankName,
    string? BankLast4,
    string? WalletEmail);

/// <summary>
///     Result of resolving payment-method details from the gateway.
/// </summary>
public sealed record GatewayPaymentMethodDetailsResult(
    bool Success,
    string? PaymentMethodId,
    GatewayPaymentMethodDetails? Details,
    string? ErrorCode,
    string? ErrorMessage);
