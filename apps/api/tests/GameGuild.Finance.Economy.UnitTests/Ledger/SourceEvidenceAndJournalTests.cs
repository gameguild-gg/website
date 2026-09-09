using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Posting;
using GameGuild.Finance.Economy.UnitTests.Posting;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class SourceEvidenceAndJournalTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-02-01T10:00:00Z");

    [Fact]
    public void Observe_CreatesHashedNonMonetaryEvidenceAndConfirmationIsImmutable()
    {
        var source = SourceEvidence.Observe(
            SourceStampId.New(), "stripe", "pi_123", "provider-payload", Time);

        source.State.Should().Be(SourceConfirmationState.Observed);
        source.ConfirmedAt.Should().BeNull();
        source.EvidenceHash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");

        var same = SourceEvidence.Observe(source.Id, "stripe", "pi_123", "provider-payload", Time);
        var confirmed = source.Confirm(Time.AddMinutes(1));

        same.EvidenceHash.Should().Be(source.EvidenceHash);
        confirmed.State.Should().Be(SourceConfirmationState.Confirmed);
        confirmed.ConfirmedAt.Should().Be(Time.AddMinutes(1));
        source.State.Should().Be(SourceConfirmationState.Observed);
        source.ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public void Evidence_RejectsInvalidTransitionsAndMalformedInputs()
    {
        var source = SourceEvidence.Observe(SourceStampId.New(), "stripe", "pi_123", "payload", Time);

        FluentActions.Invoking(() => source.Confirm(Time.AddSeconds(-1))).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => source.Reverse(Time.AddMinutes(1))).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => SourceEvidence.Observe(SourceStampId.New(), " ", "ref", "payload", Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SourceEvidence.Observe(SourceStampId.New(), "stripe", " ", "payload", Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SourceEvidence.Observe(SourceStampId.New(), "stripe", "ref", " ", Time))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConfirmedSource_CreatesRootLotButObservedSourceCannotMint()
    {
        var observed = SourceEvidence.Observe(SourceStampId.New(), "stripe", "pi_123", "payload", Time);

        FluentActions.Invoking(() => ConfirmedCreditFactory.CreateRootLot(
                CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
                ProvenanceKind.PurchasedHard, observed, Time.AddDays(120), 1))
            .Should().Throw<InvalidOperationException>();

        var confirmed = observed.Confirm(Time.AddMinutes(1));
        var lot = ConfirmedCreditFactory.CreateRootLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.PurchasedHard, confirmed, confirmed.ConfirmedAt!.Value.AddDays(120), 1);

        lot.ConfirmedAt.Should().Be(confirmed.ConfirmedAt);
        lot.OriginalMaturesAt.Should().Be(confirmed.ConfirmedAt!.Value.AddDays(120));
        lot.TraceUnitsPerCoinUnit.Should().Be(CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
        lot.Ranges.Should().Equal(new RootTraceRange(confirmed.Id, 0, 10_000, 0));
    }

    [Fact]
    public void ConfirmedSource_CanBecomeReversedWithoutMutatingPriorEvidence()
    {
        var confirmed = SourceEvidence
            .Observe(SourceStampId.New(), "stripe", "pi_123", "payload", Time)
            .Confirm(Time.AddMinutes(1));

        var reversed = confirmed.Reverse(Time.AddDays(2));

        reversed.State.Should().Be(SourceConfirmationState.Reversed);
        reversed.ReversedAt.Should().Be(Time.AddDays(2));
        confirmed.State.Should().Be(SourceConfirmationState.Confirmed);
        FluentActions.Invoking(() => confirmed.Reverse(Time)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Evidence_AllowsDisputeReplayAndRejectsBackdatedOrPostTerminalTransitions()
    {
        var observed = SourceEvidence.Observe(SourceStampId.New(), "stripe", "pi_states", "payload", Time);
        var confirmed = observed.Confirm(Time.AddMinutes(1));
        var disputed = confirmed.Dispute(Time.AddMinutes(2));

        disputed.Dispute(Time.AddMinutes(3)).State.Should().Be(SourceConfirmationState.Disputed);
        FluentActions.Invoking(() => observed.Dispute(Time.AddMinutes(1)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => confirmed.Dispute(Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => confirmed.Fail(Time.AddMinutes(2)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => observed.Fail(Time.AddTicks(-1)))
            .Should().Throw<ArgumentException>();
        observed.Fail(Time.AddMinutes(1)).State.Should().Be(SourceConfirmationState.Failed);
        observed.Expire(Time.AddMinutes(1)).State.Should().Be(SourceConfirmationState.Expired);
    }

    [Fact]
    public void JournalAppend_IsDeterministicAndBuildsVerifiableHashChain()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint);
        var firstChain = new JournalChain();
        var secondChain = new JournalChain();

        var first = firstChain.Append(request, Time);
        var second = firstChain.Append(PostingFixture.Valid(PostingTemplateKind.Spend), Time.AddSeconds(1));
        var replay = secondChain.Append(request, Time);

        first.Entry.Sequence.Should().Be(1);
        first.Entry.PreviousHash.Should().Be(JournalChain.GenesisHash);
        first.Entry.Hash.Should().Be(replay.Entry.Hash);
        first.Result.Hash.Should().Be(first.Entry.Hash);
        first.Result.Lines.Select(line => line.JournalLineId)
            .Should().Equal(replay.Result.Lines.Select(line => line.JournalLineId));
        second.Entry.PreviousHash.Should().Be(first.Entry.Hash);
        firstChain.Verify().Should().BeTrue();
        firstChain.Head.Should().Be(second.Entry);
    }

    [Fact]
    public void JournalAppend_RejectsInvalidPostingWithoutChangingHead()
    {
        var chain = new JournalChain();
        var invalid = PostingFixture.Valid(PostingTemplateKind.Spend) with
        {
            Authority = PostingAuthority.Administrator
        };

        FluentActions.Invoking(() => chain.Append(invalid, Time)).Should().Throw<PostingValidationException>();

        chain.Entries.Should().BeEmpty();
        chain.Head.Should().BeNull();
    }

    [Fact]
    public void JournalAppend_RejectsReusedPostingIdentity()
    {
        var chain = new JournalChain();
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        chain.Append(request, Time);

        FluentActions.Invoking(() => chain.Append(
                request with { IdempotencyKey = new IdempotencyKey("different-key") },
                Time.AddSeconds(1)))
            .Should().Throw<DuplicatePostingException>();
        chain.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task JournalAppend_SerializesConcurrentWritersIntoOneGlobalSequence()
    {
        var chain = new JournalChain();
        var requests = Enumerable.Range(1, 32)
            .Select(index => PostingFixture.Valid(PostingTemplateKind.Spend) with
            {
                Id = PostingId.New(),
                IdempotencyKey = new IdempotencyKey($"concurrent-{index}")
            })
            .ToArray();

        await Task.WhenAll(requests.Select((request, index) =>
            Task.Run(() => chain.Append(request, Time.AddTicks(index)))));

        chain.Entries.Select(entry => entry.Sequence).Should().Equal(Enumerable.Range(1, 32).Select(value => (long)value));
        chain.Entries.Select(entry => entry.Hash).Should().OnlyHaveUniqueItems();
        chain.Verify().Should().BeTrue();
    }
}
