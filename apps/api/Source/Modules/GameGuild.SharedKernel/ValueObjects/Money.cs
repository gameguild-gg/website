using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace GameGuild;

/// <summary>
///     Represents a money value with currency.
///     Amounts are stored as a decimal in major units (e.g. dollars) rounded to the
///     currency's ISO 4217 minor-unit exponent (see <see cref="Iso4217CurrencyCatalog"/>):
///     JPY keeps zero decimals, USD two, KWD three. Minor-unit (integer) access is available
///     through <see cref="MinorUnits"/> and <see cref="FromMinorUnits"/> for provider
///     integrations that speak the smallest currency unit.
///     The persisted/serialized shape stays exactly { Amount, Currency } — the minor-unit
///     members are computed and excluded from JSON and EF mappings, so existing invoices,
///     payments, and API contracts are unaffected.
/// </summary>
[Owned]
public record Money
{
    /// <summary>
    ///     Parameterless constructor for EF Core
    /// </summary>
    protected Money()
    {
        Amount = 0;
        Currency = "USD";
    }

    /// <summary>
    ///     Creates a money value, rounding the amount to the currency's ISO 4217 minor-unit
    ///     exponent using the historical AwayFromZero strategy. Currencies unknown to
    ///     <see cref="Iso4217CurrencyCatalog"/> keep the legacy two-decimal behavior.
    ///     The signature deliberately matches the pre-catalog constructor so serializer and
    ///     EF bindings are unchanged; use <see cref="Create"/> for other rounding strategies.
    /// </summary>
    /// <param name="amount">Amount in major units; must not be negative.</param>
    /// <param name="currency">Three-letter ISO 4217 code (any casing); defaults to USD.</param>
    /// <exception cref="ArgumentException">Thrown when the amount is negative or the currency is empty.</exception>
    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or empty.", nameof(currency));
        }

        Currency = currency.ToUpperInvariant();
        Amount = RoundAmount(amount, Iso4217CurrencyCatalog.GetMinorUnitExponent(Currency), MoneyRoundingMode.AwayFromZero);
    }

    /// <summary>
    ///     Creates a money value with an explicit rounding strategy for surplus precision
    ///     (e.g. <see cref="MoneyRoundingMode.Truncate"/> when passing through provider amounts
    ///     that must never be rounded up).
    /// </summary>
    public static Money Create(decimal amount, string currency, MoneyRoundingMode roundingMode)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or empty.", nameof(currency));
        }

        var normalizedCurrency = currency.ToUpperInvariant();
        return new Money(RoundAmount(amount, Iso4217CurrencyCatalog.GetMinorUnitExponent(normalizedCurrency), roundingMode), normalizedCurrency);
    }

    /// <summary>Amount in major units, already rounded to the currency's minor-unit exponent.</summary>
    public decimal Amount { get; init; }

    /// <summary>Upper-case ISO 4217 currency code.</summary>
    public string Currency { get; init; }

    /// <summary>
    ///     The currency's ISO 4217 minor-unit exponent (0 for JPY, 2 for USD, 3 for KWD).
    ///     Unknown codes report the legacy default of 2.
    /// </summary>
    [JsonIgnore]
    public int MinorUnitExponent { get => Iso4217CurrencyCatalog.GetMinorUnitExponent(Currency); }

    /// <summary>
    ///     The amount expressed in the currency's smallest unit (e.g. cents for USD).
    ///     Exact: <see cref="Amount"/> is always pre-rounded to <see cref="MinorUnitExponent"/> digits.
    ///     Not serialized and not persisted — computed from the canonical { Amount, Currency } pair.
    /// </summary>
    /// <exception cref="OverflowException">Thrown for amounts whose minor-unit representation exceeds <see cref="long"/>.</exception>
    [JsonIgnore]
    public long MinorUnits
    {
        get
        {
            var scale = MinorUnitScale(MinorUnitExponent);
            return (long)decimal.Round(Amount * scale, 0, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    ///     Builds a <see cref="Money"/> from an amount in the currency's smallest unit
    ///     (e.g. <c>FromMinorUnits(1999, "USD")</c> is 19.99 USD, <c>FromMinorUnits(1250, "JPY")</c> is 1250 JPY).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when minorUnits is negative or the currency is empty/unknown-length.</exception>
    public static Money FromMinorUnits(long minorUnits, string currency)
    {
        if (minorUnits < 0)
        {
            throw new ArgumentException("Minor units cannot be negative.", nameof(minorUnits));
        }

        var normalizedCurrency = currency?.ToUpperInvariant()
            ?? throw new ArgumentException("Currency cannot be null or empty.", nameof(currency));
        if (string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            throw new ArgumentException("Currency cannot be null or empty.", nameof(currency));
        }

        var exponent = Iso4217CurrencyCatalog.GetMinorUnitExponent(normalizedCurrency);
        var amount = exponent == 0 ? minorUnits : minorUnits / MinorUnitScale(exponent);
        return new Money(amount, normalizedCurrency);
    }

    /// <summary>Creates zero-valued money in the given currency.</summary>
    public static Money Zero(string currency = "USD") { return new Money(0, currency); }

    /// <summary>
    ///     Rounds an amount to the given number of minor-unit digits with the requested strategy.
    /// </summary>
    internal static decimal RoundAmount(decimal amount, int exponent, MoneyRoundingMode roundingMode)
    {
        if (exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Minor-unit exponent cannot be negative.");
        }

        return roundingMode switch
        {
            MoneyRoundingMode.AwayFromZero => Math.Round(amount, exponent, MidpointRounding.AwayFromZero),
            MoneyRoundingMode.ToEven => Math.Round(amount, exponent, MidpointRounding.ToEven),
            MoneyRoundingMode.Truncate => decimal.Truncate(amount * MinorUnitScale(exponent)) / MinorUnitScale(exponent),
            MoneyRoundingMode.Ceiling => Math.Ceiling(amount * MinorUnitScale(exponent)) / MinorUnitScale(exponent),
            _ => Math.Round(amount, exponent, MidpointRounding.AwayFromZero)
        };
    }

    private static decimal MinorUnitScale(int exponent) { return exponent == 0 ? 1m : DecimalPow10(exponent); }

    private static decimal DecimalPow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException("Cannot add money with different currencies.");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException("Cannot subtract money with different currencies.");
        }

        var result = left.Amount - right.Amount;
        if (result < 0)
        {
            throw new BusinessRuleViolationException("NegativeMoneyResult", $"Money subtraction would result in a negative amount ({result} {left.Currency}).");
        }

        return new Money(result, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier) { return new Money(money.Amount * multiplier, money.Currency); }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Cannot divide money by zero.");
        }

        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException("Cannot compare money with different currencies.");
        }

        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException("Cannot compare money with different currencies.");
        }

        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right) { return !(left < right); }

    public static bool operator <=(Money left, Money right) { return !(left > right); }

    public override string ToString()
    {
        var format = $"{{0:F{MinorUnitExponent}}} {{1}}";
        return string.Format(CultureInfo.InvariantCulture, format, Amount, Currency);
    }
}
