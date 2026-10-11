namespace GameGuild;

/// <summary>
///     <see cref="IExchangeRateProvider"/> decorator that pins selected pairs to fixed
///     quotations before delegating everything else to the wrapped provider. This is the
///     override seam for market-independent pricing decisions (e.g. a commercial EUR rate
///     that must hold for a campaign even while the market moves).
/// </summary>
/// <remarks>
///     Overrides win unconditionally over the inner provider for the exact direction they
///     are registered for — including over fresher market data. That is the point of a
///     fixed override; use it deliberately. The reciprocal direction is NOT implied:
///     pin both directions when conversions run both ways.
/// </remarks>
public sealed class FixedOverrideExchangeRateProvider : IExchangeRateProvider
{
    private readonly ManualExchangeRateProvider _overrides;
    private readonly IExchangeRateProvider _inner;

    public FixedOverrideExchangeRateProvider(IEnumerable<ExchangeRate> overrides, IExchangeRateProvider inner)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(inner);

        _overrides = new ManualExchangeRateProvider(overrides);
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<ExchangeRate?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken = default)
    {
        var pinned = await _overrides.GetRateAsync(baseCurrency, quoteCurrency, cancellationToken).ConfigureAwait(false);
        if (pinned is not null)
        {
            return pinned;
        }

        return await _inner.GetRateAsync(baseCurrency, quoteCurrency, cancellationToken).ConfigureAwait(false);
    }
}
