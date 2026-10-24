using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     European Central Bank reference-rate adapter (issue #412 spec B). Parses the ECB daily
///     euro reference rates (<c>eurofxref-daily.xml</c>, no credentials required) into an
///     EUR-base grid. Historical grids are served from the 90-day history file
///     (<c>eurofxref-hist-90d.xml</c>) when an <c>asOf</c> date is requested; dates outside
///     that window fail closed with <see cref="FxRateProviderUnavailableException"/> rather
///     than substituting latest data.
/// </summary>
public sealed class EcbFxRateProvider(HttpClient httpClient, IOptions<FxSyncOptions> options, ILogger<EcbFxRateProvider> logger) : IFxRateProvider
{
    /// <summary>Provider identifier persisted on snapshots and sync runs.</summary>
    public string Name { get => "ecb"; }

    private FxSyncOptions.EcbOptions Settings { get => options.Value.Ecb; }

    /// <inheritdoc />
    public async Task<FxRateSet> GetRatesAsync(DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default)
    {
        var settings = Settings;
        var baseCurrency = settings.BaseCurrency.Trim().ToUpperInvariant();
        if (baseCurrency != "EUR")
        {
            throw new FxRateProviderUnavailableException(Name, "ECB reference rates are always quoted against EUR.");
        }

        var path = asOfUtc is null ? "eurofxref/eurofxref-daily.xml" : "eurofxref/eurofxref-hist-90d.xml";
        var url = $"{settings.BaseUrl.TrimEnd('/')}/{path}";

        using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("ECB request to {Path} failed with {Status}.", path, (int)response.StatusCode);
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} from ECB.", null, response.StatusCode);
            }

            throw new FxRateProviderUnavailableException(Name, $"HTTP {(int)response.StatusCode} from {path}.");
        }

        return Parse(raw, baseCurrency, asOfUtc?.UtcDateTime.Date);
    }

    /// <summary>
    ///     Parses the ECB Cube XML into an <see cref="FxRateSet"/>. When
    ///     <paramref name="requestedDate"/> is set, the grid for that date is extracted from the
    ///     history document (latest matching date at or before it).
    /// </summary>
    /// <exception cref="FxRateProviderUnavailableException">Thrown when the payload is malformed, empty or lacks the requested date.</exception>
    internal static FxRateSet Parse(string raw, string baseCurrency, DateTime? requestedDate)
    {
        if (baseCurrency != "EUR")
        {
            throw new FxRateProviderUnavailableException("ecb", "ECB reference rates are always quoted against EUR.");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(raw);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new FxRateProviderUnavailableException("ecb", "Response was not valid XML.", exception);
        }

        // The ECB files mix namespaces (gesmes envelope + eurofxref default namespace); match
        // Cube elements by local name so both the daily and history layouts parse the same way.
        var dayCubes = document.Descendants()
            .Where(element => element.Name.LocalName == "Cube" && element.Attribute("time") is not null)
            .Select(element => new
            {
                Time = DateTime.TryParseExact(element.Attribute("time")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate)
                    ? parsedDate : throw new FxRateProviderUnavailableException("ecb", "Response contained an invalid effective date."),
                Rates = element.Elements()
                    .Where(rate => rate.Name.LocalName == "Cube" && rate.Attribute("currency") is not null && rate.Attribute("rate") is not null)
                    .Select(rate => new
                    {
                        Currency = rate.Attribute("currency")!.Value.ToUpperInvariant(),
                        Rate = decimal.TryParse(rate.Attribute("rate")!.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedRate)
                            ? parsedRate : throw new FxRateProviderUnavailableException("ecb", "Response contained an invalid rate.")
                    })
                    .ToList()
            })
            .Where(day => day.Rates.Count > 0)
            .ToList();

        if (dayCubes.Count == 0)
        {
            throw new FxRateProviderUnavailableException("ecb", "Response contained no rate cubes.");
        }

        var selected = requestedDate is null
            ? dayCubes.OrderByDescending(day => day.Time).First()
            : dayCubes.Where(day => day.Time.Date <= requestedDate.Value.Date).OrderByDescending(day => day.Time).FirstOrDefault()
              ?? throw new FxRateProviderUnavailableException(
                  "ecb",
                  $"No ECB reference rates exist at or before {requestedDate.Value:yyyy-MM-dd} within the available history window.");

        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var rate in selected.Rates)
        {
            if (rate.Rate > 0m && rate.Currency != baseCurrency && Iso4217CurrencyCatalog.Exists(rate.Currency))
            {
                rates[rate.Currency] = rate.Rate;
            }
        }

        if (rates.Count == 0)
        {
            throw new FxRateProviderUnavailableException("ecb", $"Rate cube for {selected.Time:yyyy-MM-dd} was empty.");
        }

        return new FxRateSet(
            "ecb",
            baseCurrency,
            rates,
            new DateTimeOffset(selected.Time.Date, TimeSpan.Zero), // The feed supplies a date, not a publication instant.
            selected.Time.Date,
            raw,
            "application/xml");
    }
}
