namespace GameGuild.Commerce.Payments;

/// <summary>
///     Routing context for gateway selection (issue #413). Carries the eligibility
///     dimensions the routing filters evaluate: currency, billing country, payment
///     method type and amount. All dimensions are optional; the selector only applies
///     the filters a gateway route actually declares.
/// </summary>
public sealed record GatewayRouteContext(
    string Currency,
    string? Country = null,
    string? PaymentMethodType = null,
    decimal? Amount = null)
{
    /// <summary>
    ///     Derives a routing context from a gateway charge request. The payment method
    ///     type is inferred from the payment method identifier prefix (pm_ / seti_ → card,
    ///     ba_ / paypal → paypal); a country may be supplied by callers through the
    ///     <c>country_code</c> metadata key.
    /// </summary>
    public static GatewayRouteContext FromRequest(GatewayPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? country = null;
        if (request.Metadata is not null &&
            request.Metadata.TryGetValue(CountryMetadataKey, out var metadataCountry) &&
            !string.IsNullOrWhiteSpace(metadataCountry))
        {
            country = metadataCountry;
        }

        return new GatewayRouteContext(
            request.Currency,
            country,
            InferPaymentMethodType(request.PaymentMethodId),
            request.Amount);
    }

    /// <summary>Metadata key callers may use to supply the billing country for routing.</summary>
    public const string CountryMetadataKey = "country_code";

    /// <summary>
    ///     Maps a provider payment method identifier onto a coarse routing type.
    ///     Stripe-prefixed identifiers map to <c>card</c>, PayPal billing agreements to
    ///     <c>paypal</c>; anything else is left untyped so it only matches unrestricted routes.
    /// </summary>
    public static string? InferPaymentMethodType(string? paymentMethodId)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodId))
        {
            return null;
        }

        var separatorIndex = paymentMethodId.IndexOf('_', StringComparison.Ordinal);
        return separatorIndex > 0
            ? paymentMethodId[..separatorIndex].ToLowerInvariant() switch
            {
                "pm" or "seti" or "card" => "card",
                "ba" or "paypal" => "paypal",
                _ => null
            }
            : null;
    }
}

/// <summary>
///     A gateway eligible for a routing context, in selection order (issue #413).
/// </summary>
public sealed record GatewayCandidate(
    IPaymentGateway Gateway,
    int Priority,
    GatewayRouteEntry? Route);

/// <summary>
///     Fail-closed gateway selection (issue #413): resolves the registered gateways that are
///     enabled and match the routing filters, ordered by priority. Implementations must never
///     fall back silently to a default gateway — when no gateway is eligible the selector
///     throws <see cref="GatewaySelectionFailedException" /> so the payment path fails explicitly.
/// </summary>
public interface IPaymentGatewaySelector
{
    /// <summary>
    ///     Selects the gateways eligible for the given routing context, ordered by ascending
    ///     priority. The returned list is empty only when no gateway is eligible; callers that
    ///     need a gateway must treat an empty result as a failure (fail closed).
    /// </summary>
    IReadOnlyList<GatewayCandidate> Select(GatewayRouteContext context);

    /// <summary>
    ///     Resolves the primary (highest-priority eligible) gateway. Fails closed with
    ///     <see cref="GatewaySelectionFailedException" /> when no gateway is eligible —
    ///     never returns null and never substitutes a disabled gateway.
    /// </summary>
    GatewayCandidate SelectPrimary(GatewayRouteContext? context = null);
}

/// <summary>
///     Explicit fail-closed failure raised when gateway selection cannot produce an
///     eligible gateway (issue #413). Extends <see cref="InvalidOperationException" /> so the
///     existing command-handler error semantics keep applying. There is never a silent
///     fallback: every reason lists each candidate gateway and why it was rejected.
/// </summary>
public sealed class GatewaySelectionFailedException : InvalidOperationException
{
    /// <summary>Initializes the exception with the aggregate selection failure reason.</summary>
    public GatewaySelectionFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Builds the exception from per-gateway rejection reasons. When no gateways are
    ///     registered at all the failure states that no processing gateway exists.
    /// </summary>
    public static GatewaySelectionFailedException Create(
        GatewayRouteContext context,
        IReadOnlyList<(string ProviderId, string Reason)> rejections)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rejections);

        var detail = rejections.Count == 0
            ? "No payment gateway adapters are registered."
            : string.Join("; ", rejections.Select(rejection => $"{rejection.ProviderId}: {rejection.Reason}"));

        return new GatewaySelectionFailedException(
            $"No eligible payment gateway for currency {context.Currency.ToUpperInvariant()}" +
            (string.IsNullOrWhiteSpace(context.Country) ? string.Empty : $", country {context.Country.ToUpperInvariant()}") +
            (string.IsNullOrWhiteSpace(context.PaymentMethodType) ? string.Empty : $", payment method {context.PaymentMethodType}") +
            $" ({detail}).");
    }
}
