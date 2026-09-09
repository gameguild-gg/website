using FluentAssertions;
using GameGuild.Finance.Economy.Money;

namespace GameGuild.Finance.Economy.UnitTests.Money;

public sealed class AmountTests
{
    [Fact]
    public void FixedParity_UsesApprovedIntegerRatios()
    {
        FixedParity.HardCoinsPerUsd.Should().Be(100);
        FixedParity.SoftCoinsPerUsd.Should().Be(100_000);
        FixedParity.SoftCoinsPerHardCoin.Should().Be(1_000);
        FixedParity.UsdNanosPerUsd.Should().Be(1_000_000_000);
        FixedParity.ToSoft(new HardCoinAmount(73)).Should().Be(new SoftCoinAmount(73_000));
    }

    [Fact]
    public void FixedParity_ExposesNoSoftToHardConversion()
    {
        typeof(FixedParity).GetMethods()
            .Should().NotContain(method =>
                method.ReturnType == typeof(HardCoinAmount) &&
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(SoftCoinAmount)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Amounts_RejectNegativeValues(long value)
    {
        FluentActions.Invoking(() => new HardCoinAmount(value)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new SoftCoinAmount(value)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new UsdNanoAmount(value)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Amounts_AreCheckedAndCannotUnderflow()
    {
        FluentActions.Invoking(() => _ = new HardCoinAmount(long.MaxValue) + new HardCoinAmount(1)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => _ = new SoftCoinAmount(long.MaxValue) + new SoftCoinAmount(1)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => _ = new UsdNanoAmount(long.MaxValue) + new UsdNanoAmount(1)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => _ = new HardCoinAmount(1) - new HardCoinAmount(2)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => _ = new SoftCoinAmount(1) - new SoftCoinAmount(2)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => _ = new UsdNanoAmount(1) - new UsdNanoAmount(2)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Amounts_SupportCheckedArithmeticAndOrdering()
    {
        (new HardCoinAmount(2) + new HardCoinAmount(3)).Should().Be(new HardCoinAmount(5));
        (new HardCoinAmount(9) - new HardCoinAmount(4)).Should().Be(new HardCoinAmount(5));
        (new HardCoinAmount(7) * 3).Should().Be(new HardCoinAmount(21));
        HardCoinAmount.Zero.Should().Be(new HardCoinAmount(0));
        (new SoftCoinAmount(9) - new SoftCoinAmount(4)).Should().Be(new SoftCoinAmount(5));
        (new SoftCoinAmount(2) + new SoftCoinAmount(3)).Should().Be(new SoftCoinAmount(5));
        (new SoftCoinAmount(7) * 3).Should().Be(new SoftCoinAmount(21));
        SoftCoinAmount.Zero.Should().Be(new SoftCoinAmount(0));
        (new UsdNanoAmount(2) + new UsdNanoAmount(3)).Should().Be(new UsdNanoAmount(5));
        (new UsdNanoAmount(9) - new UsdNanoAmount(4)).Should().Be(new UsdNanoAmount(5));
        (new UsdNanoAmount(7) * 3).Should().Be(new UsdNanoAmount(21));
        (new HardCoinAmount(2) < new HardCoinAmount(3)).Should().BeTrue();
        (new HardCoinAmount(3) > new HardCoinAmount(2)).Should().BeTrue();
        (new HardCoinAmount(2) <= new HardCoinAmount(2)).Should().BeTrue();
        (new HardCoinAmount(2) >= new HardCoinAmount(2)).Should().BeTrue();
        new HardCoinAmount(2).CompareTo(new HardCoinAmount(3)).Should().BeNegative();
        (new SoftCoinAmount(3) > new SoftCoinAmount(2)).Should().BeTrue();
        (new SoftCoinAmount(2) < new SoftCoinAmount(3)).Should().BeTrue();
        (new SoftCoinAmount(2) <= new SoftCoinAmount(2)).Should().BeTrue();
        (new SoftCoinAmount(2) >= new SoftCoinAmount(2)).Should().BeTrue();
        new SoftCoinAmount(3).CompareTo(new SoftCoinAmount(2)).Should().BePositive();
        (new UsdNanoAmount(2) < new UsdNanoAmount(3)).Should().BeTrue();
        (new UsdNanoAmount(3) > new UsdNanoAmount(2)).Should().BeTrue();
        (new UsdNanoAmount(2) <= new UsdNanoAmount(2)).Should().BeTrue();
        (new UsdNanoAmount(2) >= new UsdNanoAmount(2)).Should().BeTrue();
        new UsdNanoAmount(2).CompareTo(new UsdNanoAmount(2)).Should().Be(0);
        UsdNanoAmount.Zero.Should().Be(new UsdNanoAmount(0));
    }

    [Fact]
    public void Multiplication_RejectsNegativeOrOverflowingFactors()
    {
        FluentActions.Invoking(() => _ = new HardCoinAmount(1) * -1).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _ = new SoftCoinAmount(1) * -1).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _ = new UsdNanoAmount(1) * -1).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _ = new HardCoinAmount(long.MaxValue) * 2).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => _ = new SoftCoinAmount(long.MaxValue) * 2).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => _ = new UsdNanoAmount(long.MaxValue) * 2).Should().Throw<OverflowException>();
    }

    [Fact]
    public void CheckedAmountProperties_HoldAcrossDeterministicSamples()
    {
        var random = new Random(17072026);
        for (var index = 0; index < 1_000; index++)
        {
            var left = random.NextInt64(0, 1_000_000_000);
            var right = random.NextInt64(0, 1_000_000_000);

            var hardTotal = new HardCoinAmount(left) + new HardCoinAmount(right);
            var softTotal = new SoftCoinAmount(left) + new SoftCoinAmount(right);
            var usdTotal = new UsdNanoAmount(left) + new UsdNanoAmount(right);

            hardTotal.Units.Should().Be(left + right);
            softTotal.Units.Should().Be(left + right);
            usdTotal.Nanos.Should().Be(left + right);
            FixedParity.ToSoft(new HardCoinAmount(left)).Units.Should().Be(left * 1_000);
        }
    }
}
