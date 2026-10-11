namespace GameGuild;

/// <summary>
///     Converts <see cref="Money"/> across currencies using an <see cref="IExchangeRateProvider"/>,
///     fail-closed: when no quotation is available for the requested pair the conversion
///     throws instead of guessing. There is deliberately NO implicit 1:1 fallback — an
///     unavailable rate is an error the caller must handle, never a silent identity
///     conversion that would book invented money.
/// </summary>
public sealed class CurrencyConversionService
{
    /// <summary>Rule identifier used when a conversion fails for lack of a quotation.</summary>
    public const string MissingRateRule = "ExchangeRateUnavailable";

    private readonly IExchangeRateProvider _rates;

    public CurrencyConversionService(IExchangeRateProvider rates)
    {
        ArgumentNullException.ThrowIfNull(rates);

        _rates = rates;
    }

    /// <summary>
    ///     Converts <paramref name="amount"/> into <paramref name="targetCurrency"/>.
    ///     Same-currency requests return the amount unchanged — identity is exact arithmetic,
    ///     not an FX assumption, so it needs no quotation. Cross-currency requests require a
    ///     quotation for the exact <c>amount.Currency → targetCurrency</c> direction; without
    ///     one a <see cref="BusinessRuleViolationException"/> (<see cref="MissingRateRule"/>) is thrown.
    ///     The converted amount is rounded to the target currency's ISO 4217 minor-unit exponent
    ///     with the platform's historical AwayFromZero strategy.
    /// </summary>
    /// <param name="amount">Money to convert; its currency is the conversion base.</param>
    /// <param name="targetCurrency">ISO 4217 code to convert into (any casing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="targetCurrency"/> is empty or malformed.</exception>
    /// <exception cref="BusinessRuleViolationException">
    ///     Thrown with rule <see cref="MissingRateRule"/> when the provider has no quotation for the pair.
    /// </exception>
    public async Task<Money> ConvertAsync(
        Money amount,
        string targetCurrency,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (string.IsNullOrWhiteSpace(targetCurrency) || targetCurrency.Trim().Length != 3)
        {
            throw new ArgumentException("Target currency must be a three-letter ISO 4217 code.", nameof(targetCurrency));
        }

        var normalizedTarget = targetCurrency.Trim().ToUpperInvariant();

        if (amount.Currency == normalizedTarget)
        {
            return amount;
        }

        var rate = await _rates
            .GetRateAsync(amount.Currency, normalizedTarget, cancellationToken)
            .ConfigureAwait(false);

        if (rate is null)
        {
            throw new BusinessRuleViolationException(
                MissingRateRule,
                $"No exchange rate available to convert {amount.Amount} {amount.Currency} into {normalizedTarget}: "
                + "the configured provider has no quotation for the pair, and fail-closed conversion never assumes 1:1.",
                new { amount.Currency, TargetCurrency = normalizedTarget });
        }

        return new Money(amount.Amount * rate.Rate, normalizedTarget);
    }
}
