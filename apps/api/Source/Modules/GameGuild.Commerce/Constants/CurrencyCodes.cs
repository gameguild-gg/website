namespace GameGuild.Commerce;

/// <summary>
///     Constants for currency codes (ISO 4217).
/// </summary>
/// <remarks>
///     Originally defined in the Billing module (<c>Commerce.Billing/Constants/PaymentProviders.cs</c>).
///     Relocated to the shared commerce contracts module (issue #416) so that the
///     Subscriptions module — which Billing depends on and therefore cannot reference — can
///     consume the same payment-supported ISO 4217 allowlist for plan currency validation.
///     The namespace (<see langword="GameGuild.Commerce" />) is unchanged, so all existing
///     Billing consumers keep compiling without using-statement changes.
/// </remarks>
public static class CurrencyCodes
{
    /// <summary>US Dollar</summary>
    public const string USD = "USD";

    /// <summary>Euro</summary>
    public const string EUR = "EUR";

    /// <summary>British Pound</summary>
    public const string GBP = "GBP";

    /// <summary>Japanese Yen</summary>
    public const string JPY = "JPY";

    /// <summary>Canadian Dollar</summary>
    public const string CAD = "CAD";

    /// <summary>Australian Dollar</summary>
    public const string AUD = "AUD";

    /// <summary>Default currency code</summary>
    public const string Default = USD;

    /// <summary>
    ///     All supported currency codes.
    /// </summary>
    public static readonly IReadOnlyList<string> Supported = new[]
    {
        USD, EUR, GBP, JPY, CAD, AUD
    };

    /// <summary>
    ///     Validates if a currency code is supported.
    /// </summary>
    /// <param name="currencyCode">The currency code to validate</param>
    /// <returns>True if the currency code is supported, false otherwise</returns>
    public static bool IsSupported(string? currencyCode) =>
        !string.IsNullOrEmpty(currencyCode) && Supported.Contains(currencyCode.ToUpperInvariant());
}
