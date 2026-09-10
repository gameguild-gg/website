using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class HardCoinFundingLifecycleTests
{
    private static readonly DateTimeOffset ObservedAt = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void ProviderMonetaryLeg_NormalizesAndBuildsGloballyUniqueIdentity()
    {
        var leg = new ProviderMonetaryLeg(" stripe ", " live ", " acct_1 ", " pi_1 ", " capture ");

        leg.Provider.Should().Be("stripe");
        leg.Environment.Should().Be("live");
        leg.ConnectedAccount.Should().Be("acct_1");
        leg.ProviderObject.Should().Be("pi_1");
        leg.MonetaryLeg.Should().Be("capture");
        leg.Key.Should().Be("stripe\u001flive\u001facct_1\u001fpi_1\u001fcapture");
    }

    [Theory]
    [InlineData("", "live", "acct", "pi", "capture")]
    [InlineData("stripe", "", "acct", "pi", "capture")]
    [InlineData("stripe", "live", "", "pi", "capture")]
    [InlineData("stripe", "live", "acct", "", "capture")]
    [InlineData("stripe", "live", "acct", "pi", "")]
    public void ProviderMonetaryLeg_RejectsIncompleteIdentity(
        string provider,
        string environment,
        string account,
        string providerObject,
        string monetaryLeg)
    {
        FluentActions.Invoking(() => new ProviderMonetaryLeg(
                provider, environment, account, providerObject, monetaryLeg))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProviderMonetaryLeg_RejectsIdentityThatExceedsPersistentSourceReferenceLimit()
    {
        FluentActions.Invoking(() => new ProviderMonetaryLeg(
                new string('p', 252), "live", "acct", "pi", "capture"))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(10_000, 10_000)]
    public void HardCoinFundingAmount_MapsUsdMinorUnitsOneToOne(long usdMinorUnits, long hardCoinUnits)
    {
        HardCoinFundingAmount.FromUsdMinorUnits(usdMinorUnits)
            .Should().Be(new CoinAmount(CurrencyCode.HardCoin, hardCoinUnits));
    }

    [Fact]
    public void HardCoinFundingClaim_IsVisiblePendingWithoutMonetaryCredit()
    {
        var claim = HardCoinFundingClaim.Observe(
            SourceStampId.New(),
            WalletId.New(),
            new ProviderMonetaryLeg("stripe", "live", "acct_1", "pi_1", "capture"),
            "provider-payload-hash",
            1_250,
            ObservedAt);

        claim.State.Should().Be(SourceConfirmationState.Observed);
        claim.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 1_250));
        claim.IsPending.Should().BeTrue();
        claim.TerminalAt.Should().BeNull();
        var observed = claim.Events.Should().ContainSingle().Subject;
        observed.State.Should().Be(SourceConfirmationState.Observed);
        observed.Sequence.Should().Be(1);
        observed.EvidenceHash.Should().HaveLength(64);
    }

    [Theory]
    [InlineData(SourceConfirmationState.Confirmed)]
    [InlineData(SourceConfirmationState.Failed)]
    [InlineData(SourceConfirmationState.Expired)]
    public void HardCoinFundingClaim_ObservedCanReachExactlyOneTerminalState(SourceConfirmationState state)
    {
        var claim = CreateClaim();
        var terminalAt = ObservedAt.AddMinutes(1);

        var terminal = claim.Transition(state, "terminal-evidence", terminalAt);

        terminal.State.Should().Be(state);
        terminal.IsPending.Should().BeFalse();
        terminal.TerminalAt.Should().Be(terminalAt);
        terminal.Events.Select(item => item.State).Should().Equal(SourceConfirmationState.Observed, state);
        FluentActions.Invoking(() => terminal.Transition(
                SourceConfirmationState.Failed,
                "second-terminal-evidence",
                terminalAt.AddMinutes(1)))
            .Should().Throw<FundingTerminalStateConflictException>();
    }

    [Theory]
    [InlineData(SourceConfirmationState.Observed)]
    [InlineData(SourceConfirmationState.Disputed)]
    [InlineData(SourceConfirmationState.Reversed)]
    public void HardCoinFundingClaim_RejectsInvalidInitialTerminalState(SourceConfirmationState state)
    {
        var claim = CreateClaim();

        FluentActions.Invoking(() => claim.Transition(state, "evidence", ObservedAt.AddMinutes(1)))
            .Should().Throw<InvalidFundingStateTransitionException>();
    }

    [Fact]
    public void HardCoinFundingClaim_ConfirmedCanRecordDisputeThenReversalEvidence()
    {
        var confirmed = CreateClaim().Transition(
            SourceConfirmationState.Confirmed,
            "confirmation",
            ObservedAt.AddMinutes(1));

        var disputed = confirmed.Transition(
            SourceConfirmationState.Disputed,
            "chargeback-opened",
            ObservedAt.AddDays(5));
        var reversed = disputed.Transition(
            SourceConfirmationState.Reversed,
            "chargeback-settled",
            ObservedAt.AddDays(6));

        disputed.State.Should().Be(SourceConfirmationState.Disputed);
        reversed.State.Should().Be(SourceConfirmationState.Reversed);
        reversed.Events.Select(item => item.State).Should().Equal(
            SourceConfirmationState.Observed,
            SourceConfirmationState.Confirmed,
            SourceConfirmationState.Disputed,
            SourceConfirmationState.Reversed);
    }

    [Fact]
    public void HardCoinFundingClaim_RejectsMutatedOrBackdatedEvidence()
    {
        var claim = CreateClaim();

        FluentActions.Invoking(() => claim.Transition(
                SourceConfirmationState.Confirmed,
                " ",
                ObservedAt.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => claim.Transition(
                SourceConfirmationState.Confirmed,
                "confirmation",
                ObservedAt.AddTicks(-1)))
            .Should().Throw<ArgumentException>();
    }

    private static HardCoinFundingClaim CreateClaim() => HardCoinFundingClaim.Observe(
        SourceStampId.New(),
        WalletId.New(),
        new ProviderMonetaryLeg("stripe", "live", "acct_1", $"pi_{Guid.NewGuid():N}", "capture"),
        "observed-evidence",
        1_000,
        ObservedAt);
}
