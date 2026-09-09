using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Policy;

namespace GameGuild.Finance.Economy.UnitTests.Policy;

public sealed class MonetaryPolicyTests
{
    private static readonly DateTimeOffset EffectiveAt = new(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FixedParityAndPolicyArithmeticUseDeterministicFloorRounding()
    {
        EconomyParity.HardCoinUnitsPerUsd.Should().Be(100);
        EconomyParity.SoftCoinUnitsPerUsd.Should().Be(100_000);
        EconomyParity.SoftCoinUnitsPerHardCoin.Should().Be(1_000);
        EconomyParity.EarnedHardMaturity.Should().Be(TimeSpan.FromDays(120));

        var policy = Policy(conversionFeePpm: 3_333, adReservePpm: 250_000);

        policy.Limits.MaximumHardTransferUnits.Should().Be(10_000);
        policy.Limits.MaximumSoftSpendUnits.Should().Be(1_000_000);
        policy.MinimumServiceMarginPpm.Should().Be(200_000);
        policy.ConvertHardToSoft(1).Should().Be(996);
        policy.QuoteAdRewardSoft(1_000_000).Should().Be(75_000);
        policy.QuoteAdRewardSoft(10_000_000).Should().Be(250_000);
    }

    [Fact]
    public void ServicePriceAcceptsExactMarginAndRejectsOneSoftCoinBelowIt()
    {
        var exact = Policy(servicePriceSoftUnits: 100_000, stressedCostMicrousd: 800_000);

        exact.ServicePrices["ai.render"].SoftUnits.Should().Be(100_000);

        FluentActions.Invoking(() => Policy(servicePriceSoftUnits: 99_999, stressedCostMicrousd: 800_000))
            .Should().Throw<ArgumentException>().WithMessage("*minimum gross margin*");
    }

    [Fact]
    public void CatalogResolvesHalfOpenEffectiveWindowsAndRejectsOverlapOrVersionReuse()
    {
        var catalog = new MonetaryPolicyCatalog();
        var first = Policy(version: 1, effectiveAt: EffectiveAt, endsAt: EffectiveAt.AddDays(1));
        var second = Policy(version: 2, effectiveAt: EffectiveAt.AddDays(1), endsAt: null);

        catalog.Add(first);
        catalog.Add(second);

        catalog.Resolve(EffectiveAt).Should().BeSameAs(first);
        catalog.Resolve(EffectiveAt.AddDays(1)).Should().BeSameAs(second);
        FluentActions.Invoking(() => catalog.Resolve(EffectiveAt.AddTicks(-1)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => catalog.Add(Policy(version: 2, effectiveAt: EffectiveAt.AddDays(3))))
            .Should().Throw<InvalidOperationException>().WithMessage("*version*");
        FluentActions.Invoking(() => catalog.Add(Policy(version: 3, effectiveAt: EffectiveAt.AddHours(12))))
            .Should().Throw<InvalidOperationException>().WithMessage("*overlap*");
        catalog.Policies.Should().Equal(first, second);
    }

    [Fact]
    public void PolicyContractsRejectInvalidRatesLimitsPricesWindowsAndOverflow()
    {
        FluentActions.Invoking(() => new AdRewardPolicy(-1, 1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new AdRewardPolicy(1_000_000, 1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new EconomyOperationLimits(0, 1, 1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new EconomyOperationLimits(1, 0, 1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new EconomyOperationLimits(1, 1, 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ServicePricePolicy(" ", 1, 0)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ServicePricePolicy("service", 0, 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ServicePricePolicy("service", 1, -1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => Policy(endsAt: EffectiveAt)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new MonetaryPolicySnapshot(
                new PolicyVersion(1), EffectiveAt, null, 0, 0,
                new AdRewardPolicy(0, 1), new EconomyOperationLimits(1, 1, 1),
                [new ServicePricePolicy("duplicate", 100_000, 0), new ServicePricePolicy("duplicate", 100_000, 0)]))
            .Should().Throw<ArgumentException>().WithMessage("*duplicated*");

        var policy = Policy(conversionFeePpm: 0);
        policy.EarnedHardMaturity.Should().Be(TimeSpan.FromDays(120));
        FluentActions.Invoking(() => policy.ConvertHardToSoft(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => policy.ConvertHardToSoft(long.MaxValue)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => policy.QuoteAdRewardSoft(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CatalogAcceptsSeparatedFiniteAndOpenEndedWindows()
    {
        var catalog = new MonetaryPolicyCatalog();
        var openEnded = Policy(version: 1, effectiveAt: EffectiveAt.AddDays(2));
        var historical = Policy(version: 2, effectiveAt: EffectiveAt, endsAt: EffectiveAt.AddDays(1));

        catalog.Add(openEnded);
        catalog.Add(historical);

        catalog.Policies.Should().Equal(historical, openEnded);
        FluentActions.Invoking(() => catalog.Add(Policy(version: 3, effectiveAt: EffectiveAt.AddDays(3))))
            .Should().Throw<InvalidOperationException>().WithMessage("*overlap*");
    }

    [Fact]
    public void FinitePolicyWindowRejectsExactEndAndLaterInstants()
    {
        var catalog = new MonetaryPolicyCatalog();
        catalog.Add(Policy(effectiveAt: EffectiveAt, endsAt: EffectiveAt.AddHours(1)));

        catalog.Resolve(EffectiveAt).EffectiveAt.Should().Be(EffectiveAt);
        catalog.Resolve(EffectiveAt.AddHours(1).AddTicks(-1)).EffectiveAt.Should().Be(EffectiveAt);
        FluentActions.Invoking(() => catalog.Resolve(EffectiveAt.AddHours(1)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => catalog.Resolve(EffectiveAt.AddHours(2)))
            .Should().Throw<InvalidOperationException>();
    }

    private static MonetaryPolicySnapshot Policy(
        long version = 1,
        DateTimeOffset? effectiveAt = null,
        DateTimeOffset? endsAt = null,
        int conversionFeePpm = 10_000,
        int adReservePpm = 100_000,
        long servicePriceSoftUnits = 100_000,
        long stressedCostMicrousd = 800_000) =>
        new(
            new PolicyVersion(version),
            effectiveAt ?? EffectiveAt,
            endsAt,
            conversionFeePpm,
            200_000,
            new AdRewardPolicy(adReservePpm, 250_000),
            new EconomyOperationLimits(10_000, 1_000_000, 250_000),
            [new ServicePricePolicy("ai.render", servicePriceSoftUnits, stressedCostMicrousd)]);
}
