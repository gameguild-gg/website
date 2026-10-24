using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     FX rate sync configuration (issue #412 spec B). Bound from the <c>FxRates</c> section.
///     Fail-closed by default: <see cref="Provider"/> is <c>"none"</c>, so unless a deployment
///     deliberately selects a provider and supplies its credentials, no rates are fetched,
///     no HTTP calls happen and cross-currency conversion keeps throwing — same posture as the
///     empty manual rate set this replaces, but now operator-controlled.
/// </summary>
public sealed class FxSyncOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FxRates";

    /// <summary>Supported provider selectors.</summary>
    public const string ProviderNone = "none";

    /// <summary>OpenExchangeRates provider selector.</summary>
    public const string ProviderOpenExchangeRates = "openexchangerates";

    /// <summary>European Central Bank provider selector.</summary>
    public const string ProviderEcb = "ecb";

    /// <summary>Primary rate provider: "none" (default, fail-closed), "openexchangerates" or "ecb".</summary>
    public string Provider { get; set; } = ProviderNone;

    /// <summary>
    ///     Optional fallback provider consulted when the primary fails after retries
    ///     (e.g. "ecb"). Default "none" = no fallback.
    /// </summary>
    public string FallbackProvider { get; set; } = ProviderNone;

    /// <summary>Whether the scheduled sync worker runs at all. Independent of <see cref="Provider"/>:
    /// manual sync can be triggered through the admin endpoint with the worker disabled.</summary>
    public bool WorkerEnabled { get; set; }

    /// <summary>Daily sync time (UTC) as 24h "HH:mm". The worker fires once per day at this time.</summary>
    public string SyncTimeUtc { get; set; } = "00:00";

    /// <summary>
    ///     Optional fixed polling interval (minutes) overriding the daily schedule — for
    ///     development and tests. 0 (default) uses the daily <see cref="SyncTimeUtc"/> schedule.
    /// </summary>
    public int IntervalMinutes { get; set; }

    /// <summary>Cache TTL for conversion-path rate lookups (spec: 1 hour).</summary>
    public int CacheTtlMinutes { get; set; } = 60;

    /// <summary>Maximum attempts per provider fetch (retry with exponential backoff). At least 1.</summary>
    public int RetryMaxAttempts { get; set; } = 3;

    /// <summary>Base delay for the exponential backoff between retries (doubles per attempt).</summary>
    public int RetryBaseDelaySeconds { get; set; } = 2;

    /// <summary>Operators are alerted after this many consecutive failed runs. At least 1.</summary>
    public int AlertAfterConsecutiveFailures { get; set; } = 3;

    /// <summary>Newest persisted snapshot older than this many days counts as stale and raises a staleness alert.</summary>
    public int StalenessAlertDays { get; set; } = 2;

    /// <summary>OpenExchangeRates adapter settings.</summary>
    public OpenExchangeRatesOptions OpenExchangeRates { get; set; } = new();

    /// <summary>ECB adapter settings.</summary>
    public EcbOptions Ecb { get; set; } = new();

    /// <summary>OpenExchangeRates adapter settings.</summary>
    public sealed class OpenExchangeRatesOptions
    {
        /// <summary>App ID (API key). Supplied through configuration/secret store — never persisted by this module.</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>API base URL (overridable for proxies/tests).</summary>
        public string BaseUrl { get; set; } = "https://openexchangerates.org/api";

        /// <summary>Grid base currency (free plans only support USD).</summary>
        public string BaseCurrency { get; set; } = "USD";

        /// <summary>Request timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>ECB adapter settings.</summary>
    public sealed class EcbOptions
    {
        /// <summary>Reference-rate base URL (overridable for proxies/tests). The ECB feed needs no credentials.</summary>
        public string BaseUrl { get; set; } = "https://www.ecb.europa.eu/stats";

        /// <summary>Grid base currency; the ECB feed always quotes the euro.</summary>
        public string BaseCurrency { get; set; } = "EUR";

        /// <summary>Request timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}

/// <summary>Validates <see cref="FxSyncOptions"/> at startup (fail-closed on misconfiguration).</summary>
public sealed class FxSyncOptionsValidator : IValidateOptions<FxSyncOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, FxSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!IsKnownProvider(options.Provider))
        {
            failures.Add($"FxRates:Provider must be one of 'none', 'openexchangerates', 'ecb' (got '{options.Provider}').");
        }

        if (!string.IsNullOrWhiteSpace(options.FallbackProvider) && !IsKnownProvider(options.FallbackProvider))
        {
            failures.Add($"FxRates:FallbackProvider must be 'none', 'openexchangerates' or 'ecb' (got '{options.FallbackProvider}').");
        }

        if (options.Provider == FxSyncOptions.ProviderNone && options.FallbackProvider != FxSyncOptions.ProviderNone
            && !string.IsNullOrWhiteSpace(options.FallbackProvider))
        {
            failures.Add("FxRates:FallbackProvider cannot be set while FxRates:Provider is 'none'.");
        }

        var usesOpenExchangeRates = options.Provider == FxSyncOptions.ProviderOpenExchangeRates
            || options.FallbackProvider == FxSyncOptions.ProviderOpenExchangeRates;
        var usesEcb = options.Provider == FxSyncOptions.ProviderEcb || options.FallbackProvider == FxSyncOptions.ProviderEcb;

        if (usesOpenExchangeRates && string.IsNullOrWhiteSpace(options.OpenExchangeRates.AppId))
        {
            failures.Add("FxRates:OpenExchangeRates:AppId is required when FxRates:Provider is 'openexchangerates'.");
        }

        if (usesOpenExchangeRates && !IsSecureUrl(options.OpenExchangeRates.BaseUrl))
        {
            failures.Add("FxRates:OpenExchangeRates:BaseUrl must use HTTPS (HTTP is allowed only for loopback tests).");
        }

        if (usesOpenExchangeRates && !Iso4217CurrencyCatalog.Exists(options.OpenExchangeRates.BaseCurrency))
        {
            failures.Add("FxRates:OpenExchangeRates:BaseCurrency must be a known ISO currency.");
        }

        if (usesEcb && !IsSecureUrl(options.Ecb.BaseUrl))
        {
            failures.Add("FxRates:Ecb:BaseUrl must use HTTPS (HTTP is allowed only for loopback tests).");
        }

        if (usesEcb && !string.Equals(options.Ecb.BaseCurrency, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("FxRates:Ecb:BaseCurrency must be EUR because ECB reference rates use the euro as their base.");
        }

        if (options.CacheTtlMinutes < 0)
        {
            failures.Add("FxRates:CacheTtlMinutes must be >= 0.");
        }

        if (options.RetryMaxAttempts < 1)
        {
            failures.Add("FxRates:RetryMaxAttempts must be >= 1.");
        }

        if (options.RetryBaseDelaySeconds < 0)
        {
            failures.Add("FxRates:RetryBaseDelaySeconds must be >= 0.");
        }

        if (options.AlertAfterConsecutiveFailures < 1)
        {
            failures.Add("FxRates:AlertAfterConsecutiveFailures must be >= 1.");
        }

        if (options.StalenessAlertDays < 1)
        {
            failures.Add("FxRates:StalenessAlertDays must be >= 1.");
        }

        if (options.IntervalMinutes < 0)
        {
            failures.Add("FxRates:IntervalMinutes must be >= 0.");
        }

        if (!TimeSpan.TryParse(options.SyncTimeUtc, System.Globalization.CultureInfo.InvariantCulture, out var syncTime)
            || syncTime < TimeSpan.Zero || syncTime >= TimeSpan.FromDays(1))
        {
            failures.Add($"FxRates:SyncTimeUtc must be a 24h 'HH:mm' time (got '{options.SyncTimeUtc}').");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;

        static bool IsKnownProvider(string value)
        {
            return value is FxSyncOptions.ProviderNone
                or FxSyncOptions.ProviderOpenExchangeRates
                or FxSyncOptions.ProviderEcb;
        }

        static bool IsSecureUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
    }
}
