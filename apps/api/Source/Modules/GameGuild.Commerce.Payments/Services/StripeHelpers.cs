using System.Text.RegularExpressions;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Converts between decimal amounts and Stripe's integer (smallest-currency-unit) format.
/// </summary>
internal static class StripeAmountConverter
{
    /// <summary>
    ///     Converts a decimal amount to Stripe's integer format (smallest currency unit).
    ///     Uses the catalog scale with Stripe's API exceptions: ISK and UGX retain a
    ///     two-decimal representation for whole-unit charges, while MGA uses whole units.
    ///     See https://docs.stripe.com/currencies#special-cases.
    /// </summary>
    public static long ToStripeAmount(decimal amount, string currency)
    {
        if (RequiresWholeUnitAmount(currency) && amount != decimal.Truncate(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Stripe requires a whole-unit amount for this currency.");
        }

        var exponent = GetStripeMinorUnitExponent(currency);
        var scaled = exponent == 0 ? amount : amount * MinorUnitScale(exponent);
        return (long)decimal.Round(scaled, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     Converts a Stripe integer amount back to decimal using its API representation.
    /// </summary>
    public static decimal FromStripeAmount(long amount, string currency)
    {
        var exponent = GetStripeMinorUnitExponent(currency);
        var converted = exponent == 0 ? amount : amount / MinorUnitScale(exponent);
        if (RequiresWholeUnitAmount(currency) && converted != decimal.Truncate(converted))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Stripe's amount must represent whole units for this currency.");
        }

        return converted;
    }

    private static int GetStripeMinorUnitExponent(string currency)
    {
        if (string.Equals(currency, "ISK", StringComparison.OrdinalIgnoreCase) || string.Equals(currency, "UGX", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return string.Equals(currency, "MGA", StringComparison.OrdinalIgnoreCase) ? 0 : Iso4217CurrencyCatalog.GetMinorUnitExponent(currency);
    }

    private static bool RequiresWholeUnitAmount(string currency) =>
        string.Equals(currency, "ISK", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currency, "UGX", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currency, "MGA", StringComparison.OrdinalIgnoreCase);

    private static decimal MinorUnitScale(int exponent) { return decimal.Parse("1".PadRight(exponent + 1, '0'), System.Globalization.CultureInfo.InvariantCulture); }
}

/// <summary>
///     Maps between Stripe status strings and domain enums.
/// </summary>
internal static class StripeStatusMapper
{
    /// <summary>
    ///     Maps a Stripe payment-intent status to <see cref="PaymentStatus"/>.
    /// </summary>
    public static PaymentStatus MapPaymentStatus(string stripeStatus) =>
        stripeStatus switch
        {
            "succeeded" => PaymentStatus.Succeeded,
            "processing" => PaymentStatus.Processing,
            "requires_payment_method" => PaymentStatus.Failed,
            "requires_confirmation" => PaymentStatus.Pending,
            "requires_action" => PaymentStatus.RequiresAction,
            "canceled" => PaymentStatus.Cancelled,
            _ => PaymentStatus.Pending
        };

    /// <summary>
    ///     Maps a refund reason string to Stripe's accepted reason values.
    /// </summary>
    public static string? MapRefundReason(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
            return null;

        return reason.ToLowerInvariant() switch
        {
            "duplicate" => "duplicate",
            "fraudulent" => "fraudulent",
            "requested_by_customer" or "customer_request" => "requested_by_customer",
            _ => null
        };
    }
}

/// <summary>
///     Validates that payment requests carry Stripe object identifiers instead of raw card data.
/// </summary>
internal static class StripePaymentMethodIdentifier
{
    private static readonly Regex PaymentMethodIdPattern =
        new("^pm_[A-Za-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string ValidationMessage =
        "PaymentMethodId must be a Stripe payment method ID starting with 'pm_'. Raw card numbers are not accepted.";

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PaymentMethodIdPattern.IsMatch(value.Trim());
}
