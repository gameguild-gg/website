namespace GameGuild.Commerce.Billing;

/// <summary>
///     Attributes a subscription external identifier to the external billing provider
///     that issued it, using the providers' documented identifier conventions.
///     Used by the report-only provider-migration dry-run (issue #397). Fail-closed:
///     identifiers that cannot be attributed with confidence return false rather than
///     being guessed (Google purchase tokens have no stable public prefix, so they are
///     surfaced for manual review instead).
/// </summary>
public static class BillingProviderExternalIdRecognizer
{
    /// <summary>
    ///     Attempts to attribute an external subscription identifier to a provider key
    ///     from <see cref="PaymentProviders"/>.
    /// </summary>
    public static bool TryRecognize(string? externalId, out string providerKey)
    {
        providerKey = string.Empty;

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return false;
        }

        // Stripe subscription object identifiers use the documented "sub_" prefix.
        if (externalId.StartsWith("sub_", StringComparison.Ordinal))
        {
            providerKey = PaymentProviders.Stripe;
            return true;
        }

        // PayPal subscription identifiers use the documented "I-" prefix.
        if (externalId.StartsWith("I-", StringComparison.Ordinal))
        {
            providerKey = PaymentProviders.PayPal;
            return true;
        }

        // App Store original transaction identifiers are numeric.
        if (externalId.Length > 0 && externalId.All(char.IsDigit))
        {
            providerKey = PaymentProviders.AppleAppStore;
            return true;
        }

        return false;
    }
}
