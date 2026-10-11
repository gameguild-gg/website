using System.Text.Json;
using FluentAssertions;

namespace GameGuild.Tests.SharedKernel.Unit;

/// <summary>
///     Unit tests for the minor-unit / per-currency-exponent upgrade of <see cref="Money"/>
///     (issue #412 stage 1) and its backward compatibility with the historical
///     two-decimal decimal-based contract.
/// </summary>
public class MoneyMinorUnitTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Per-currency exponent rounding
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1234.56, 1235)]   // AwayFromZero at 0 decimals rounds .5 up
    [InlineData(1234.49, 1234)]
    [InlineData(1234.0, 1234)]
    public void Constructor_WithJpy_Should_Round_To_Zero_Decimals(decimal input, decimal expected)
    {
        var money = new Money(input, "JPY");

        money.Amount.Should().Be(expected);
        money.MinorUnitExponent.Should().Be(0);
        money.MinorUnits.Should().Be((long)expected);
    }

    [Theory]
    [InlineData(1.2345, "1.235")] // KWD has 3 minor-unit digits
    [InlineData(1.2344, "1.234")]
    [InlineData(0.0005, "0.001")]
    public void Constructor_WithThreeDecimalCurrency_Should_Round_To_Three_Decimals(decimal input, string expected)
    {
        var money = new Money(input, "KWD");

        money.Amount.Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
        money.MinorUnitExponent.Should().Be(3);
    }

    [Theory]
    [InlineData(10.125, 10.13)]   // legacy AwayFromZero midpoint behavior preserved
    [InlineData(10.124, 10.12)]
    public void Constructor_WithUsd_Should_Keep_Legacy_TwoDecimal_Rounding(decimal input, decimal expected)
    {
        var money = new Money(input, "USD");

        money.Amount.Should().Be(expected);
        money.MinorUnitExponent.Should().Be(2);
    }

    [Theory]
    [InlineData(1.999, 2.00)]
    [InlineData(1.994, 1.99)]
    public void Constructor_WithUnknownCurrency_Should_Fall_Back_To_Two_Decimals(decimal input, decimal expected)
    {
        var money = new Money(input, "XYZ");

        money.Amount.Should().Be(expected);
        money.MinorUnitExponent.Should().Be(2);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Minor-unit round-trips
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("USD", 1999, "19.99")]
    [InlineData("USD", 0, "0")]
    [InlineData("JPY", 1250, "1250")]
    [InlineData("KWD", 1235, "1.235")]
    public void FromMinorUnits_Should_Produce_Exact_Major_Unit_Amount(string currency, long minorUnits, string expected)
    {
        var money = Money.FromMinorUnits(minorUnits, currency);

        money.Currency.Should().Be(currency);
        money.Amount.Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
        money.MinorUnits.Should().Be(minorUnits, "minor-unit round-trip must be exact");
    }

    [Fact]
    public void FromMinorUnits_WithNegativeUnits_Should_Throw()
    {
        var act = () => Money.FromMinorUnits(-1, "USD");

        act.Should().Throw<ArgumentException>().WithMessage("*Minor units cannot be negative*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromMinorUnits_WithEmptyCurrency_Should_Throw(string? currency)
    {
        var act = () => Money.FromMinorUnits(100, currency!);

        act.Should().Throw<ArgumentException>().WithMessage("*Currency cannot be null or empty*");
    }

    [Fact]
    public void MinorUnits_OfZeroUsd_Should_Be_Zero()
    {
        Money.Zero("USD").MinorUnits.Should().Be(0);
        Money.Zero("JPY").MinorUnits.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Explicit rounding modes
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(MoneyRoundingMode.AwayFromZero, 2.01)]
    [InlineData(MoneyRoundingMode.ToEven, 2.00)]
    [InlineData(MoneyRoundingMode.Truncate, 2.00)]
    [InlineData(MoneyRoundingMode.Ceiling, 2.01)]
    public void Create_WithRoundingMode_AtMidpoint_Should_Apply_Requested_Strategy(
        MoneyRoundingMode mode, decimal expected)
    {
        var money = Money.Create(2.005m, "USD", mode);

        money.Amount.Should().Be(expected);
    }

    [Theory]
    [InlineData(MoneyRoundingMode.AwayFromZero, 2.01)]
    [InlineData(MoneyRoundingMode.ToEven, 2.01)]
    [InlineData(MoneyRoundingMode.Truncate, 2.00)]
    [InlineData(MoneyRoundingMode.Ceiling, 2.01)]
    public void Create_WithRoundingMode_AboveMidpoint_Should_Apply_Requested_Strategy(
        MoneyRoundingMode mode, decimal expected)
    {
        var money = Money.Create(2.009m, "USD", mode);

        money.Amount.Should().Be(expected);
    }

    [Theory]
    [InlineData(MoneyRoundingMode.AwayFromZero, 1235)]
    [InlineData(MoneyRoundingMode.ToEven, 1234)]  // 1234.5 -> even neighbor
    [InlineData(MoneyRoundingMode.Truncate, 1234)]
    [InlineData(MoneyRoundingMode.Ceiling, 1235)]
    public void Create_WithRoundingMode_OnZeroDecimalCurrency_Should_Apply_Requested_Strategy(
        MoneyRoundingMode mode, decimal expected)
    {
        var money = Money.Create(1234.5m, "JPY", mode);

        money.Amount.Should().Be(expected);
    }

    [Fact]
    public void Create_WithAwayFromZero_Should_Match_Constructor_Behavior()
    {
        var legacyStyle = new Money(2.005m, "USD");
        var explicitStyle = Money.Create(2.005m, "USD", MoneyRoundingMode.AwayFromZero);

        legacyStyle.Should().Be(explicitStyle);
    }

    [Fact]
    public void Create_WithNegativeAmountOrEmptyCurrency_Should_Throw()
    {
        var negative = () => Money.Create(-1m, "USD", MoneyRoundingMode.Truncate);
        negative.Should().Throw<ArgumentException>().WithMessage("*Amount cannot be negative*");

        var emptyCurrency = () => Money.Create(1m, "  ", MoneyRoundingMode.Truncate);
        emptyCurrency.Should().Throw<ArgumentException>().WithMessage("*Currency cannot be null or empty*");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Backward compatibility: equality, operators, ToString, serialization
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_Should_Ignore_Rounding_Path_Used_To_Produce_The_Amount()
    {
        var viaRounding = new Money(2.005m, "USD");
        var direct = new Money(2.01m, "USD");

        viaRounding.Should().Be(direct);
        viaRounding.GetHashCode().Should().Be(direct.GetHashCode());
    }

    [Fact]
    public void Arithmetic_OnJpy_Should_Round_Results_To_Integer_Yen()
    {
        var unit = new Money(100m, "JPY");

        (unit * 1.5m).Amount.Should().Be(150);
        (unit / 3m).Amount.Should().Be(33); // 33.33... truncated to 0 decimals via AwayFromZero
    }

    [Fact]
    public void Arithmetic_OnUsd_Should_Keep_Legacy_Behavior()
    {
        var money = new Money(99.99m, "USD");

        (money / 3m).Amount.Should().Be(33.33m);
        (money * 2m).Amount.Should().Be(199.98m);
    }

    [Fact]
    public void ToString_Should_Use_Currency_Exponent()
    {
        new Money(123.456m, "USD").ToString().Should().Be("123.46 USD");
        new Money(1234.5m, "JPY").ToString().Should().Be("1235 JPY");
        new Money(1.2345m, "KWD").ToString().Should().Be("1.235 KWD");
    }

    [Fact]
    public void Serialization_WithApiOptions_Should_Keep_Exactly_Amount_And_Currency_Properties()
    {
        var money = Money.FromMinorUnits(1999, "USD");

        var json = JsonSerializer.Serialize(money, SharedJsonOptions.Api);

        using var document = JsonDocument.Parse(json);
        var propertyNames = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToList();

        propertyNames.Should().BeEquivalentTo(new[] { "amount", "currency" },
            "the persisted/serialized Money shape must stay exactly { Amount, Currency }");
        document.RootElement.GetProperty("amount").GetDecimal().Should().Be(19.99m);
        document.RootElement.GetProperty("currency").GetString().Should().Be("USD");
    }

    [Fact]
    public void Serialization_Should_Round_Trip_Legacy_Payloads()
    {
        const string legacyJson = """{"Amount":10.55,"Currency":"USD"}""";

        var deserialized = JsonSerializer.Deserialize<Money>(legacyJson);

        deserialized.Should().Be(new Money(10.55m, "USD"));
    }

    [Fact]
    public void Deserialization_WithWebOptions_Should_Round_Trip()
    {
        var original = Money.FromMinorUnits(1235, "KWD");

        var json = JsonSerializer.Serialize(original, SharedJsonOptions.Web);
        var deserialized = JsonSerializer.Deserialize<Money>(json, SharedJsonOptions.Web);

        deserialized.Should().Be(original);
        deserialized!.MinorUnits.Should().Be(1235);
    }
}
