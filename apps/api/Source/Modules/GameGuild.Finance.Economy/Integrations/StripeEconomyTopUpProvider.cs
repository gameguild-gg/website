using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Funding;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Integrations;

public sealed class StripeEconomyTopUpProvider(
    IOptions<StripeGatewayOptions> options,
    IStripePaymentService stripe) : IEconomyTopUpProvider
{
    private const string Provider = "stripe";
    private readonly StripeGatewayOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly IStripePaymentService _stripe = stripe ?? throw new ArgumentNullException(nameof(stripe));

    public async ValueTask<EconomyTopUpProviderResult> CreateAsync(
        EconomyTopUpProviderCreateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateConfiguration();
        Validate(request);
        var result = await _stripe.CreatePaymentIntentAsync(
            new GatewayPaymentIntentSetupRequest(
                $"economy-top-up:{request.IdempotencyKey}",
                request.UsdMinorUnits / 100m,
                request.Currency,
                "Economy HardCoin top-up",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["economy_top_up_id"] = request.TopUpId.ToString("N"),
                    ["tenant_id"] = request.TenantId.ToString("N"),
                    ["purpose"] = "economy_hard_coin_top_up"
                }),
            cancellationToken).ConfigureAwait(false);
        if (result.OutcomeUnknown)
            throw new EconomyTopUpProviderAmbiguousException(
                "Stripe payment-intent creation requires provider reconciliation.");
        return Map(
            result.TransactionId,
            result.Status,
            result.ClientSecret,
            result.ProviderMapping);
    }

    public async ValueTask<EconomyTopUpProviderResult> RetrieveAsync(
        string providerObjectId,
        CancellationToken cancellationToken)
    {
        ValidateConfiguration();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerObjectId);
        var result = await _stripe.GetPaymentAsync(providerObjectId, cancellationToken).ConfigureAwait(false);
        if (string.Equals(result.ErrorCode, "stripe_outcome_unknown", StringComparison.Ordinal))
            throw new EconomyTopUpProviderAmbiguousException(
                "Stripe payment-intent retrieval requires provider reconciliation.");
        return Map(
            result.TransactionId,
            result.Status,
            result.ClientActionToken,
            result.ProviderMapping);
    }

    internal void ValidateConfiguration()
    {
        if (!_options.IsEnabled || _options.UseSimulation ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.PublishableKey) ||
            string.IsNullOrWhiteSpace(_options.AccountId))
            throw new EconomyTopUpProviderUnavailableException(
                "Stripe top-up is disabled until real provider configuration is complete.");
    }

    internal static void Validate(EconomyTopUpProviderCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TopUpId == Guid.Empty || request.TenantId == Guid.Empty)
            throw new ArgumentException("Top-up and tenant IDs are required.", nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.UsdMinorUnits);
        if (!string.Equals(request.Currency, "USD", StringComparison.Ordinal))
            throw new ArgumentException("HardCoin top-ups require USD provider currency.", nameof(request));
        _ = new Contracts.IdempotencyKey(request.IdempotencyKey);
    }

    private EconomyTopUpProviderResult Map(
        string? transactionId,
        PaymentStatus status,
        string? clientSecret,
        GatewayProviderMapping? mapping)
    {
        if (string.IsNullOrWhiteSpace(transactionId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            mapping is null ||
            !string.Equals(mapping.ProviderObjectId, transactionId, StringComparison.Ordinal) ||
            !string.Equals(mapping.ProviderObjectType, "payment_intent", StringComparison.Ordinal) ||
            !string.Equals(mapping.ProviderMonetaryLeg, "capture", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(mapping.ProviderEnvironment) ||
            string.IsNullOrWhiteSpace(mapping.ProviderAccountId))
            throw new EconomyTopUpProviderUnavailableException(
                "Stripe returned an incomplete payment-intent binding.");

        var topUpStatus = status switch
        {
            PaymentStatus.Pending or PaymentStatus.RequiresAction => EconomyTopUpProviderStatus.RequiresAction,
            PaymentStatus.Processing => EconomyTopUpProviderStatus.Processing,
            _ => throw new EconomyTopUpProviderUnavailableException(
                "Stripe returned a payment-intent state that cannot start a top-up.")
        };
        return new EconomyTopUpProviderResult(
            Provider,
            mapping.ProviderEnvironment,
            mapping.ProviderAccountId,
            mapping.ProviderObjectId,
            mapping.ProviderObjectType,
            mapping.ProviderMonetaryLeg,
            topUpStatus,
            clientSecret,
            _options.PublishableKey);
    }
}

public sealed class EconomyTopUpProviderUnavailableException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed class EconomyTopUpProviderAmbiguousException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
