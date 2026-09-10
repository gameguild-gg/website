using FluentAssertions;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.UnitTests.Reserves;

public sealed class ReserveFormulaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FixedParityFaceValuesAndHardBuffersAreCalculatedExactly()
    {
        ReserveFormula.HardFaceValueUsdMinor(123).Should().Be(123);
        ReserveFormula.SoftFaceValueUsdNanos(100_001).Should().Be(1_000_010_000);
        ReserveFormula.RequiredHardReserveUsdMinor(100, 2, 3, 4).Should().Be(109);
    }

    [Fact]
    public void MinimumServicePriceUsesStressedCostMarginAndCeilingArithmetic()
    {
        ReserveFormula.StressedUnitCostUsdNanos(100, 125, 110).Should().Be(125);
        ReserveFormula.MinimumServicePriceSoftUnits(1_000_000, 250_000).Should().Be(134);
        ReserveFormula.MinimumServicePriceSoftUnits(0, 0).Should().Be(0);

        FluentActions.Invoking(() => ReserveFormula.MinimumServicePriceSoftUnits(1, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ReserveFormula.MinimumServicePriceSoftUnits(1, 1_000_000))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StressedPortfolioUsesWorstUnreservedRatioAndSelectedReservedServices()
    {
        var services = new[]
        {
            Service("render", price: 2, current: 5, trailing: 4, stress: 3, reserved: 2),
            Service("storage", price: 4, current: 8, trailing: 9, stress: 7, reserved: 4)
        };

        ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                outstandingSoftUnits: 9,
                unreservedSoftUnits: 3,
                irreversibleInFlightProviderCostUsdNanos: 7,
                services,
                Now)
            .Should().Be(29);

        ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                outstandingSoftUnits: 9,
                unreservedSoftUnits: 3,
                irreversibleInFlightProviderCostUsdNanos: 7,
                services.Reverse().ToArray(),
                Now)
            .Should().Be(29);
    }

    [Fact]
    public void MissingStaleDisabledOrZeroPricedServiceInputsFailClosed()
    {
        ReserveFormula.StressedExpectedRedemptionCostUsdNanos(0, 0, 0, [], Now)
            .Should().Be(0);
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                1, 1, 0, [], Now))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                1, 1, 0, [Service("stale", 1, 1, 1, 1, 0) with { ExpiresAt = Now }], Now))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                1, 1, 0, [Service("disabled", 1, 1, 1, 1, 0) with { Enabled = false }], Now))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                1, 1, 0, [Service("zero", 0, 1, 1, 1, 0)], Now))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void PortfolioRejectsDoubleCountingAndUnknownReservedServices()
    {
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                10, 9, 0, [Service("render", 1, 1, 1, 1, 2)], Now))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                1, 1, 0,
                [Service("render", 1, 1, 1, 1, 0), Service("render", 1, 1, 1, 1, 0)], Now))
            .Should().Throw<ReserveInputUnknownException>();
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                1, 0, 0, [Service("disabled", 1, 1, 1, 1, 1) with { Enabled = false }], Now))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void RequiredSoftReserveUsesTheHigherExposureAddsBuffersAndRoundsToCents()
    {
        ReserveFormula.RequiredSoftReserveUsdNanos(
                softFaceValueUsdNanos: 9_000_000,
                stressedExpectedRedemptionCostUsdNanos: 10_000_001,
                adEstimateVarianceBufferUsdNanos: 1,
                fraudLossBudgetUsdNanos: 1,
                providerFxBufferUsdNanos: 1,
                operatingLiquidityBufferUsdNanos: 1)
            .Should().Be(20_000_000);
    }

    [Fact]
    public void NegativeAndOverflowingFormulaInputsAreRejected()
    {
        FluentActions.Invoking(() => ReserveFormula.HardFaceValueUsdMinor(-1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ReserveFormula.SoftFaceValueUsdNanos(-1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ReserveFormula.RequiredHardReserveUsdMinor(long.MaxValue, 1, 0, 0))
            .Should().Throw<OverflowException>();
        FluentActions.Invoking(() => ReserveFormula.RequiredHardReserveUsdMinor(0, -1, 0, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
                long.MaxValue, long.MaxValue, long.MaxValue,
                [Service("overflow", 1, long.MaxValue, 0, 0, 0)], Now))
            .Should().Throw<OverflowException>();
    }

    private static ReserveServiceObservation Service(
        string code,
        long price,
        long current,
        long trailing,
        long stress,
        long reserved) =>
        new(code, price, current, trailing, stress, reserved, true, Now.AddMinutes(-1), Now.AddMinutes(5));
}
