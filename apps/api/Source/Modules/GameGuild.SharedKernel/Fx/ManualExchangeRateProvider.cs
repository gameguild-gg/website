namespace GameGuild;

/// <summary>
///     <see cref="IExchangeRateProvider"/> serving quotations that were entered — and are
///     owned — by platform operators. This is the seed source of truth for FX on the
///     platform: it works with zero external dependencies and stays correct for as long
///     as the entered quotations are, which is exactly the audit story the manual
///     entries need (no hidden external fetches behind a conversion).
/// </summary>
/// <remarks>
///     Only the exact registered direction is served. Registering USD→EUR does not make
///     EUR→USD conversions work: the reciprocal pair must be entered explicitly so every
///     served quotation is an auditable operator decision rather than derived arithmetic.
/// </remarks>
public sealed class ManualExchangeRateProvider : IExchangeRateProvider
{
    private readonly IReadOnlyDictionary<(string Base, string Quote), ExchangeRate> _rates;

    public ManualExchangeRateProvider(IEnumerable<ExchangeRate> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);

        var map = new Dictionary<(string Base, string Quote), ExchangeRate>();
        foreach (var rate in rates)
        {
            var key = (rate.BaseCurrency, rate.QuoteCurrency);
            if (map.TryGetValue(key, out var existing))
            {
                throw new ArgumentException(
                    $"Duplicate manual exchange rate for pair {rate.BaseCurrency}→{rate.QuoteCurrency} "
                    + $"(existing source '{existing.Source}' at {existing.AsOfUtc:O}, incoming source '{rate.Source}' at {rate.AsOfUtc:O}).");
            }

            map.Add(key, rate);
        }

        _rates = map;
    }

    /// <inheritdoc />
    public Task<ExchangeRate?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBase = baseCurrency?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedQuote = quoteCurrency?.Trim().ToUpperInvariant() ?? string.Empty;

        // Same-coded pair returns null on purpose: this provider has no quotation for it,
        // and callers that treat same-currency as identity must branch before asking.
        return Task.FromResult(_rates.TryGetValue((normalizedBase, normalizedQuote), out var rate) ? rate : null);
    }
}
