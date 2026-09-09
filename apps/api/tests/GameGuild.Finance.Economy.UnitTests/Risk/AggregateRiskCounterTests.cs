using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class AggregateRiskCounterTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MultiDimensionLimitsAreReservedAtomicallyUnderConcurrency()
    {
        var store = new AggregateRiskCounterStore();
        var limits = Limits(50);
        var attempts = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            try
            {
                store.Reserve(
                    Guid.NewGuid(), PostingTemplateKind.Spend,
                    new CoinAmount(CurrencyCode.HardCoin, 10), limits, Time);
                return true;
            }
            catch (AggregateRiskLimitExceededException)
            {
                return false;
            }
        }));

        var outcomes = await Task.WhenAll(attempts);

        outcomes.Count(value => value).Should().Be(5);
        store.Reservations.Should().HaveCount(5);
        store.Reservations.Should().OnlyContain(reservation => reservation.Allocations.Count == 3);
    }

    [Fact]
    public void ReservationReplayMustBeExactAndCounterVersionCannotRegress()
    {
        var store = new AggregateRiskCounterStore();
        var id = Guid.NewGuid();
        var first = store.Reserve(
            id, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 5), Limits(10, 2), Time);

        first.Id.Should().Be(id);
        first.Allocations.Should().OnlyContain(allocation => allocation.CounterVersion == 2);
        store.Reserve(
            id, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 5), Limits(10, 2), Time).Should().Be(first);
        FluentActions.Invoking(() => store.Reserve(
                id, PostingTemplateKind.PayoutReservation,
                new CoinAmount(CurrencyCode.HardCoin, 4), Limits(10, 2), Time))
            .Should().Throw<RiskDecisionReuseException>();
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), PostingTemplateKind.PayoutReservation,
                new CoinAmount(CurrencyCode.HardCoin, 1), Limits(10, 1), Time.AddMinutes(1)))
            .Should().Throw<StaleRiskCounterException>();
    }

    [Fact]
    public void ExpiredVelocityWindowNoLongerConsumesTheLimit()
    {
        var store = new AggregateRiskCounterStore();
        store.Reserve(
            Guid.NewGuid(), PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.SoftCoin, 10), Limits(10), Time);

        store.Reserve(
            Guid.NewGuid(), PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.SoftCoin, 10), Limits(10), Time.AddHours(1).AddTicks(1));

        store.Reservations.Should().HaveCount(2);
    }

    [Fact]
    public void ContractsExposeEveryRequiredAggregateDimensionAndValidateInputs()
    {
        Enum.GetValues<RiskLimitDimension>().Should().HaveCount(10);
        FluentActions.Invoking(() => new RiskLimitKey((RiskLimitDimension)99, "subject"))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new AggregateRiskLimit(
                new RiskLimitKey(RiskLimitDimension.Wallet, "wallet"), 1, 1, TimeSpan.Zero))
            .Should().Throw<ArgumentOutOfRangeException>();

        var store = new AggregateRiskCounterStore();
        var amount = new CoinAmount(CurrencyCode.HardCoin, 1);
        FluentActions.Invoking(() => store.Reserve(
                Guid.Empty, PostingTemplateKind.Spend, amount, Limits(1), Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), (PostingTemplateKind)99, amount, Limits(1), Time))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), PostingTemplateKind.Spend, amount, [], Time))
            .Should().Throw<ArgumentException>();
        var duplicate = new AggregateRiskLimit(
            new RiskLimitKey(RiskLimitDimension.Wallet, "wallet"), 1, 1, TimeSpan.FromHours(1));
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), PostingTemplateKind.Spend, amount, [duplicate, duplicate], Time))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DifferentOperationCurrencyAndExpiredWindowDoNotShareAllocatedCapacity()
    {
        var store = new AggregateRiskCounterStore();
        var limits = Limits(10);

        store.Reserve(Guid.NewGuid(), PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.HardCoin, 10), limits, Time);
        store.Reserve(Guid.NewGuid(), PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 10), limits, Time);
        store.Reserve(Guid.NewGuid(), PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.SoftCoin, 10), limits, Time);
        store.Reserve(Guid.NewGuid(), PostingTemplateKind.Spend,
            new CoinAmount(CurrencyCode.HardCoin, 10), limits, Time.AddHours(1).AddTicks(1));

        store.Reservations.Should().HaveCount(4);
    }

    private static AggregateRiskLimit[] Limits(long maxUnits, long version = 1) =>
    [
        new(new RiskLimitKey(RiskLimitDimension.Wallet, "wallet"), version, maxUnits, TimeSpan.FromHours(1)),
        new(new RiskLimitKey(RiskLimitDimension.IdentityCluster, "identity"), version, maxUnits, TimeSpan.FromHours(1)),
        new(new RiskLimitKey(RiskLimitDimension.GlobalLossBudget, "global"), version, maxUnits, TimeSpan.FromHours(1))
    ];
}
