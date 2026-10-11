using Microsoft.Extensions.Options;
using Stripe;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Stripe payment gateway facade.
///     Delegates to <see cref="IStripePaymentService"/> and <see cref="IStripeCustomerService"/>
///     while satisfying the <see cref="IPaymentGateway"/> contract for backward compatibility.
/// </summary>
public class StripePaymentGateway(
    IOptions<StripeGatewayOptions> options,
    IStripePaymentService paymentService,
    IStripeCustomerService customerService) : IPaymentGateway
{
    private readonly StripeGatewayOptions _options = options.Value;

    /// <summary>
    ///     Configures the Stripe API key on first access when not in simulation mode.
    /// </summary>
    internal static void EnsureApiKey(StripeGatewayOptions opts)
    {
        if (!opts.UseSimulation && !string.IsNullOrEmpty(opts.ApiKey))
        {
            StripeConfiguration.ApiKey = opts.ApiKey;
        }
    }

    /// <inheritdoc />
    public string ProviderId => "stripe";

    /// <inheritdoc />
    public string DisplayName => "Stripe";

    /// <inheritdoc />
    public bool IsEnabled => _options.IsEnabled;

    /// <inheritdoc />
    public Task<GatewayPaymentResult> ProcessPaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default) =>
        paymentService.ProcessPaymentAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayPaymentResult> GetPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default) =>
        paymentService.GetPaymentAsync(externalTransactionId, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default) =>
        paymentService.CancelPaymentAsync(externalTransactionId, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayRefundResult> ProcessRefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default) =>
        paymentService.ProcessRefundAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayDisputeActionResult> AcceptDisputeAsync(
        GatewayDisputeActionRequest request,
        CancellationToken cancellationToken = default) =>
        paymentService.AcceptDisputeAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayDisputeActionResult> ChallengeDisputeAsync(
        GatewayDisputeChallengeRequest request,
        CancellationToken cancellationToken = default) =>
        paymentService.ChallengeDisputeAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ValidateWebhookSignatureAsync(
        string payload,
        string signature,
        string secret) =>
        paymentService.ValidateWebhookSignatureAsync(payload, signature, secret);

    /// <inheritdoc />
    public Task<GatewayCustomerResult> CreateCustomerAsync(
        GatewayCustomerRequest request,
        CancellationToken cancellationToken = default) =>
        customerService.CreateCustomerAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayPaymentMethodResult> CreatePaymentMethodAsync(
        GatewayPaymentMethodRequest request,
        CancellationToken cancellationToken = default) =>
        customerService.CreatePaymentMethodAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayCancellationResult> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default) =>
        customerService.CancelSubscriptionAsync(externalSubscriptionId, cancellationToken);
}

/// <summary>
///     Configuration options for the Stripe payment gateway.
/// </summary>
public class StripeGatewayOptions
{
    /// <summary>
    ///     Section name in configuration.
    /// </summary>
    public const string SectionName = "PaymentGateways:Stripe";

    /// <summary>
    ///     Whether Stripe is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    ///     Whether to use simulated responses instead of real Stripe API.
    ///     Should be true for development/testing, false for production.
    /// </summary>
    public bool UseSimulation { get; set; } = false;

    /// <summary>
    ///     Stripe API key (secret key).
    ///     Required when UseSimulation is false.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    ///     Stripe publishable key (for client-side).
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    ///     Platform/default Stripe account used when no Connect account is configured.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    ///     Optional Stripe Connect account used for outbound API requests and as the payment-object owner.
    ///     Empty means requests target the platform account.
    /// </summary>
    public string ConnectedAccountId { get; set; } = string.Empty;

    /// <summary>
    ///     Whether configured credentials address Stripe live mode.
    /// </summary>
    public bool LiveMode { get; set; }

    /// <summary>
    ///     Webhook signing secret.
    ///     Required for webhook signature verification.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    ///     Tolerance in seconds for webhook timestamp validation.
    ///     Default is 300 seconds (5 minutes) as recommended by Stripe.
    /// </summary>
    public long WebhookToleranceSeconds { get; set; } = 300;
}
