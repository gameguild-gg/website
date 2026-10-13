using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Configuration-driven gateway routing (issue #413), bound from <c>Payments:GatewayRouting</c>.
///     Each entry governs one registered gateway: whether it participates in routing, its priority
///     (lower value wins), and its eligibility filters. An empty list keeps the fail-closed default
///     disposition: every gateway registered in DI that reports <c>IsEnabled</c> participates, ordered
///     by the well-known provider priorities (stripe first, then paypal, then everything else).
/// </summary>
public sealed class GatewayRoutingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:GatewayRouting";

    /// <summary>
    ///     Per-gateway routing entries. Absent entries fall back to the adapter's own
    ///     <c>IsEnabled</c> disposition with the well-known default priority.
    /// </summary>
    public List<GatewayRouteEntry> Gateways { get; set; } = [];

    /// <summary>
    ///     Resolves the configured route entry for a provider, if one exists. Provider
    ///     identifiers are compared case-insensitively.
    /// </summary>
    public GatewayRouteEntry? FindEntry(string providerId) =>
        Gateways.FirstOrDefault(entry =>
            string.Equals(entry.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Resolves the effective priority for a provider: the configured value, or the
    ///     well-known default (stripe 100, paypal 200, unknown adapters 1000).
    /// </summary>
    public static int ResolvePriority(string providerId, GatewayRouteEntry? entry) =>
        entry?.Priority ?? providerId.ToLowerInvariant() switch
        {
            GatewayProviderIds.Stripe => 100,
            GatewayProviderIds.PayPal => 200,
            _ => 1000
        };
}

/// <summary>
///     Well-known payment gateway provider identifiers used inside the Payments module
///     (mirrors the Commerce.Billing <c>PaymentProviders</c> constants without a module dependency).
/// </summary>
public static class GatewayProviderIds
{
    /// <summary>Stripe provider identifier.</summary>
    public const string Stripe = "stripe";

    /// <summary>PayPal provider identifier.</summary>
    public const string PayPal = "paypal";
}

/// <summary>
///     One gateway's routing configuration. Filter lists are opt-in restrictions:
///     an empty list means "no restriction" (every currency, country or payment
///     method is eligible); a non-empty list must contain the request's value.
/// </summary>
public sealed class GatewayRouteEntry
{
    /// <summary>Provider identifier of the registered gateway (stripe, paypal, ...).</summary>
    [Required]
    [MaxLength(50)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the gateway participates in routing. When an entry exists this flag and the
    ///     adapter's own <c>IsEnabled</c> must both be true (fail-closed conjunction).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Selection priority; lower values are tried first. Ties fall back to the
    ///     registration order of the adapters.
    /// </summary>
    [Range(1, 10000)]
    public int Priority { get; set; } = 100;

    /// <summary>ISO 4217 currency codes this gateway accepts; empty means all.</summary>
    public List<string> SupportedCurrencies { get; set; } = [];

    /// <summary>ISO 3166-1 alpha-2 billing countries this gateway accepts; empty means all.</summary>
    public List<string> SupportedCountries { get; set; } = [];

    /// <summary>Payment method types this gateway accepts (card, paypal, ...); empty means all.</summary>
    public List<string> SupportedPaymentMethods { get; set; } = [];

    /// <summary>Inclusive minimum amount (major currency units) this gateway accepts; null means no bound.</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>Inclusive maximum amount (major currency units) this gateway accepts; null means no bound.</summary>
    public decimal? MaxAmount { get; set; }
}

/// <summary>
///     Startup validation for <see cref="GatewayRoutingOptions" />: rejects duplicate provider
///     entries, unknown empty provider identifiers, and inverted amount bounds.
/// </summary>
public sealed class GatewayRoutingOptionsValidator : IValidateOptions<GatewayRoutingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GatewayRoutingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var seenProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in options.Gateways)
        {
            if (string.IsNullOrWhiteSpace(entry.ProviderId))
            {
                failures.Add("Gateway routing entries must declare a provider id.");
                continue;
            }

            if (!seenProviders.Add(entry.ProviderId))
            {
                failures.Add($"Duplicate gateway routing entry for provider '{entry.ProviderId}'.");
            }

            if (entry.Priority is < 1 or > 10000)
            {
                failures.Add($"Provider '{entry.ProviderId}': Priority must be between 1 and 10000.");
            }

            if (entry.MinAmount.HasValue && entry.MaxAmount.HasValue && entry.MinAmount.Value > entry.MaxAmount.Value)
            {
                failures.Add($"Provider '{entry.ProviderId}': MinAmount cannot exceed MaxAmount.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
