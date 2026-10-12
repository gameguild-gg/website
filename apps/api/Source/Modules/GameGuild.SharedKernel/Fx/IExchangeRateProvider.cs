namespace GameGuild;

/// <summary>
///     Source of FX quotations for cross-currency <see cref="Money"/> conversion.
///     Implementations are stacked: a manual base provider, optionally wrapped by a
///     <see cref="FixedOverrideExchangeRateProvider"/> that pins selected pairs.
/// </summary>
/// <remarks>
///     Contract (fail-closed): a provider that has no quotation for a pair returns
///     <c>null</c>. It must NEVER fabricate a rate — in particular it must never return
///     an implicit 1:1 rate for an unknown or same-coded pair — and it must never throw
///     for a merely unknown pair; infrastructure failures surface as exceptions instead.
/// </remarks>
public interface IExchangeRateProvider
{
    /// <summary>
    ///     Gets the quotation converting one unit of <paramref name="baseCurrency"/> into
    ///     <paramref name="quoteCurrency"/> (case-insensitive, direction matters).
    /// </summary>
    /// <param name="baseCurrency">ISO 4217 code to convert from.</param>
    /// <param name="quoteCurrency">ISO 4217 code to convert to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The best available quotation, or <c>null</c> when this provider has none for the pair.</returns>
    Task<ExchangeRate?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken = default);
}
