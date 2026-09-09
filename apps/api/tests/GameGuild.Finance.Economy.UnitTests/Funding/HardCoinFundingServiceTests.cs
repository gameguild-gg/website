using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class HardCoinFundingServiceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void ObserveTopUp_StoresVisiblePendingClaimWithoutMintOrLot()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);

        var claim = service.ObserveTopUp(Observe());

        claim.IsPending.Should().BeTrue();
        claim.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 2_500));
        store.FundingClaims.Should().ContainSingle();
        store.PendingFundingClaims.Should().ContainSingle();
        store.SourceEvidenceHistory.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
        store.JournalEntries.Should().BeEmpty();
        store.CreditLots.Should().BeEmpty();
        store.ProjectionUpdates.Should().BeEmpty();
    }

    [Fact]
    public void ObserveTopUp_RejectsProviderMonetaryLegReuse()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Observe();
        service.ObserveTopUp(command);

        FluentActions.Invoking(() => service.ObserveTopUp(command with { SourceId = SourceStampId.New() }))
            .Should().Throw<DuplicateProviderMonetaryLegException>();
        store.FundingClaims.Should().ContainSingle();
    }

    [Fact]
    public void PendingTopUp_CannotAuthorizeValueMovement()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var pending = service.ObserveTopUp(Observe());

        FluentActions.Invoking(() => service.Transfer(new TransferFragmentsCommand(
                PostingId.New(),
                new IdempotencyKey("pre-confirmation-spend"),
                pending.WalletId,
                WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1),
                ProvenanceKind.PurchasedHard,
                new ReserveVersion(1),
                new PolicyVersion(1),
                Time.AddMinutes(1))))
            .Should().Throw<InsufficientFragmentsException>();
    }

    [Fact]
    public void ConfirmObservedTopUp_ClosesPendingAndCreatesExactlyOneRootMint()
    {
        var (store, service, claim) = Setup();

        var result = service.ConfirmObservedTopUp(Confirm(claim));

        result.Status.Should().Be(PostingStatus.Accepted);
        store.PendingFundingClaims.Should().BeEmpty();
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Confirmed);
        store.SourceEvidenceHistory.Select(item => item.State).Should().Equal(
            SourceConfirmationState.Observed,
            SourceConfirmationState.Confirmed);
        store.JournalEntries.Should().ContainSingle();
        store.CreditLots.Should().ContainSingle().Which.Should().Match<CreditLot>(lot =>
            lot.Amount == claim.Amount &&
            lot.WalletId == claim.WalletId &&
            lot.Provenance == ProvenanceKind.PurchasedHard);
        store.ProjectionUpdates.Should().ContainSingle().Which.DeltaUnits.Should().Be(claim.Amount.Units);
    }

    [Fact]
    public void ConfirmObservedTopUp_DuplicateWebhookIsIdempotent()
    {
        var (store, service, claim) = Setup();
        var command = Confirm(claim);

        var first = service.ConfirmObservedTopUp(command);
        var duplicate = service.ConfirmObservedTopUp(command);

        duplicate.Should().Be(first);
        store.JournalEntries.Should().ContainSingle();
        store.CreditLots.Should().ContainSingle();
        store.FundingClaims.Should().ContainSingle().Which.Events.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(SourceConfirmationState.Failed)]
    [InlineData(SourceConfirmationState.Expired)]
    public void FinalizeObservedTopUp_RecordsTerminalEvidenceWithoutMonetaryPosting(SourceConfirmationState state)
    {
        var (store, service, claim) = Setup();

        var terminal = service.FinalizeObservedTopUp(new FinalizeObservedTopUpCommand(
            claim.SourceId,
            state,
            "provider-terminal-evidence",
            Time.AddMinutes(2)));

        terminal.State.Should().Be(state);
        store.PendingFundingClaims.Should().BeEmpty();
        store.SourceEvidenceHistory.Select(item => item.State).Should().Equal(
            SourceConfirmationState.Observed,
            state);
        store.JournalEntries.Should().BeEmpty();
        store.CreditLots.Should().BeEmpty();
        store.ProjectionUpdates.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmObservedTopUp_MissingObservedEvidenceFailsClosedAndRollsBack()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Observe();
        var claim = HardCoinFundingClaim.Observe(
            command.SourceId,
            command.WalletId,
            command.ProviderLeg,
            command.Evidence,
            command.AuthoritativeUsdMinorUnits,
            command.ObservedAt);
        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            return true;
        });
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.ConfirmObservedTopUp(Confirm(claim)))
            .Should().Throw<InvalidOperationException>().WithMessage("*Observed source evidence*");

        store.SnapshotCounts().Should().Be(before);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
        store.SourceEvidenceHistory.Should().BeEmpty();
    }

    [Fact]
    public void FinalizeObservedTopUp_MissingObservedEvidenceFailsClosedAndRollsBack()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Observe();
        var claim = HardCoinFundingClaim.Observe(
            command.SourceId,
            command.WalletId,
            command.ProviderLeg,
            command.Evidence,
            command.AuthoritativeUsdMinorUnits,
            command.ObservedAt);
        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            return true;
        });
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.FinalizeObservedTopUp(new FinalizeObservedTopUpCommand(
                claim.SourceId,
                SourceConfirmationState.Failed,
                "provider-failure",
                Time.AddMinutes(2))))
            .Should().Throw<InvalidOperationException>().WithMessage("*Observed source evidence*");

        store.SnapshotCounts().Should().Be(before);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
        store.SourceEvidenceHistory.Should().BeEmpty();
    }

    [Fact]
    public void FinalizeObservedTopUp_RejectsConfirmedAsATerminalFailureAndRollsBack()
    {
        var (store, service, claim) = Setup();
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.FinalizeObservedTopUp(new FinalizeObservedTopUpCommand(
                claim.SourceId,
                SourceConfirmationState.Confirmed,
                "invalid-terminal-confirmation",
                Time.AddMinutes(2))))
            .Should().Throw<InvalidFundingStateTransitionException>();

        store.SnapshotCounts().Should().Be(before);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
        store.SourceEvidenceHistory.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
    }

    [Fact]
    public void ConfirmObservedTopUp_RecoversAfterRetryableProviderTimeout()
    {
        var (store, service, claim) = Setup();

        store.PendingFundingClaims.Should().ContainSingle();
        var result = service.ConfirmObservedTopUp(Confirm(claim, Time.AddHours(2)) with
        {
            Evidence = "provider-confirmed-after-timeout"
        });

        result.Status.Should().Be(PostingStatus.Accepted);
        store.PendingFundingClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task TerminalRace_AllowsExactlyOneOutcomeAndAtMostOneRootMint()
    {
        var (store, service, claim) = Setup();
        var actions = new Func<bool>[]
        {
            () => Complete(() => service.ConfirmObservedTopUp(Confirm(claim))),
            () => Complete(() => service.FinalizeObservedTopUp(new FinalizeObservedTopUpCommand(
                claim.SourceId, SourceConfirmationState.Failed, "failure", Time.AddMinutes(2)))),
            () => Complete(() => service.FinalizeObservedTopUp(new FinalizeObservedTopUpCommand(
                claim.SourceId, SourceConfirmationState.Expired, "expiry", Time.AddMinutes(3))))
        };

        var outcomes = await Task.WhenAll(actions.Select(action => Task.Run(action)));

        outcomes.Should().ContainSingle(value => value);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().BeOneOf(
            SourceConfirmationState.Confirmed,
            SourceConfirmationState.Failed,
            SourceConfirmationState.Expired);
        store.CreditLots.Should().HaveCountLessThanOrEqualTo(1);
        store.JournalEntries.Should().HaveCountLessThanOrEqualTo(1);
    }

    private static bool Complete(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (FundingTerminalStateConflictException)
        {
            return false;
        }
    }

    private static (InMemoryLedgerKernelStore Store, TransactionalPostingService Service, HardCoinFundingClaim Claim) Setup()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        return (store, service, service.ObserveTopUp(Observe()));
    }

    private static ObserveHardCoinTopUpCommand Observe() => new(
        SourceStampId.New(),
        WalletId.New(),
        new ProviderMonetaryLeg("stripe", "live", "acct_gameguild", $"pi_{Guid.NewGuid():N}", "capture"),
        "provider-observation",
        2_500,
        Time);

    private static ConfirmObservedTopUpCommand Confirm(
        HardCoinFundingClaim claim,
        DateTimeOffset? recoveredAt = null)
    {
        var idempotencyKey = new IdempotencyKey("provider-confirmation");
        var confirmedAt = recoveredAt ?? Time.AddMinutes(1);
        return new ConfirmObservedTopUpCommand(
            PostingId.New(),
            idempotencyKey,
            claim.SourceId,
            CreditLotId.New(),
            new ReserveVersion(1),
            new PolicyVersion(1),
            "provider-confirmation-evidence",
            confirmedAt,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint,
                idempotencyKey,
                claim.WalletId,
                claim.Amount,
                [claim.SourceId],
                confirmedAt));
    }
}
