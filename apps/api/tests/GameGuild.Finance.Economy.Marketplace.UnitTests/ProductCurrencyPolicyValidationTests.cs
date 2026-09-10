using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class ProductCurrencyPolicyValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PolicyAndQuote_ExposeTheVersionedSnapshot()
    {
        var productId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var policy = ProductCurrencyPolicyVersion.Create(
            productId, sellerId, 7, ProductCurrencyMode.FixedMix,
            100, 100_000, 125_000, Now);

        var quote = policy.Quote(MarketplaceCurrencyChoice.FixedMix);

        policy.ProductId.Should().Be(productId);
        policy.SellerId.Should().Be(sellerId);
        policy.Version.Should().Be(7);
        policy.Mode.Should().Be(ProductCurrencyMode.FixedMix);
        policy.HardPriceUnits.Should().Be(100);
        policy.SoftPriceUnits.Should().Be(100_000);
        policy.PlatformFeePpm.Should().Be(125_000);
        policy.EffectiveAt.Should().Be(Now);
        quote.ProductId.Should().Be(productId);
        quote.SellerId.Should().Be(sellerId);
        quote.Mode.Should().Be(ProductCurrencyMode.FixedMix);
        quote.Legs[0].Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 100));
        quote.Legs[1].Amount.Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 100_000));
    }

    [Fact]
    public void PriceLeg_RejectsInvalidOrNonConservingValues()
    {
        var invalidCurrency = (CurrencyCode)999;

        FluentActions.Invoking(() => new MarketplacePriceLegSnapshot(invalidCurrency, 1, 1, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 0, 0, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 1, -1, 2))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 1, 1, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 2, 1, 0))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
                new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, long.MaxValue, long.MaxValue, 1))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void QuoteSnapshot_RejectsInvalidIdentityModeAndLegs()
    {
        var hard = new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 1, 1, 0);
        var duplicateHard = new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 2, 2, 0);

        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(Guid.Empty, Guid.NewGuid(), 1, ProductCurrencyMode.HardOnly, [hard]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(Guid.NewGuid(), Guid.Empty, 1, ProductCurrencyMode.HardOnly, [hard]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(Guid.NewGuid(), Guid.NewGuid(), 0, ProductCurrencyMode.HardOnly, [hard]))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, (ProductCurrencyMode)999, [hard]))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, ProductCurrencyMode.HardOnly, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, ProductCurrencyMode.HardOnly, []))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
                new MarketplaceQuoteSnapshot(
                    Guid.NewGuid(), Guid.NewGuid(), 1, ProductCurrencyMode.HardOnly, [hard, duplicateHard]))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PolicyCreation_RejectsEveryInvalidConfigurationBoundary()
    {
        var product = Guid.NewGuid();
        var seller = Guid.NewGuid();

        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                Guid.Empty, seller, 1, ProductCurrencyMode.HardOnly, 1, 0, 0, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, Guid.Empty, 1, ProductCurrencyMode.HardOnly, 1, 0, 0, Now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, seller, 0, ProductCurrencyMode.HardOnly, 1, 0, 0, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, seller, 1, (ProductCurrencyMode)999, 1, 0, 0, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, seller, 1, ProductCurrencyMode.HardOnly, -1, 0, 0, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, seller, 1, ProductCurrencyMode.SoftOnly, 0, -1, 0, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, seller, 1, ProductCurrencyMode.HardOnly, 1, 0, -1, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                product, seller, 1, ProductCurrencyMode.HardOnly, 1, 0,
                ProductCurrencyPolicyVersion.PartsPerMillion, Now))
            .Should().Throw<ArgumentOutOfRangeException>();

        var invalidPrices = new[]
        {
            (ProductCurrencyMode.HardOnly, 0L, 0L),
            (ProductCurrencyMode.HardOnly, 1L, 1L),
            (ProductCurrencyMode.SoftOnly, 1L, 1L),
            (ProductCurrencyMode.SoftOnly, 0L, 0L),
            (ProductCurrencyMode.Either, 0L, 1L),
            (ProductCurrencyMode.Either, 1L, 0L),
            (ProductCurrencyMode.FixedMix, 0L, 1L),
            (ProductCurrencyMode.FixedMix, 1L, 0L)
        };

        foreach (var (mode, hard, soft) in invalidPrices)
        {
            FluentActions.Invoking(() => ProductCurrencyPolicyVersion.Create(
                    product, seller, 1, mode, hard, soft, 0, Now))
                .Should().Throw<MarketplaceCurrencyPolicyException>();
        }
    }

    [Fact]
    public void QuoteAndFee_RejectInvalidChoiceAndFeeBoundaries()
    {
        var policy = ProductCurrencyPolicyVersion.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, ProductCurrencyMode.HardOnly, 100, 0, 0, Now);

        policy.Invoking(value => value.Quote((MarketplaceCurrencyChoice)999))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.CalculateFee(0, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProductCurrencyPolicyVersion.CalculateFee(1, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() =>
                ProductCurrencyPolicyVersion.CalculateFee(
                    1, ProductCurrencyPolicyVersion.PartsPerMillion))
            .Should().Throw<ArgumentOutOfRangeException>();
        ProductCurrencyPolicyVersion.CalculateFee(long.MaxValue, 999_999)
            .Should().Be(9_223_362_813_482_738_952);
    }
}
