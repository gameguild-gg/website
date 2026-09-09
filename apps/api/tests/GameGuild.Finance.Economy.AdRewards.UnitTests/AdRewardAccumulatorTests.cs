using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardAccumulatorTests
{
    private static readonly WalletId Wallet = new(Guid.Parse("41000000-0000-0000-0000-000000000004"));
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void Accrue_UsesExactFixedParityAndOneFinalDivision()
    {
        var accumulator = new AdRewardRationalAccumulator();

        var quote = accumulator.Accrue(Wallet, new IdempotencyKey("reward-1"), Policy(), 1);

        quote.RewardSoftUnits.Should().Be(112);
        quote.EstimatedNetEcpmUsdNanos.Should().Be(2_000_000_000);
        quote.ContractedRevenueSharePpm.Should().Be(700_000);
        quote.SafetyBufferPpm.Should().Be(200_000);
        quote.FixedSoftCoinsPerUsd.Should().Be(100_000);
        quote.PreviousRemainder.Should().Be(0);
        quote.NextRemainder.Should().Be(0);
    }

    [Fact]
    public void Accrue_PreservesSplitVersusBatchEntitlementAndRemainder()
    {
        var split = new AdRewardRationalAccumulator();
        var batch = new AdRewardRationalAccumulator();
        var policy = Policy(ecpm: 1_234_567_891, share: 654_321, buffer: 123_456);

        var first = split.Accrue(Wallet, new IdempotencyKey("split-1"), policy, 1);
        var second = split.Accrue(Wallet, new IdempotencyKey("split-2"), policy, 1);
        var combined = batch.Accrue(Wallet, new IdempotencyKey("batch"), policy, 2);

        (first.RewardSoftUnits + second.RewardSoftUnits).Should().Be(combined.RewardSoftUnits);
        split.RemainderFor(Wallet).Should().Be(batch.RemainderFor(Wallet));
    }

    [Fact]
    public void Accrue_RetainsWalletRemainderAcrossNetworkAndPolicyChanges()
    {
        var accumulator = new AdRewardRationalAccumulator();
        var first = accumulator.Accrue(
            Wallet,
            new IdempotencyKey("first"),
            Policy(ecpm: 1, share: 1, buffer: 999_998),
            1);
        var nextPolicy = Policy(network: "admob", version: 2, ecpm: 1, share: 1, buffer: 999_998);

        var second = accumulator.Accrue(Wallet, new IdempotencyKey("second"), nextPolicy, 1);

        first.RewardSoftUnits.Should().Be(0);
        second.PreviousRemainder.Should().Be(first.NextRemainder);
        accumulator.RemainderFor(Wallet).Should().Be(second.NextRemainder);
    }

    [Fact]
    public void Accrue_IsIdempotentAndRejectsKeyReuseWithDifferentInput()
    {
        var accumulator = new AdRewardRationalAccumulator();
        var key = new IdempotencyKey("same");
        var first = accumulator.Accrue(Wallet, key, Policy(), 1);

        accumulator.Accrue(Wallet, key, Policy(), 1).Should().Be(first);
        accumulator.RemainderFor(Wallet).Should().Be(first.NextRemainder);
        FluentActions.Invoking(() => accumulator.Accrue(Wallet, key, Policy(), 2))
            .Should().Throw<AdRewardIdempotencyConflictException>();
    }

    [Fact]
    public void Preview_IsNonMutatingIdempotentAndRejectsConflictingCommittedInput()
    {
        var accumulator = new AdRewardRationalAccumulator();
        var key = new IdempotencyKey("preview");
        FluentActions.Invoking(() => accumulator.Preview(Wallet, key, null!, 1))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => accumulator.Preview(Wallet, key, Policy(), 0))
            .Should().Throw<ArgumentOutOfRangeException>();

        var preview = accumulator.Preview(Wallet, key, Policy(), 1);
        accumulator.RemainderFor(Wallet).Should().Be(0);
        var committed = accumulator.Accrue(Wallet, key, Policy(), 1);

        accumulator.Preview(Wallet, key, Policy(), 1).Should().Be(committed);
        FluentActions.Invoking(() => accumulator.Preview(Wallet, key, Policy(), 2))
            .Should().Throw<AdRewardIdempotencyConflictException>();
        preview.Should().BeEquivalentTo(committed);
        committed.WalletId.Should().Be(Wallet);
        committed.IdempotencyKey.Should().Be(key);
        committed.Network.Should().Be("unity");
        committed.PolicyVersion.Should().Be(new PolicyVersion(1));
        committed.ImpressionCount.Should().Be(1);
        committed.InputFingerprint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Accrue_RejectsNonPositiveBatchCapAndOverflow()
    {
        var accumulator = new AdRewardRationalAccumulator();
        FluentActions.Invoking(() => accumulator.Accrue(Wallet, new IdempotencyKey("zero"), Policy(), 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => accumulator.Accrue(
                Wallet,
                new IdempotencyKey("cap"),
                Policy(maximumReward: 100),
                1))
            .Should().Throw<AdRewardLimitExceededException>();
        FluentActions.Invoking(() => accumulator.Accrue(
                Wallet,
                new IdempotencyKey("overflow"),
                Policy(ecpm: long.MaxValue, share: 1_000_000, buffer: 0, maximumReward: long.MaxValue),
                long.MaxValue))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void Calculate_RejectsEveryOutOfRangeRemainder()
    {
        FluentActions.Invoking(() => AdRewardRationalAccumulator.Calculate(
                Wallet, new IdempotencyKey("negative-remainder"), Policy(), 1, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => AdRewardRationalAccumulator.Calculate(
                Wallet, new IdempotencyKey("large-remainder"), Policy(), 1,
                AdRewardRationalAccumulator.CanonicalDenominator))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static AdNetworkPolicy Policy(
        string network = "unity",
        long version = 1,
        long ecpm = 2_000_000_000,
        int share = 700_000,
        int buffer = 200_000,
        long maximumReward = 1_000) => new(
        network,
        new PolicyVersion(version),
        Now.AddHours(-1),
        Now.AddHours(1),
        AdRewardIssuanceMode.ImmediateProviderProof,
        AdNetworkYieldState.Trailing,
        ecpm,
        share,
        buffer,
        900_000,
        TimeSpan.FromSeconds(3),
        maximumReward,
        Now,
        TimeSpan.FromHours(24),
        100);
}
