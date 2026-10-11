using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Tests.SharedKernel.Unit.Fx;

/// <summary>
///     Unit tests for the fail-closed FX core (issue #412 stage 2):
///     <see cref="ExchangeRate"/>, <see cref="ManualExchangeRateProvider"/>,
///     <see cref="FixedOverrideExchangeRateProvider"/>, and <see cref="CurrencyConversionService"/>.
/// </summary>
public class ExchangeRateTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidPair_Should_Normalize_Casing_And_Trim()
    {
        var rate = new ExchangeRate(" usd ", "eur", 0.92m, AsOf, " manual ");

        rate.BaseCurrency.Should().Be("USD");
        rate.QuoteCurrency.Should().Be("EUR");
        rate.Rate.Should().Be(0.92m);
        rate.AsOfUtc.Should().Be(AsOf);
        rate.Source.Should().Be("manual");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDX")]
    public void Constructor_WithMalformedBaseCurrency_Should_Throw(string? baseCurrency)
    {
        var act = () => new ExchangeRate(baseCurrency!, "EUR", 0.92m, AsOf, "manual");

        act.Should().Throw<ArgumentException>().WithMessage("*Base currency*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("EU")]
    public void Constructor_WithMalformedQuoteCurrency_Should_Throw(string? quoteCurrency)
    {
        var act = () => new ExchangeRate("USD", quoteCurrency!, 0.92m, AsOf, "manual");

        act.Should().Throw<ArgumentException>().WithMessage("*Quote currency*");
    }

    [Theory]
    [InlineData("USD", "usd")]
    [InlineData("jpy", "JPY")]
    public void Constructor_WithSameCurrencies_Should_Throw(string baseCurrency, string quoteCurrency)
    {
        var act = () => new ExchangeRate(baseCurrency, quoteCurrency, 0.92m, AsOf, "manual");

        act.Should().Throw<ArgumentException>().WithMessage("*must differ*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.92)]
    public void Constructor_WithNonPositiveRate_Should_Throw(decimal rate)
    {
        var act = () => new ExchangeRate("USD", "EUR", rate, AsOf, "manual");

        act.Should().Throw<ArgumentException>().WithMessage("*strictly positive*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingSource_Should_Throw(string? source)
    {
        var act = () => new ExchangeRate("USD", "EUR", 0.92m, AsOf, source!);

        act.Should().Throw<ArgumentException>().WithMessage("*source*");
    }

    [Theory]
    [InlineData("USD", "EUR", true)]
    [InlineData("usd", "eur", true)]
    [InlineData("EUR", "USD", false)]
    [InlineData("USD", "GBP", false)]
    public void Matches_Should_Be_Case_Insensitive_And_Directional(string from, string to, bool expected)
    {
        var rate = new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual");

        rate.Matches(from, to).Should().Be(expected);
    }

    [Fact]
    public void Invert_Should_Produce_The_Reciprocal_Pair()
    {
        var rate = new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual");

        var inverse = rate.Invert();

        inverse.BaseCurrency.Should().Be("EUR");
        inverse.QuoteCurrency.Should().Be("USD");
        inverse.Rate.Should().Be(decimal.Round(1m / 0.92m, 12, MidpointRounding.AwayFromZero));
        inverse.AsOfUtc.Should().Be(AsOf);
        inverse.Source.Should().Be("manual");
    }
}

public class ManualExchangeRateProviderTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetRateAsync_WithRegisteredPair_Should_Return_The_Exact_Quotation()
    {
        var rate = new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual");
        var provider = new ManualExchangeRateProvider([rate]);

        var served = await provider.GetRateAsync("USD", "EUR");

        served.Should().Be(rate);
    }

    [Fact]
    public async Task GetRateAsync_Should_Be_Case_Insensitive()
    {
        var provider = new ManualExchangeRateProvider([new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual")]);

        var served = await provider.GetRateAsync("usd", "eur");

        served.Should().NotBeNull();
        served!.Rate.Should().Be(0.92m);
    }

    [Fact]
    public async Task GetRateAsync_WithReverseDirection_Should_Return_Null()
    {
        // Directionality is deliberate: registering USD→EUR is an operator decision about
        // that pair only; EUR→USD must be entered separately rather than derived.
        var provider = new ManualExchangeRateProvider([new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual")]);

        var served = await provider.GetRateAsync("EUR", "USD");

        served.Should().BeNull();
    }

    [Fact]
    public async Task GetRateAsync_WithUnknownPair_Should_Return_Null_Not_Throw()
    {
        var provider = new ManualExchangeRateProvider([new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual")]);

        var served = await provider.GetRateAsync("GBP", "JPY");

        served.Should().BeNull("an unknown pair is an absent quotation, not an error");
    }

    [Fact]
    public async Task GetRateAsync_WithSameCurrency_Should_Return_Null()
    {
        var provider = new ManualExchangeRateProvider([]);

        var served = await provider.GetRateAsync("USD", "USD");

        served.Should().BeNull("providers never fabricate an implicit 1:1");
    }

    [Fact]
    public void Constructor_WithDuplicatePair_Should_Throw()
    {
        var act = () => new ManualExchangeRateProvider(
        [
            new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual"),
            new ExchangeRate("USD", "EUR", 0.93m, AsOf.AddHours(1), "manual"),
        ]);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate manual exchange rate*USD→EUR*");
    }
}

public class FixedOverrideExchangeRateProviderTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class StubProvider(Func<string, string, ExchangeRate?> resolve) : IExchangeRateProvider
    {
        public Task<ExchangeRate?> GetRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(resolve(baseCurrency, quoteCurrency));
        }
    }

    [Fact]
    public async Task GetRateAsync_WithOverrideForPair_Should_Win_Over_Inner_Provider()
    {
        var marketRate = new ExchangeRate("USD", "EUR", 0.90m, AsOf, "market");
        var pinnedRate = new ExchangeRate("USD", "EUR", 0.92m, AsOf, "override");
        var provider = new FixedOverrideExchangeRateProvider(
            [pinnedRate],
            new StubProvider((from, to) => marketRate));

        var served = await provider.GetRateAsync("USD", "EUR");

        served.Should().Be(pinnedRate, "a fixed override must beat the wrapped provider for its pair");
    }

    [Fact]
    public async Task GetRateAsync_WithoutOverride_Should_Delegate_To_Inner_Provider()
    {
        var marketRate = new ExchangeRate("EUR", "GBP", 0.86m, AsOf, "market");
        var provider = new FixedOverrideExchangeRateProvider(
            [new ExchangeRate("USD", "EUR", 0.92m, AsOf, "override")],
            new StubProvider((from, to) => from == "EUR" && to == "GBP" ? marketRate : null));

        var served = await provider.GetRateAsync("EUR", "GBP");

        served.Should().Be(marketRate);
    }

    [Fact]
    public async Task GetRateAsync_When_Neither_Has_The_Pair_Should_Return_Null()
    {
        var provider = new FixedOverrideExchangeRateProvider(
            [new ExchangeRate("USD", "EUR", 0.92m, AsOf, "override")],
            new StubProvider((from, to) => null));

        var served = await provider.GetRateAsync("USD", "JPY");

        served.Should().BeNull();
    }

    [Fact]
    public async Task GetRateAsync_Override_Should_Be_Directional()
    {
        var innerRate = new ExchangeRate("EUR", "USD", 1.08m, AsOf, "market");
        var provider = new FixedOverrideExchangeRateProvider(
            [new ExchangeRate("USD", "EUR", 0.92m, AsOf, "override")],
            new StubProvider((from, to) => innerRate));

        var reverse = await provider.GetRateAsync("EUR", "USD");

        reverse.Should().Be(innerRate, "the pinned direction does not shadow the reciprocal direction");
    }
}

public class CurrencyConversionServiceTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class ThrowingProvider : IExchangeRateProvider
    {
        public Task<ExchangeRate?> GetRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("provider must not be consulted for same-currency conversion");
        }
    }

    private sealed class DictionaryProvider(IReadOnlyDictionary<(string From, string To), decimal> rates) : IExchangeRateProvider
    {
        public Task<ExchangeRate?> GetRateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
        {
            var normalizedFrom = baseCurrency.Trim().ToUpperInvariant();
            var normalizedTo = quoteCurrency.Trim().ToUpperInvariant();

            return Task.FromResult(rates.TryGetValue((normalizedFrom, normalizedTo), out var rate)
                ? new ExchangeRate(normalizedFrom, normalizedTo, rate, AsOf, "test")
                : null);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithSameCurrency_Should_Return_The_Same_Instance_Without_Consulting_The_Provider()
    {
        var amount = new Money(19.99m, "USD");
        var service = new CurrencyConversionService(new ThrowingProvider());

        var converted = await service.ConvertAsync(amount, "USD");

        converted.Should().BeSameAs(amount, "same-currency identity is exact arithmetic, not an FX lookup");
    }

    [Fact]
    public async Task ConvertAsync_WithAvailableRate_Should_Convert_And_Round_To_Target_Minor_Units()
    {
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>
        {
            [("USD", "EUR")] = 0.92m,
        }));

        var converted = await service.ConvertAsync(new Money(19.99m, "USD"), "EUR");

        converted.Amount.Should().Be(18.39m, "19.99 * 0.92 = 18.3908 rounds to 18.39 at EUR's two minor units");
        converted.Currency.Should().Be("EUR");
        converted.MinorUnitExponent.Should().Be(2);
    }

    [Fact]
    public async Task ConvertAsync_Into_Zero_Decimal_Currency_Should_Round_To_Integer_Units()
    {
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>
        {
            [("USD", "JPY")] = 150m,
        }));

        var converted = await service.ConvertAsync(new Money(19.99m, "USD"), "JPY");

        converted.Amount.Should().Be(2999m, "19.99 * 150 = 2998.5 rounds to 2999 at JPY's zero minor units");
        converted.Currency.Should().Be("JPY");
        converted.MinorUnitExponent.Should().Be(0);
    }

    [Fact]
    public async Task ConvertAsync_Into_Three_Decimal_Currency_Should_Round_To_Three_Minor_Units()
    {
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>
        {
            [("USD", "KWD")] = 0.3065m,
        }));

        var converted = await service.ConvertAsync(new Money(10.00m, "USD"), "KWD");

        converted.Amount.Should().Be(3.065m);
        converted.MinorUnitExponent.Should().Be(3);
    }

    [Fact]
    public async Task ConvertAsync_WithoutRate_Should_Throw_Fail_Closed_Never_Silent_OneToOne()
    {
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>()));

        var act = () => service.ConvertAsync(new Money(19.99m, "USD"), "EUR");

        var exception = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        exception.Which.Rule.Should().Be(CurrencyConversionService.MissingRateRule);
        exception.Which.Message.Should().Contain("USD").And.Contain("EUR").And.Contain("never assumes 1:1");
    }

    [Fact]
    public async Task ConvertAsync_WithRate_Only_In_Reverse_Direction_Should_Throw_Fail_Closed()
    {
        // EUR→USD registered, USD→EUR asked: the exact direction has no quotation, so the
        // conversion fails rather than silently deriving from the reciprocal.
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>
        {
            [("EUR", "USD")] = 1.08m,
        }));

        var act = () => service.ConvertAsync(new Money(19.99m, "USD"), "EUR");

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be(CurrencyConversionService.MissingRateRule);
    }

    [Fact]
    public async Task ConvertAsync_WithMalformedTargetCurrency_Should_Throw_ArgumentException()
    {
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>()));

        var act = () => service.ConvertAsync(new Money(19.99m, "USD"), "EU");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*three-letter ISO 4217*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConvertAsync_WithEmptyTargetCurrency_Should_Throw_ArgumentException(string? target)
    {
        var service = new CurrencyConversionService(new DictionaryProvider(new Dictionary<(string, string), decimal>()));

        var act = () => service.ConvertAsync(new Money(19.99m, "USD"), target!);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*three-letter ISO 4217*");
    }

    [Fact]
    public async Task ConvertAsync_When_Provider_Fails_Should_Propagate_The_Failure()
    {
        var service = new CurrencyConversionService(new ThrowingProvider());

        var act = () => service.ConvertAsync(new Money(19.99m, "USD"), "EUR");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WithNullProvider_Should_Throw()
    {
        var act = () => new CurrencyConversionService(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class FxServiceCollectionExtensionsTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddManualExchangeRates_Should_Register_A_Singleton_Provider_Serving_The_Rates()
    {
        var services = new ServiceCollection();

        services.AddManualExchangeRates([new ExchangeRate("USD", "EUR", 0.92m, AsOf, "manual")]);
        services.AddCurrencyConversion();

        using var provider = services.BuildServiceProvider();
        var rates = provider.GetRequiredService<IExchangeRateProvider>();
        rates.Should().BeOfType<ManualExchangeRateProvider>();
        provider.GetRequiredService<CurrencyConversionService>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddFixedExchangeRateOverrides_Should_Wrap_The_Registered_Provider()
    {
        var services = new ServiceCollection();

        services.AddManualExchangeRates(
        [
            new ExchangeRate("USD", "EUR", 0.90m, AsOf, "manual"),
            new ExchangeRate("EUR", "GBP", 0.86m, AsOf, "manual"),
        ]);
        services.AddFixedExchangeRateOverrides([new ExchangeRate("USD", "EUR", 0.92m, AsOf, "override")]);
        services.AddCurrencyConversion();

        using var provider = services.BuildServiceProvider();
        var rates = provider.GetRequiredService<IExchangeRateProvider>();
        rates.Should().BeOfType<FixedOverrideExchangeRateProvider>();
        (await rates.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.92m);
        (await rates.GetRateAsync("EUR", "GBP"))!.Rate.Should().Be(0.86m);
    }

    [Fact]
    public void AddFixedExchangeRateOverrides_Without_Base_Provider_Should_Throw()
    {
        var services = new ServiceCollection();

        var act = () => services.AddFixedExchangeRateOverrides([new ExchangeRate("USD", "EUR", 0.92m, AsOf, "override")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*base IExchangeRateProvider*");
    }

    [Fact]
    public async Task Registered_Stack_Should_Convert_Through_The_Override()
    {
        var services = new ServiceCollection();

        services.AddManualExchangeRates([new ExchangeRate("USD", "JPY", 140m, AsOf, "manual")]);
        services.AddFixedExchangeRateOverrides([new ExchangeRate("USD", "JPY", 150m, AsOf, "override")]);
        services.AddCurrencyConversion();

        using var provider = services.BuildServiceProvider();
        var conversion = provider.GetRequiredService<CurrencyConversionService>();

        var converted = await conversion.ConvertAsync(new Money(19.99m, "USD"), "JPY");

        converted.Amount.Should().Be(2999m, "the override rate (150), not the manual base rate (140), must apply");
    }
}
