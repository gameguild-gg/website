namespace GameGuild;

/// <summary>
///     An immutable FX quotation: how many units of <see cref="QuoteCurrency"/> one unit of
///     <see cref="BaseCurrency"/> was worth at <see cref="AsOfUtc"/>, according to <see cref="Source"/>.
///     Multiply a base-currency amount by <see cref="Rate"/> to express it in the quote currency.
///     Pure platform reference data — no provider clients, no persistence, no domain vocabulary.
/// </summary>
public sealed record ExchangeRate
{
    /// <param name="baseCurrency">ISO 4217 code the rate converts from (e.g. "USD" in USD→EUR 0.92); any casing.</param>
    /// <param name="quoteCurrency">ISO 4217 code the rate converts to (e.g. "EUR" in USD→EUR 0.92); any casing.</param>
    /// <param name="rate">Quote units per one base unit; strictly positive.</param>
    /// <param name="asOfUtc">Moment (UTC) the quotation is valid for. Historical rates keep their original stamp.</param>
    /// <param name="source">Provenance of the quotation (e.g. "manual", "override", "openexchangerates"). Required for audit.</param>
    public ExchangeRate(
        string baseCurrency,
        string quoteCurrency,
        decimal rate,
        DateTimeOffset asOfUtc,
        string source)
    {
        if (string.IsNullOrWhiteSpace(baseCurrency) || baseCurrency.Trim().Length != 3)
        {
            throw new ArgumentException("Base currency must be a three-letter ISO 4217 code.", nameof(baseCurrency));
        }

        if (string.IsNullOrWhiteSpace(quoteCurrency) || quoteCurrency.Trim().Length != 3)
        {
            throw new ArgumentException("Quote currency must be a three-letter ISO 4217 code.", nameof(quoteCurrency));
        }

        var normalizedBase = baseCurrency.Trim().ToUpperInvariant();
        var normalizedQuote = quoteCurrency.Trim().ToUpperInvariant();

        if (normalizedBase == normalizedQuote)
        {
            throw new ArgumentException(
                $"Base and quote currency must differ ('{normalizedBase}' → '{normalizedQuote}' is not an FX pair).");
        }

        if (rate <= 0)
        {
            throw new ArgumentException("Exchange rate must be strictly positive.", nameof(rate));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Exchange rate source is required for audit provenance.", nameof(source));
        }

        BaseCurrency = normalizedBase;
        QuoteCurrency = normalizedQuote;
        Rate = rate;
        AsOfUtc = asOfUtc;
        Source = source.Trim();
    }

    /// <summary>Currency the rate converts from.</summary>
    public string BaseCurrency { get; }

    /// <summary>Currency the rate converts to.</summary>
    public string QuoteCurrency { get; }

    /// <summary>Quote units per one base unit.</summary>
    public decimal Rate { get; }

    /// <summary>Moment (UTC) the quotation is valid for.</summary>
    public DateTimeOffset AsOfUtc { get; }

    /// <summary>Provenance of the quotation (manual entry, fixed override, external provider, ...).</summary>
    public string Source { get; }

    /// <summary>
    ///     Whether this quotation answers conversions from <paramref name="from"/> to <paramref name="to"/>
    ///     (case-insensitive). A quotation only ever covers its own direction; the reciprocal
    ///     direction requires its own entry unless the provider exposes one.
    /// </summary>
    public bool Matches(string from, string to)
    {
        return string.Equals(from?.Trim(), BaseCurrency, StringComparison.OrdinalIgnoreCase)
            && string.Equals(to?.Trim(), QuoteCurrency, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Builds the reciprocal quotation (quote→base) with rate <c>1 / Rate</c> rounded to
    ///     12 significant decimals to keep decimal arithmetic bounded. Mostly for provider
    ///     bookkeeping; converters should query the direction they need.
    /// </summary>
    public ExchangeRate Invert()
    {
        return new ExchangeRate(
            QuoteCurrency,
            BaseCurrency,
            decimal.Round(1m / Rate, 12, MidpointRounding.AwayFromZero),
            AsOfUtc,
            Source);
    }
}
