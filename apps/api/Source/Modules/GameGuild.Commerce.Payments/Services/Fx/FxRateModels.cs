namespace GameGuild.Commerce.Payments;

/// <summary>
///     One complete rate grid fetched from an FX provider (issue #412 spec B): how many units
///     of each quote currency one unit of <see cref="BaseCurrency"/> was worth at
/// <see cref="AsOfUtc"/>, plus the verbatim provider response for audit storage.
/// </summary>
/// <param name="ProviderName">Provider identifier (e.g. "openexchangerates", "ecb").</param>
/// <param name="BaseCurrency">Grid base currency (e.g. "USD" for OpenExchangeRates, "EUR" for ECB).</param>
/// <param name="Rates">Quote currency → quote units per one base unit. Strictly positive, upper-case keys.</param>
/// <param name="AsOfUtc">Moment (UTC) the provider stamped the data with.</param>
/// <param name="EffectiveDateUtc">Effective (data) date the rates are valid for, UTC, day-truncated.</param>
/// <param name="RawResponse">Verbatim provider response body.</param>
/// <param name="RawResponseContentType">Content type of <paramref name="RawResponse"/>.</param>
public sealed record FxRateSet(
    string ProviderName,
    string BaseCurrency,
    IReadOnlyDictionary<string, decimal> Rates,
    DateTimeOffset AsOfUtc,
    DateTime EffectiveDateUtc,
    string RawResponse,
    string RawResponseContentType)
{
    /// <summary>Source label persisted with every snapshot derived from this set.</summary>
    public string Source { get => "latest"; }
}

/// <summary>
///     Thrown by <see cref="IFxRateProvider"/> implementations when no rate set can be
///     produced (provider disabled, missing credentials, transport failure after retries).
///     The sync service treats this as a failed run — it never fabricates fallback rates.
/// </summary>
public sealed class FxRateProviderUnavailableException(string providerName, string message, Exception? innerException = null)
    : Exception($"{providerName}: {message}", innerException)
{
    /// <summary>Provider that could not produce a rate set.</summary>
    public string ProviderName { get; } = providerName;
}
