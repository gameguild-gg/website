namespace GameGuild.Commerce.Payments;

/// <summary>
///     <see cref="IExchangeRateProvider"/> for the conversion path (issue #412 spec B): answers
///     single-pair quotations from the <see cref="FxRuntimeRateSource"/> (persisted snapshots +
///     operator overrides) behind the TTL <see cref="IFxRateCache"/>. Replaces the empty manual
///     rate set previously registered by <c>PaymentsModule</c>: with no provider configured and
///     no overrides entered it serves no cross-currency quotations — conversions still throw —
///     but operator overrides and synced market grids now flow into the same conversion seam.
///     Same-code pairs return null by contract (<see cref="ManualExchangeRateProvider"/> does the
///     same): callers branch on identity before asking.
/// </summary>
public sealed class PersistedFxRateProvider(IFxRateCache cache, FxRuntimeRateSource runtimeSource) : IExchangeRateProvider
{
    /// <inheritdoc />
    public async Task<ExchangeRate?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken = default)
    {
        var normalizedBase = baseCurrency?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedQuote = quoteCurrency?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalizedBase.Length != 3 || normalizedQuote.Length != 3 || normalizedBase == normalizedQuote)
        {
            return null;
        }

        await runtimeSource.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        // Overrides carry their own expiry and are deliberately never placed in the TTL
        // quotation cache. Check them first so cached market rates cannot hide a local edit.
        var overridden = runtimeSource.TryGetOverride(normalizedBase, normalizedQuote, SystemClock.UtcNow);
        if (overridden is not null)
        {
            return overridden;
        }

        var cached = await cache.GetAsync(normalizedBase, normalizedQuote, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.Source != "manual-override")
        {
            return cached;
        }

        var rate = runtimeSource.TryGetRate(normalizedBase, normalizedQuote, SystemClock.UtcNow);
        if (rate is null)
        {
            return null; // misses are deliberately not cached: a fresh sync must become visible
        }

        if (rate.Source != "manual-override")
        {
            await cache.SetAsync(rate, cancellationToken).ConfigureAwait(false);
        }
        return rate;
    }
}
