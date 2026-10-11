using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Factory for creating simulated payment gateway responses.
///     Used during development/testing when real payment gateway SDKs are not integrated.
///     Centralizes all simulated response patterns for DRY compliance.
/// </summary>
/// <remarks>
///     This factory should be removed or disabled in production environments.
///     All methods generate realistic-looking but fake transaction IDs following the
///     Stripe ID format conventions (prefix + random GUID).
/// </remarks>
public static class SimulatedPaymentResultFactory
{
    /// <summary>
    ///     Stripe-style prefixes for different transaction types
    /// </summary>
    private static class Prefixes
    {
        public const string PaymentIntent = "pi_";
        public const string Charge = "ch_";
        public const string Refund = "re_";
        public const string Customer = "cus_";
        public const string PaymentMethod = "pm_";
        public const string SetupIntent = "seti_";
    }

    /// <summary>
    ///     Generates a Stripe-style transaction ID with the given prefix.
    /// </summary>
    private static string GenerateId(string prefix) => $"{prefix}{Guid.NewGuid():N}";

    /// <summary>
    ///     Creates a successful simulated payment result.
    /// </summary>
    public static GatewayPaymentResult PaymentSuccess(ILogger? logger = null)
    {
        var transactionId = GenerateId(Prefixes.PaymentIntent);
        logger?.LogDebug("Generated simulated Stripe payment: {TransactionId}", transactionId);

        return new GatewayPaymentResult(
            Success: true,
            TransactionId: transactionId,
            ExternalPaymentId: GenerateId(Prefixes.Charge),
            ErrorCode: null,
            ErrorMessage: null,
            Status: PaymentStatus.Succeeded,
            ProcessedAt: SystemClock.UtcNow,
            ProviderMapping: new GatewayProviderMapping(
                "test",
                "acct_simulated",
                transactionId,
                "payment_intent",
                "capture"));
    }

    /// <summary>
    ///     Creates a failed simulated payment result.
    /// </summary>
    public static GatewayPaymentResult PaymentFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayPaymentResult(
            Success: false,
            TransactionId: null,
            ExternalPaymentId: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            Status: PaymentStatus.Failed,
            ProcessedAt: SystemClock.UtcNow);
    }

    /// <summary>
    ///     Creates a successful simulated refund result.
    /// </summary>
    public static GatewayRefundResult RefundSuccess(decimal amount, ILogger? logger = null)
    {
        var refundId = GenerateId(Prefixes.Refund);
        logger?.LogDebug("Generated simulated Stripe refund: {RefundId}", refundId);

        return new GatewayRefundResult(
            Success: true,
            RefundId: refundId,
            AmountRefunded: amount,
            ErrorCode: null,
            ErrorMessage: null,
            ProcessedAt: SystemClock.UtcNow);
    }

    /// <summary>
    ///     Creates a failed simulated refund result.
    /// </summary>
    public static GatewayRefundResult RefundFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayRefundResult(
            Success: false,
            RefundId: null,
            AmountRefunded: 0,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            ProcessedAt: SystemClock.UtcNow);
    }

    /// <summary>
    ///     Creates a successful simulated customer result.
    /// </summary>
    public static GatewayCustomerResult CustomerSuccess(ILogger? logger = null)
    {
        var customerId = GenerateId(Prefixes.Customer);
        logger?.LogDebug("Generated simulated Stripe customer: {CustomerId}", customerId);

        return new GatewayCustomerResult(
            Success: true,
            ExternalCustomerId: customerId,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    ///     Creates a failed simulated customer result.
    /// </summary>
    public static GatewayCustomerResult CustomerFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayCustomerResult(
            Success: false,
            ExternalCustomerId: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    ///     Creates a successful simulated payment method result.
    /// </summary>
    /// <param name="testCard">Optional test card configuration (defaults to Visa 4242)</param>
    /// <param name="logger">Optional logger for debug output</param>
    public static GatewayPaymentMethodResult PaymentMethodSuccess(
        SimulatedTestCard? testCard = null,
        ILogger? logger = null)
    {
        testCard ??= SimulatedTestCard.Visa4242;
        var paymentMethodId = GenerateId(Prefixes.PaymentMethod);
        logger?.LogDebug("Generated simulated Stripe payment method: {PaymentMethodId}", paymentMethodId);

        return new GatewayPaymentMethodResult(
            Success: true,
            ExternalPaymentMethodId: paymentMethodId,
            CardLast4: testCard.Last4,
            CardBrand: testCard.Brand,
            ExpiryMonth: testCard.ExpiryMonth,
            ExpiryYear: testCard.ExpiryYear,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    ///     Creates a failed simulated payment method result.
    /// </summary>
    public static GatewayPaymentMethodResult PaymentMethodFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayPaymentMethodResult(
            Success: false,
            ExternalPaymentMethodId: null,
            CardLast4: null,
            CardBrand: null,
            ExpiryMonth: null,
            ExpiryYear: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    ///     Creates a successful simulated payment-method details result (issue #414).
    /// </summary>
    /// <param name="paymentMethodId">The payment method ID to resolve</param>
    /// <param name="testCard">Optional test card configuration (defaults to Visa 4242)</param>
    /// <param name="logger">Optional logger for debug output</param>
    public static GatewayPaymentMethodDetailsResult PaymentMethodDetails(
        string paymentMethodId,
        SimulatedTestCard? testCard = null,
        ILogger? logger = null)
    {
        testCard ??= SimulatedTestCard.Visa4242;
        logger?.LogDebug("Generated simulated Stripe payment method details for: {PaymentMethodId}", paymentMethodId);

        return new GatewayPaymentMethodDetailsResult(
            Success: true,
            PaymentMethodId: paymentMethodId,
            Details: new GatewayPaymentMethodDetails(
                Type: "card",
                CardBrand: testCard.Brand,
                CardLast4: testCard.Last4,
                CardExpMonth: testCard.ExpiryMonth,
                CardExpYear: testCard.ExpiryYear,
                CardFingerprint: "fp_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(paymentMethodId)))[..16].ToLowerInvariant(),
                BankName: null,
                BankLast4: null,
                WalletEmail: null),
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    ///     Creates a failed simulated payment-method details result.
    /// </summary>
    public static GatewayPaymentMethodDetailsResult PaymentMethodDetailsFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayPaymentMethodDetailsResult(
            Success: false,
            PaymentMethodId: null,
            Details: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    ///     Creates a successful simulated setup intent result.
    /// </summary>
    public static GatewaySetupIntentResult SetupIntentSuccess(string customerId, ILogger? logger = null)
    {
        var setupIntentId = GenerateId(Prefixes.SetupIntent);
        var clientSecret = $"{setupIntentId}_secret_{Guid.NewGuid():N}";
        logger?.LogDebug("Generated simulated Stripe setup intent: {SetupIntentId}", setupIntentId);

        return new GatewaySetupIntentResult(
            Success: true,
            ExternalSetupIntentId: setupIntentId,
            ClientSecret: clientSecret,
            CustomerId: customerId,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    ///     Creates a failed simulated setup intent result.
    /// </summary>
    public static GatewaySetupIntentResult SetupIntentFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewaySetupIntentResult(
            Success: false,
            ExternalSetupIntentId: null,
            ClientSecret: null,
            CustomerId: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    ///     Creates a successful simulated default payment method update result.
    /// </summary>
    public static GatewayDefaultPaymentMethodResult DefaultPaymentMethodSuccess()
    {
        return new GatewayDefaultPaymentMethodResult(
            Success: true,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    ///     Creates a failed simulated default payment method update result.
    /// </summary>
    public static GatewayDefaultPaymentMethodResult DefaultPaymentMethodFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayDefaultPaymentMethodResult(
            Success: false,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    ///     Creates a successful simulated cancellation result.
    /// </summary>
    public static GatewayCancellationResult CancellationSuccess(DateTime? effectiveDate = null, ILogger? logger = null)
    {
        effectiveDate ??= SystemClock.UtcNow;
        logger?.LogDebug("Generated simulated Stripe subscription cancellation");

        return new GatewayCancellationResult(
            Success: true,
            ErrorCode: null,
            ErrorMessage: null,
            EffectiveDate: effectiveDate.Value);
    }

    /// <summary>
    ///     Creates a failed simulated cancellation result.
    /// </summary>
    public static GatewayCancellationResult CancellationFailure(string errorMessage, string errorCode = "stripe_error")
    {
        return new GatewayCancellationResult(
            Success: false,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            EffectiveDate: null);
    }
}

/// <summary>
///     Simulated test card configuration for payment method simulation.
/// </summary>
public sealed class SimulatedTestCard
{
    /// <summary>
    ///     Standard Stripe test card (Visa ending in 4242).
    /// </summary>
    public static readonly SimulatedTestCard Visa4242 = new("4242", "visa", 12, SystemClock.UtcNow.Year + 3);

    /// <summary>
    ///     Mastercard test card ending in 5555.
    /// </summary>
    public static readonly SimulatedTestCard Mastercard5555 = new("5555", "mastercard", 10, SystemClock.UtcNow.Year + 2);

    /// <summary>
    ///     Amex test card ending in 8431.
    /// </summary>
    public static readonly SimulatedTestCard Amex8431 = new("8431", "amex", 6, SystemClock.UtcNow.Year + 4);

    public SimulatedTestCard(string last4, string brand, int expiryMonth, int expiryYear)
    {
        Last4 = last4;
        Brand = brand;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
    }

    public string Last4 { get; }
    public string Brand { get; }
    public int ExpiryMonth { get; }
    public int ExpiryYear { get; }
}
