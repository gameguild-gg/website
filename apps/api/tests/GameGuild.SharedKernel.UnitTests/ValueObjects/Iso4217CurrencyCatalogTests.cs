using FluentAssertions;

namespace GameGuild.Tests.SharedKernel.Unit.ValueObjects;

/// <summary>
///     Unit tests for the ISO 4217 currency catalog (issue #412 stage 1).
/// </summary>
public class Iso4217CurrencyCatalogTests
{
    [Fact]
    public void Count_Should_Exceed_Sixty_Currencies_For_Growth()
    {
        Iso4217CurrencyCatalog.Count.Should().BeGreaterThanOrEqualTo(60);
        Iso4217CurrencyCatalog.All.Should().HaveCount(Iso4217CurrencyCatalog.Count);
    }

    [Fact]
    public void Catalog_Should_Cover_All_Six_Whitelisted_Billing_Currencies()
    {
        foreach (var code in new[] { "USD", "EUR", "GBP", "JPY", "CAD", "AUD" })
        {
            Iso4217CurrencyCatalog.Exists(code).Should().BeTrue($"whitelisted billing currency {code} must be cataloged");
        }
    }

    [Theory]
    [InlineData("USD", "840", 2)]
    [InlineData("EUR", "978", 2)]
    [InlineData("GBP", "826", 2)]
    [InlineData("CAD", "124", 2)]
    [InlineData("AUD", "036", 2)]
    [InlineData("JPY", "392", 0)]
    public void TryGet_WithWhitelistedCurrency_Should_Return_Canonical_Entry(
        string code, string expectedNumeric, int expectedExponent)
    {
        var found = Iso4217CurrencyCatalog.TryGet(code, out var currency);

        found.Should().BeTrue();
        currency.Should().NotBeNull();
        currency!.Code.Should().Be(code);
        currency.NumericCode.Should().Be(expectedNumeric);
        currency.MinorUnitExponent.Should().Be(expectedExponent);
        currency.Symbol.Should().NotBeNullOrEmpty();
        currency.EnglishName.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("KWD", 3)]
    [InlineData("BHD", 3)]
    [InlineData("OMR", 3)]
    [InlineData("JOD", 3)]
    [InlineData("TND", 3)]
    [InlineData("IQD", 3)]
    [InlineData("LYD", 3)]
    [InlineData("CLF", 4)]
    [InlineData("KRW", 0)]
    [InlineData("VND", 0)]
    [InlineData("CLP", 0)]
    [InlineData("ISK", 0)]
    [InlineData("HUF", 0)]
    [InlineData("PYG", 0)]
    [InlineData("MMK", 0)]
    [InlineData("XOF", 0)]
    [InlineData("XPF", 0)]
    public void TryGet_WithSpecialExponentCurrency_Should_Report_Iso_Exponent(string code, int expectedExponent)
    {
        var found = Iso4217CurrencyCatalog.TryGet(code, out var currency);

        found.Should().BeTrue();
        currency!.MinorUnitExponent.Should().Be(expectedExponent);
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("Usd")]
    [InlineData("uSd")]
    public void TryGet_Should_Be_Case_Insensitive(string code)
    {
        var found = Iso4217CurrencyCatalog.TryGet(code, out var currency);

        found.Should().BeTrue();
        currency!.Code.Should().Be("USD");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("US")]
    [InlineData("USDX")]
    [InlineData("ZZZ")]
    [InlineData("123")]
    public void TryGet_WithUnknownOrMalformedCode_Should_Return_False(string? code)
    {
        Iso4217CurrencyCatalog.TryGet(code, out var currency).Should().BeFalse();
        currency.Should().BeNull();
        Iso4217CurrencyCatalog.Exists(code).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ZZZ")]
    [InlineData("TOOLONG")]
    public void GetMinorUnitExponent_WithUnknownCode_Should_Fall_Back_To_Two(string? code)
    {
        Iso4217CurrencyCatalog.GetMinorUnitExponent(code)
            .Should().Be(Iso4217CurrencyCatalog.DefaultMinorUnitExponent, "unknown codes keep legacy two-decimal behavior");
    }

    [Theory]
    [InlineData("JPY", 0)]
    [InlineData("USD", 2)]
    [InlineData("KWD", 3)]
    [InlineData("CLF", 4)]
    public void GetMinorUnitExponent_WithKnownCode_Should_Return_Catalog_Exponent(string code, int expected)
    {
        Iso4217CurrencyCatalog.GetMinorUnitExponent(code).Should().Be(expected);
    }

    [Fact]
    public void Get_WithUnknownCode_Should_Throw_ArgumentException()
    {
        var act = () => Iso4217CurrencyCatalog.Get("ZZZ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not a known ISO 4217*");
    }

    [Fact]
    public void Get_WithKnownCode_Should_Return_Entry_Regardless_Of_Casing()
    {
        Iso4217CurrencyCatalog.Get("jpy").Code.Should().Be("JPY");
    }

    [Fact]
    public void Catalog_Should_Have_Unique_And_Well_Formed_Codes()
    {
        var entries = Iso4217CurrencyCatalog.All;

        entries.Select(entry => entry.Code).Should().OnlyHaveUniqueItems();
        entries.Select(entry => entry.NumericCode).Should().OnlyHaveUniqueItems();
        entries.All(entry => entry.Code.Length == 3 && entry.Code.All(char.IsUpper))
            .Should().BeTrue("ISO 4217 alphabetic codes are three upper-case letters");
        entries.All(entry => entry.NumericCode.Length == 3 && entry.NumericCode.All(char.IsDigit))
            .Should().BeTrue("ISO 4217 numeric codes are three digits");
        entries.All(entry => entry.MinorUnitExponent is >= 0 and <= 4)
            .Should().BeTrue("ISO 4217 minor-unit exponents are between 0 and 4");
    }
}
