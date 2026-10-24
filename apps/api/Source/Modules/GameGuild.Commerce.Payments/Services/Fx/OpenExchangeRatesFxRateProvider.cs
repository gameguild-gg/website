using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     OpenExchangeRates adapter (issue #412 spec B). Fetches the latest (or historical)
///     USD-base rate grid from <c>api/latest.json</c> / <c>api/historical/{date}.json</c>
///     through a typed <see cref="HttpClient"/> registered by
///     <see cref="FxRateSyncServiceCollectionExtensions"/>. Config-gated: only registered when
///     <c>FxRates:Provider</c> (or the fallback) selects it, so the default deployment dials
///     nothing. The app ID travels as a query parameter per the provider's API contract; it is
///     supplied through configuration/secrets and never persisted by this module.
/// </summary>
public sealed class OpenExchangeRatesFxRateProvider(HttpClient httpClient, IOptions<FxSyncOptions> options, ILogger<OpenExchangeRatesFxRateProvider> logger) : IFxRateProvider
{
    /// <summary>Provider identifier persisted on snapshots and sync runs.</summary>
    public string Name { get => "openexchangerates"; }

    private FxSyncOptions.OpenExchangeRatesOptions Settings { get => options.Value.OpenExchangeRates; }

    /// <inheritdoc />
    public async Task<FxRateSet> GetRatesAsync(DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        if (string.IsNullOrWhiteSpace(settings.AppId))
        {
            throw new FxRateProviderUnavailableException(Name, "AppId is not configured (FxRates:OpenExchangeRates:AppId).");
        }

        var baseCurrency = Normalize(settings.BaseCurrency, "USD");
        var path = asOfUtc is null
            ? "latest.json"
            : $"historical/{asOfUtc.Value.UtcDateTime:yyyy-MM-dd}.json";
        var url = $"{settings.BaseUrl.TrimEnd('/')}/{path}?app_id={Uri.EscapeDataString(settings.AppId)}&base={baseCurrency}";

        using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenExchangeRates request to {Path} failed with {Status}.", path, (int)response.StatusCode);
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} from OpenExchangeRates.", null, response.StatusCode);
            }

            throw new FxRateProviderUnavailableException(Name, $"HTTP {(int)response.StatusCode} from {path}.");
        }

        return Parse(raw, baseCurrency);
    }

    /// <summary>Parses an OpenExchangeRates response body into an <see cref="FxRateSet"/>.</summary>
    /// <exception cref="FxRateProviderUnavailableException">Thrown when the payload is malformed or empty.</exception>
    internal static FxRateSet Parse(string raw, string baseCurrency)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException exception)
        {
            throw new FxRateProviderUnavailableException("openexchangerates", "Response was not valid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("rates", out var ratesElement) || ratesElement.ValueKind != JsonValueKind.Object)
            {
                throw new FxRateProviderUnavailableException("openexchangerates", "Response contained no 'rates' object.");
            }

            if (!root.TryGetProperty("base", out var baseElement) || baseElement.ValueKind != JsonValueKind.String
                || !string.Equals(baseElement.GetString(), baseCurrency, StringComparison.OrdinalIgnoreCase)
                || !Iso4217CurrencyCatalog.Exists(baseCurrency))
            {
                throw new FxRateProviderUnavailableException("openexchangerates", "Response base currency did not match the requested ISO currency.");
            }

            if (!root.TryGetProperty("timestamp", out var timestampElement)
                || timestampElement.ValueKind != JsonValueKind.Number || !timestampElement.TryGetInt64(out var seconds))
            {
                throw new FxRateProviderUnavailableException("openexchangerates", "Response contained no publication timestamp.");
            }

            DateTime timestamp;
            try
            {
                timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new FxRateProviderUnavailableException("openexchangerates", "Response publication timestamp was invalid.", exception);
            }

            var effectiveDate = root.TryGetProperty("date", out var dateElement)
                                && dateElement.ValueKind == JsonValueKind.String
                                && DateTime.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate)
                ? parsedDate.Date
                : timestamp.Date;

            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var entry in ratesElement.EnumerateObject())
            {
                var code = entry.Name.ToUpperInvariant();
                if (code == baseCurrency || !Iso4217CurrencyCatalog.Exists(code))
                {
                    continue; // Providers include base:1; it is identity, not a persisted FX pair.
                }

                if (entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetDecimal(out var rate))
                {
                    continue;
                }

                if (rate <= 0m)
                {
                    continue;
                }

                rates[code] = rate;
            }

            if (rates.Count == 0)
            {
                throw new FxRateProviderUnavailableException("openexchangerates", "Response 'rates' object was empty.");
            }

            return new FxRateSet(
                "openexchangerates",
                baseCurrency,
                rates,
                new DateTimeOffset(timestamp, TimeSpan.Zero),
                effectiveDate,
                raw,
                "application/json");
        }
    }

    private static string Normalize(string value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length == 3 ? trimmed.ToUpperInvariant() : fallback;
    }
}
