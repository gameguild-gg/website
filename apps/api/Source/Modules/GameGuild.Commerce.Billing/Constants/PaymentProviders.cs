namespace GameGuild.Commerce;

/// <summary>
///     Constants for payment provider identifiers.
///     Use these instead of magic strings throughout the commerce modules.
/// </summary>
public static class PaymentProviders
{
    /// <summary>Stripe payment provider</summary>
    public const string Stripe = "stripe";

    /// <summary>PayPal payment provider</summary>
    public const string PayPal = "paypal";

    /// <summary>Apple Pay payment provider (in-app purchases)</summary>
    public const string ApplePay = "applepay";

    /// <summary>Apple App Store (server-to-server notifications)</summary>
    public const string AppleAppStore = "apple_app_store";

    /// <summary>Google Pay payment provider</summary>
    public const string GooglePay = "googlepay";

    /// <summary>Google Play Store (server-to-server notifications)</summary>
    public const string GooglePlayStore = "google_play_store";

    /// <summary>
    ///     All supported payment providers.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Stripe,
        PayPal,
        ApplePay,
        AppleAppStore,
        GooglePay,
        GooglePlayStore
    };

    /// <summary>
    ///     Validates if a provider string is a supported payment provider.
    /// </summary>
    /// <param name="provider">The provider string to validate</param>
    /// <returns>True if the provider is supported, false otherwise</returns>
    public static bool IsSupported(string? provider) =>
        !string.IsNullOrEmpty(provider) && All.Contains(provider.ToLowerInvariant());

    /// <summary>
    ///     Normalizes a provider string to lowercase.
    /// </summary>
    /// <param name="provider">The provider string to normalize</param>
    /// <returns>The normalized provider string</returns>
    public static string Normalize(string provider) =>
        provider.ToLowerInvariant();
}

// CurrencyCodes (ISO 4217 allowlist) moved to the shared commerce contracts module
// (GameGuild.Commerce/Constants/CurrencyCodes.cs, issue #416) so the Subscriptions module
// can validate plan currencies against the same payment-supported allowlist. The namespace
// (GameGuild.Commerce) is unchanged; Billing consumers keep compiling without changes.
