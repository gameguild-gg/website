using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Marketplace;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class ProductCurrencyPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ProductCurrencyMode.HardOnly, MarketplaceCurrencyChoice.Hard, 100L, 0L)]
    [InlineData(ProductCurrencyMode.SoftOnly, MarketplaceCurrencyChoice.Soft, 0L, 100_000L)]
    [InlineData(ProductCurrencyMode.Either, MarketplaceCurrencyChoice.Hard, 100L, 0L)]
    [InlineData(ProductCurrencyMode.Either, MarketplaceCurrencyChoice.Soft, 0L, 100_000L)]
    [InlineData(ProductCurrencyMode.FixedMix, MarketplaceCurrencyChoice.FixedMix, 100L, 100_000L)]
    public void Quote_EnforcesTheVersionedAcceptedCurrencyPolicy(
        ProductCurrencyMode mode,
        MarketplaceCurrencyChoice choice,
        long expectedHard,
        long expectedSoft)
    {
        var policy = ProductCurrencyPolicyVersion.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            version: 3,
            mode,
            hardPriceUnits: mode == ProductCurrencyMode.SoftOnly ? 0 : 100,
            softPriceUnits: mode == ProductCurrencyMode.HardOnly ? 0 : 100_000,
            platformFeePpm: 100_000,
            effectiveAt: Now);

        var quote = policy.Quote(choice);

        quote.PolicyVersion.Should().Be(3);
        quote.Legs.SingleOrDefault(leg => leg.Currency == CurrencyCode.HardCoin)?.Units
            .Should().Be(expectedHard == 0 ? null : expectedHard);
        quote.Legs.SingleOrDefault(leg => leg.Currency == CurrencyCode.SoftCoin)?.Units
            .Should().Be(expectedSoft == 0 ? null : expectedSoft);
        quote.Legs.Sum(leg => leg.SellerUnits + leg.PlatformFeeUnits)
            .Should().Be(expectedHard + expectedSoft);
    }

    [Fact]
    public void Quote_RejectsAChoiceOutsideTheAcceptedPolicy()
    {
        var policy = ProductCurrencyPolicyVersion.Create(
            Guid.NewGuid(), Guid.NewGuid(), 1, ProductCurrencyMode.HardOnly,
            100, 0, 0, Now);

        policy.Invoking(value => value.Quote(MarketplaceCurrencyChoice.Soft))
            .Should().Throw<MarketplaceCurrencyPolicyException>();
    }
}
