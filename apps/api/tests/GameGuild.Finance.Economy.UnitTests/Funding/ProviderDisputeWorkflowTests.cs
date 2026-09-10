using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class ProviderDisputeWorkflowTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-08-03T12:00:00Z");

    [Fact]
    public void OpenDispute_FreezesExactCurrentDescendantsAcrossWalletsAndCurrencies()
    {
        var fixture = Setup(10);
        fixture.Posting.ConvertHardToSoft(Convert(fixture, 4));
        var recipient = WalletId.New();
        fixture.Posting.Transfer(Transfer(fixture.WalletId, recipient, 2));
        var unrelated = fixture.Posting.ObserveTopUp(Observe(fixture.WalletId, 3));
        fixture.Posting.ConfirmObservedTopUp(Confirm(unrelated));

        var opened = fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open", 1, 7, ProviderDisputeStatus.Open));

        opened.Status.Should().Be(ProviderDisputeStatus.Open);
        opened.FrozenHardEquivalentUnits.Should().Be(7);
        fixture.Store.DisputeFragmentFreezes.Should().OnlyContain(item => item.Status == HoldStatus.Active);
        fixture.Store.DisputeFragmentFreezes.SelectMany(item => item.Ranges)
            .Should().OnlyContain(range => range.Root == fixture.SourceId);
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.SoftCoin).Should().BeEmpty();
        fixture.Store.GetAvailableLots(recipient, CurrencyCode.HardCoin).Should().BeEmpty();
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.HardCoin)
            .Sum(lot => lot.Amount.Units).Should().Be(6,
                "three units of the disputed root and all three units of the unrelated root remain available");
        fixture.Store.FundingClaims.Single(claim => claim.SourceId == fixture.SourceId)
            .State.Should().Be(SourceConfirmationState.Disputed);
    }

    [Fact]
    public void WonDispute_ReleasesExactFreezesAndRestoresConfirmedFundingState()
    {
        var fixture = Setup(10);
        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open", 1, 6, ProviderDisputeStatus.Open));

        var won = fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-won", 2, 6, ProviderDisputeStatus.Won));

        won.Status.Should().Be(ProviderDisputeStatus.Won);
        won.Reversal.Should().BeNull();
        fixture.Store.DisputeFragmentFreezes.Should().OnlyContain(item => item.Status == HoldStatus.Released);
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.HardCoin)
            .Sum(lot => lot.Amount.Units).Should().Be(10);
        fixture.Store.FundingClaims.Single().State.Should().Be(SourceConfirmationState.Confirmed);
        fixture.Store.ProviderReversalStates.Should().BeEmpty();
    }

    [Fact]
    public void LostDispute_ConsumesFrozenRangesPostsOffsetsAndRestrictsResponsibleDebt()
    {
        var fixture = Setup(10);
        fixture.Posting.ConvertHardToSoft(Convert(fixture, 4, fee: 1));
        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open", 1, 10, ProviderDisputeStatus.Open));

        var lost = fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-lost", 2, 10, ProviderDisputeStatus.Lost));

        lost.Status.Should().Be(ProviderDisputeStatus.Lost);
        lost.Reversal.Should().NotBeNull();
        lost.Reversal!.State.PartitionedHardEquivalentUnits.Should().Be(10);
        lost.Reversal.State.RecoveredHardUnits.Should().Be(5);
        lost.Reversal.State.RecoveredConvertedSoftUnits.Should().Be(4_000);
        lost.Reversal.State.ResponsibleDebtHardUnits.Should().Be(1);
        fixture.Store.DisputeFragmentFreezes.Should().OnlyContain(item => item.Status == HoldStatus.Consumed);
        fixture.Store.GetDebt(fixture.WalletId).OutstandingHardUnits.Should().Be(1);

        FluentActions.Invoking(() => fixture.Posting.Transfer(
                Transfer(fixture.WalletId, WalletId.New(), 1)))
            .Should().Throw<WalletDebtRestrictionException>();
        FluentActions.Invoking(() => fixture.Posting.ConvertHardToSoft(Convert(fixture, 1)))
            .Should().Throw<WalletDebtRestrictionException>();
        fixture.Store.ProviderDisputes.Should().ContainSingle();
        fixture.Store.DebtEvents.Should().ContainSingle().Which.OutstandingHardUnits.Should().Be(1);
    }

    [Fact]
    public void ProviderEvents_AreIdempotentConflictSafeAndStrictlyOrdered()
    {
        var fixture = Setup(10);
        var openedEvent = Notification(
            fixture.SourceId, "evt-open", 2, 4, ProviderDisputeStatus.Open);

        var opened = fixture.Disputes.Handle(openedEvent);
        fixture.Disputes.Handle(openedEvent).Should().BeEquivalentTo(opened);
        fixture.Store.ProviderDisputeEvents.Should().ContainSingle();

        FluentActions.Invoking(() => fixture.Disputes.Handle(openedEvent with
            {
                CumulativeDisputedHardUnits = 5
            }))
            .Should().Throw<ProviderDisputeEventConflictException>();
        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-stale", 1, 5, ProviderDisputeStatus.Open)))
            .Should().Throw<StaleProviderDisputeEventException>();
        fixture.Store.ProviderDisputeEvents.Should().ContainSingle();
        fixture.Store.DisputeFragmentFreezes.Sum(item => item.HardEquivalentUnits).Should().Be(4);
    }

    [Fact]
    public void DirectReversal_UsesRootFenceAndLeavesNoMutationWhenAnotherReversalOwnsIt()
    {
        var fixture = Setup(5);
        var epoch = fixture.Fences.BeginReversal(fixture.SourceId);
        try
        {
            FluentActions.Invoking(() => fixture.Posting.ReverseTopUp(Reversal(fixture.SourceId, 5)))
                .Should().Throw<InvalidOperationException>();
            fixture.Store.ProviderReversalStates.Should().BeEmpty();
            fixture.Store.FundingClaims.Single().State.Should().Be(SourceConfirmationState.Confirmed);
        }
        finally
        {
            fixture.Fences.CompleteReversal(fixture.SourceId, epoch);
        }
    }

    [Fact]
    public void OpenDispute_CumulativeUpdatesFreezeOnlyTheDeltaAndRejectRegression()
    {
        var fixture = Setup(10);
        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open-1", 1, 3, ProviderDisputeStatus.Open));

        var increased = fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open-2", 2, 7, ProviderDisputeStatus.Open));

        increased.CumulativeDisputedHardUnits.Should().Be(7);
        increased.FrozenHardEquivalentUnits.Should().Be(7);
        fixture.Store.DisputeFragmentFreezes.Sum(item => item.HardEquivalentUnits).Should().Be(7);
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.HardCoin)
            .Sum(lot => lot.Amount.Units).Should().Be(3);

        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-open-regression", 3, 6, ProviderDisputeStatus.Open)))
            .Should().Throw<ProviderMonetaryTotalExceededException>();
        fixture.Store.ProviderDisputeEvents.Should().HaveCount(2);
    }

    [Fact]
    public void OpenDispute_RejectsTerminalFundingAndAmountsBeyondTheConfirmedValue()
    {
        var terminalFixture = Setup(5);
        terminalFixture.Store.Execute(transaction =>
        {
            var claim = transaction.CurrentFundingClaim(terminalFixture.SourceId);
            transaction.UpdateFundingClaim(claim.Transition(
                SourceConfirmationState.Reversed,
                "provider-reversal",
                Time.AddMinutes(2)));
            return 0;
        });

        FluentActions.Invoking(() => terminalFixture.Disputes.Handle(Notification(
                terminalFixture.SourceId, "evt-terminal-funding", 1, 1, ProviderDisputeStatus.Open)))
            .Should().Throw<InvalidFundingStateTransitionException>();

        var exceededFixture = Setup(5);
        FluentActions.Invoking(() => exceededFixture.Disputes.Handle(Notification(
                exceededFixture.SourceId, "evt-exceeds-confirmed", 1, 6, ProviderDisputeStatus.Open)))
            .Should().Throw<ProviderMonetaryTotalExceededException>();
    }

    [Fact]
    public void OpenDisputeRejectsAConfirmedClaimWithoutSourceEvidence()
    {
        var fixture = SetupFundingWithoutSourceEvidence(SourceConfirmationState.Confirmed);

        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-missing-confirmed-source", 1, 1, ProviderDisputeStatus.Open)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Confirmed source evidence was not found*");
    }

    [Fact]
    public void WonDisputeRejectsADisputedClaimWithoutSourceEvidence()
    {
        var fixture = SetupFundingWithoutSourceEvidence(SourceConfirmationState.Disputed);
        fixture.Store.Execute(transaction =>
        {
            transaction.SetProviderDisputeCase(new ProviderDisputeCase(
                "dp-primary", fixture.SourceId, fixture.WalletId, ProviderDisputeStatus.Open,
                1, 1, 0, 0, [], null, Time));
            return 0;
        });

        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-missing-disputed-source", 2, 1, ProviderDisputeStatus.Won)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Disputed source evidence was not found*");
    }

    [Fact]
    public void OpenDispute_SortsPersistedReversalHistoryBeforePlanningTheIncrement()
    {
        var fixture = Setup(10);
        var first = new RootTraceRange(fixture.SourceId, 0, 1_000, 0);
        var second = new RootTraceRange(fixture.SourceId, 1_000, 1_000, 0);
        fixture.Store.Execute(transaction =>
        {
            transaction.SetProviderReversalState(new ProviderReversalState(
                fixture.SourceId,
                10,
                2,
                0,
                0,
                0,
                0,
                [second, first]));
            return 0;
        });

        var opened = fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-history", 1, 3, ProviderDisputeStatus.Open));

        opened.CumulativeDisputedHardUnits.Should().Be(3);
        opened.BaselineReversedHardUnits.Should().Be(2);
    }

    [Fact]
    public void OpenDispute_RejectsAnAmountBelowTheCommittedReversalBaseline()
    {
        var fixture = Setup(5);
        fixture.Store.Execute(transaction =>
        {
            transaction.SetProviderReversalState(new ProviderReversalState(
                fixture.SourceId,
                5,
                2,
                0,
                0,
                0,
                0,
                []));
            return 0;
        });

        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-below-baseline", 1, 1, ProviderDisputeStatus.Open)))
            .Should().Throw<ProviderMonetaryTotalExceededException>()
            .WithMessage("*cannot precede already committed reversals*");
    }

    [Fact]
    public void WonDispute_RejectsAProviderReversalStateThatDisagreesWithItsBaseline()
    {
        var fixture = Setup(5);
        fixture.Store.Execute(transaction =>
        {
            transaction.SetProviderDisputeCase(new ProviderDisputeCase(
                "dp-primary",
                fixture.SourceId,
                fixture.WalletId,
                ProviderDisputeStatus.Open,
                1,
                3,
                0,
                0,
                [],
                null,
                Time));
            transaction.SetProviderReversalState(new ProviderReversalState(
                fixture.SourceId,
                5,
                1,
                0,
                0,
                0,
                0,
                []));
            return 0;
        });

        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-inconsistent-won", 2, 3, ProviderDisputeStatus.Won)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*committed reversal value cannot be marked won*");
    }

    [Fact]
    public void OpenDispute_AfterMaturityStillFreezesExactSourceFragments()
    {
        var fixture = Setup(4);
        var matureEvent = Notification(
            fixture.SourceId, "evt-mature", 1, 4, ProviderDisputeStatus.Open) with
        {
            OccurredAt = Time.AddDays(121)
        };

        fixture.Disputes.Handle(matureEvent);

        fixture.Store.DisputeFragmentFreezes.Should().OnlyContain(item => item.Status == HoldStatus.Active);
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.HardCoin).Should().BeEmpty();
    }

    [Fact]
    public void WonDispute_WithPriorPartialReversalReleasesOnlyIncrementAndKeepsSourceDisputed()
    {
        var fixture = Setup(10);
        fixture.Posting.ReverseTopUp(Reversal(fixture.SourceId, 2));
        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open", 1, 5, ProviderDisputeStatus.Open) with
        {
            OccurredAt = Time.AddMinutes(11)
        });

        var won = fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-won", 2, 5, ProviderDisputeStatus.Won) with
        {
            OccurredAt = Time.AddMinutes(12)
        });

        won.BaselineReversedHardUnits.Should().Be(2);
        fixture.Store.DisputeFragmentFreezes.Sum(item => item.HardEquivalentUnits).Should().Be(3);
        fixture.Store.DisputeFragmentFreezes.Should().OnlyContain(item => item.Status == HoldStatus.Released);
        fixture.Store.FundingClaims.Single().State.Should().Be(SourceConfirmationState.Disputed);
        fixture.Store.SourceEvidenceHistory[^1].State.Should().Be(SourceConfirmationState.Disputed);
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.HardCoin)
            .Sum(lot => lot.Amount.Units).Should().Be(8);
    }

    [Fact]
    public void ProviderEvents_NormalizeIdentifiersAndRejectWrongSourceOrTerminalReuse()
    {
        var fixture = Setup(5);
        var padded = Notification(
            fixture.SourceId, " evt-open ", 1, 3, ProviderDisputeStatus.Open) with
        {
            ProviderDisputeReference = " dp-primary "
        };
        var opened = fixture.Disputes.Handle(padded);

        fixture.Disputes.Handle(padded with
            {
                ProviderEventId = "evt-open",
                ProviderDisputeReference = "dp-primary"
            })
            .Should().BeEquivalentTo(opened);
        fixture.Store.ProviderDisputeEvents.Should().ContainSingle();

        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                SourceStampId.New(), "evt-wrong-root", 2, 3, ProviderDisputeStatus.Open)))
            .Should().Throw<ProviderDisputeEventConflictException>();

        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-won", 2, 3, ProviderDisputeStatus.Won));
        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-reopen", 3, 4, ProviderDisputeStatus.Open)))
            .Should().Throw<ProviderDisputeTerminalStateException>();
    }

    [Fact]
    public void TerminalDispute_RequiresAnOpenCaseAndTheSameCumulativeAmount()
    {
        var fixture = Setup(5);
        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-won", 1, 3, ProviderDisputeStatus.Won)))
            .Should().Throw<KeyNotFoundException>();

        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-open", 2, 3, ProviderDisputeStatus.Open));
        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-lost", 3, 4, ProviderDisputeStatus.Lost)))
            .Should().Throw<ProviderMonetaryTotalExceededException>();
    }

    [Fact]
    public void DisputeFreeze_RejectsInvalidIdentityStateTimingRangesAndParity()
    {
        var root = SourceStampId.New();
        var range = new RootTraceRange(root, 0, 1_000, 0);
        var valid = new DisputeFragmentFreeze(
            Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 1), [range], HoldStatus.Active, Time, null);

        FluentActions.Invoking(() => valid.Transition(HoldStatus.Active, Time.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => valid.Transition(HoldStatus.Released, Time.AddMinutes(-1)))
            .Should().Throw<ArgumentException>();
        var released = valid.Transition(HoldStatus.Released, Time.AddMinutes(1));
        FluentActions.Invoking(() => released.Transition(HoldStatus.Consumed, Time.AddMinutes(2)))
            .Should().Throw<InvalidOperationException>();

        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.Empty, "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1), [range], HoldStatus.Active, Time, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1),
                [new RootTraceRange(SourceStampId.New(), 0, 1_000, 0)], HoldStatus.Active, Time, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1), [], HoldStatus.Active, Time, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1), [range], (HoldStatus)99, Time, null))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 1), [range], HoldStatus.Released, Time, null))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.SoftCoin, 1),
                [new RootTraceRange(root, 0, 1, 0)], HoldStatus.Active, Time, null))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => new DisputeFragmentFreeze(
                Guid.NewGuid(), "dp", root, CreditLotId.New(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 2), [range], HoldStatus.Active, Time, null))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void KernelStore_EnforcesDisputeEventFreezeAndDebtInvariantsTransactionally()
    {
        var store = new InMemoryLedgerKernelStore();
        var root = SourceStampId.New();
        var wallet = WalletId.New();
        FluentActions.Invoking(() => store.GetProviderDisputeCase("missing"))
            .Should().Throw<KeyNotFoundException>();
        store.GetDebt(wallet).Should().Be(new WalletDebtPosition(wallet, 0, DateTimeOffset.MinValue));

        var disputeEvent = new ProviderDisputeEventRecord(
            "evt", "dp", root, 1, ProviderDisputeStatus.Open, 1, "hash", Time);
        var freeze = new DisputeFragmentFreeze(
            Guid.NewGuid(), "dp", root, CreditLotId.New(), wallet,
            new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RootTraceRange(root, 0, 1_000, 0)], HoldStatus.Active, Time, null);

        store.Execute(transaction =>
        {
            transaction.AddProviderDisputeEvent(disputeEvent);
            transaction.AddDisputeFreeze(freeze);
            transaction.RecordDebt(wallet, root, 0, Time);
            transaction.RecordDebt(wallet, root, 2, Time);
            transaction.FindProviderDisputeEvent("evt").Should().Be(disputeEvent);
            return 0;
        });

        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.AddProviderDisputeEvent(disputeEvent);
                return 0;
            }))
            .Should().Throw<ProviderDisputeEventConflictException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.AddDisputeFreeze(freeze);
                return 0;
            }))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.RecordDebt(wallet, root, -3, Time);
                return 0;
            }))
            .Should().Throw<InvalidOperationException>();
        store.DebtEvents.Should().ContainSingle();
    }

    [Fact]
    public void KernelStore_RejectsRootSliceThatDoesNotResolveToWholeCoinUnits()
    {
        var store = new InMemoryLedgerKernelStore();
        var firstRoot = SourceStampId.New();
        var secondRoot = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            ProvenanceKind.PurchasedHard, Time, Time, 1, CreditLotState.Active,
            [
                new RootTraceRange(firstRoot, 0, 500, 0),
                new RootTraceRange(secondRoot, 0, 500, 0)
            ],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
        store.Execute(transaction =>
        {
            transaction.AddCreditLot(lot);
            return 0;
        });

        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.GetAvailableRootLots(firstRoot);
                return 0;
            }))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void SourceEvidence_DisputeResolutionRejectsWrongStateAndBackdating()
    {
        var observed = SourceEvidence.Observe(SourceStampId.New(), "stripe", "pi", "evidence", Time);
        FluentActions.Invoking(() => observed.ResolveDispute(Time.AddMinutes(1)))
            .Should().Throw<InvalidOperationException>();
        var disputed = observed.Confirm(Time.AddMinutes(1)).Dispute(Time.AddMinutes(2));
        FluentActions.Invoking(() => disputed.ResolveDispute(Time.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        disputed.ResolveDispute(Time.AddMinutes(3)).State.Should().Be(SourceConfirmationState.Confirmed);
    }

    [Fact]
    public void Workflow_RejectsInvalidNotificationsAndTerminalFollowups()
    {
        var fixture = Setup(5);
        var valid = Notification(fixture.SourceId, "evt-open", 1, 3, ProviderDisputeStatus.Open);

        FluentActions.Invoking(() => fixture.Disputes.Handle(valid with
            {
                Status = (ProviderDisputeStatus)99
            }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => fixture.Disputes.Handle(valid with
            {
                IrrecoverableDisposition = (ProviderReversalDisposition)99
            }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => fixture.Disputes.Handle(valid with
            {
                ProviderSequence = 0
            }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => fixture.Disputes.Handle(valid with
            {
                CumulativeDisputedHardUnits = 0
            }))
            .Should().Throw<ArgumentOutOfRangeException>();

        fixture.Disputes.Handle(valid);
        fixture.Disputes.Handle(Notification(
            fixture.SourceId, "evt-won", 2, 3, ProviderDisputeStatus.Won));
        FluentActions.Invoking(() => fixture.Disputes.Handle(Notification(
                fixture.SourceId, "evt-terminal", 3, 3, ProviderDisputeStatus.Lost)))
            .Should().Throw<ProviderDisputeTerminalStateException>();
    }

    [Fact]
    public void Workflow_RejectsNullDependencies()
    {
        var store = new InMemoryLedgerKernelStore();
        var fences = new RootReversalFenceRegistry();
        var posting = new TransactionalPostingService(store, fences: fences);

        FluentActions.Invoking(() => new ProviderDisputeWorkflow(null!, posting, fences))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ProviderDisputeWorkflow(store, null!, fences))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ProviderDisputeWorkflow(store, posting, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DisputeRecordsAndFreeze_ExposeImmutableAuditState()
    {
        var root = SourceStampId.New();
        var wallet = WalletId.New();
        var lot = CreditLotId.New();
        var range = new RootTraceRange(root, 0, CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0);
        var notification = new ProviderDisputeNotification(
            "evt", "dp", root, 1, 1, ProviderDisputeStatus.Open,
            ProviderReversalDisposition.ResponsibleDebt, "evidence",
            new ReserveVersion(2), new PolicyVersion(3), Time);
        var eventRecord = new ProviderDisputeEventRecord(
            notification.ProviderEventId, notification.ProviderDisputeReference, root, 1,
            ProviderDisputeStatus.Open, 1, "hash", Time);
        var freeze = new DisputeFragmentFreeze(
            Guid.NewGuid(), "dp", root, lot, wallet,
            new CoinAmount(CurrencyCode.HardCoin, 1), [range], HoldStatus.Active, Time, null);
        var debt = new WalletDebtEvent(2, wallet, root, 3, 5, Time);
        var dispute = new ProviderDisputeCase(
            "dp", root, wallet, ProviderDisputeStatus.Open, 1, 1, 0, 1,
            [freeze.Id], null, Time);

        notification.ReserveVersion.Should().Be(new ReserveVersion(2));
        notification.PolicyVersion.Should().Be(new PolicyVersion(3));
        eventRecord.ProviderEventId.Should().Be("evt");
        eventRecord.ProviderDisputeReference.Should().Be("dp");
        eventRecord.SourceId.Should().Be(root);
        eventRecord.ProviderSequence.Should().Be(1);
        eventRecord.Status.Should().Be(ProviderDisputeStatus.Open);
        eventRecord.CumulativeDisputedHardUnits.Should().Be(1);
        eventRecord.RequestHash.Should().Be("hash");
        eventRecord.OccurredAt.Should().Be(Time);
        freeze.Id.Should().NotBe(Guid.Empty);
        freeze.ProviderDisputeReference.Should().Be("dp");
        freeze.RootSourceId.Should().Be(root);
        freeze.LotId.Should().Be(lot);
        freeze.WalletId.Should().Be(wallet);
        freeze.Amount.Units.Should().Be(1);
        freeze.Ranges.Should().ContainSingle().Which.Should().Be(range);
        freeze.Status.Should().Be(HoldStatus.Active);
        freeze.PlacedAt.Should().Be(Time);
        freeze.TerminalAt.Should().BeNull();
        freeze.HardEquivalentUnits.Should().Be(1);
        debt.Sequence.Should().Be(2);
        debt.WalletId.Should().Be(wallet);
        debt.SourceId.Should().Be(root);
        debt.DeltaHardUnits.Should().Be(3);
        debt.OutstandingHardUnits.Should().Be(5);
        debt.OccurredAt.Should().Be(Time);
        dispute.ProviderDisputeReference.Should().Be("dp");
        dispute.SourceId.Should().Be(root);
        dispute.ResponsibleWalletId.Should().Be(wallet);
        dispute.FreezeIds.Should().ContainSingle().Which.Should().Be(freeze.Id);
        dispute.UpdatedAt.Should().Be(Time);
    }
    private static Fixture Setup(long hardUnits)
    {
        var store = new InMemoryLedgerKernelStore();
        var fences = new RootReversalFenceRegistry();
        var posting = new TransactionalPostingService(store, fences: fences);
        var wallet = WalletId.New();
        var claim = posting.ObserveTopUp(Observe(wallet, hardUnits));
        posting.ConfirmObservedTopUp(Confirm(claim));
        return new Fixture(
            store,
            fences,
            posting,
            new ProviderDisputeWorkflow(store, posting, fences),
            wallet,
            claim.SourceId);
    }

    private static Fixture SetupFundingWithoutSourceEvidence(SourceConfirmationState state)
    {
        var store = new InMemoryLedgerKernelStore();
        var fences = new RootReversalFenceRegistry();
        var posting = new TransactionalPostingService(store, fences: fences);
        var wallet = WalletId.New();
        var source = SourceStampId.New();
        var claim = HardCoinFundingClaim.Observe(
                source,
                wallet,
                new ProviderMonetaryLeg("stripe", "live", "acct_gameguild", $"pi_{Guid.NewGuid():N}", "capture"),
                "provider-observation",
                1,
                Time)
            .Transition(SourceConfirmationState.Confirmed, "provider-confirmation", Time.AddSeconds(1));
        if (state == SourceConfirmationState.Disputed)
            claim = claim.Transition(SourceConfirmationState.Disputed, "provider-dispute", Time.AddSeconds(2));

        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            return 0;
        });
        return new Fixture(
            store,
            fences,
            posting,
            new ProviderDisputeWorkflow(store, posting, fences),
            wallet,
            source);
    }

    private static ProviderDisputeNotification Notification(
        SourceStampId sourceId,
        string eventId,
        long sequence,
        long cumulativeHardUnits,
        ProviderDisputeStatus status) => new(
        eventId,
        "dp-primary",
        sourceId,
        sequence,
        cumulativeHardUnits,
        status,
        ProviderReversalDisposition.ResponsibleDebt,
        $"provider-{status.ToString().ToLowerInvariant()}-evidence",
        new ReserveVersion(1),
        new PolicyVersion(1),
        Time.AddMinutes(sequence));

    private static ObserveHardCoinTopUpCommand Observe(WalletId wallet, long units) => new(
        SourceStampId.New(),
        wallet,
        new ProviderMonetaryLeg("stripe", "live", "acct_gameguild", $"pi_{Guid.NewGuid():N}", "capture"),
        "provider-observation",
        units,
        Time);

    private static ConfirmObservedTopUpCommand Confirm(HardCoinFundingClaim claim)
    {
        var key = new IdempotencyKey($"confirm-{claim.SourceId.Value:N}");
        var confirmedAt = Time.AddSeconds(1);
        return new ConfirmObservedTopUpCommand(
            PostingId.New(), key, claim.SourceId, CreditLotId.New(),
            new ReserveVersion(1), new PolicyVersion(1), "provider-confirmation", confirmedAt,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint, key, claim.WalletId, claim.Amount,
                [claim.SourceId], confirmedAt));
    }

    private static ConvertHardToSoftCommand Convert(Fixture fixture, long principal, long fee = 0)
    {
        var key = new IdempotencyKey($"convert-{Guid.NewGuid():N}");
        var requestedAt = Time.AddSeconds(2);
        return new ConvertHardToSoftCommand(
            PostingId.New(), PostingId.New(), key, fixture.WalletId, CreditLotId.New(),
            principal, fee, new ReserveVersion(1), new PolicyVersion(1), requestedAt,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.HardToSoftConversion,
                key,
                fixture.WalletId,
                new CoinAmount(CurrencyCode.HardCoin, principal + fee),
                [fixture.SourceId],
                requestedAt,
                new CoinAmount(CurrencyCode.SoftCoin, principal * 1_000)));
    }

    private static TransferFragmentsCommand Transfer(
        WalletId source,
        WalletId destination,
        long units) => new(
        PostingId.New(),
        new IdempotencyKey($"transfer-{Guid.NewGuid():N}"),
        source,
        destination,
        new CoinAmount(CurrencyCode.HardCoin, units),
        ProvenanceKind.PurchasedHard,
        new ReserveVersion(1),
        new PolicyVersion(1),
        Time.AddSeconds(3));

    private static ReverseTopUpCommand Reversal(SourceStampId sourceId, long cumulativeHardUnits) => new(
        PostingId.New(),
        new IdempotencyKey($"reversal-{Guid.NewGuid():N}"),
        sourceId,
        cumulativeHardUnits,
        ProviderReversalDisposition.ResponsibleDebt,
        "provider-reversal",
        new ReserveVersion(1),
        new PolicyVersion(1),
        Time.AddMinutes(10));

    private sealed record Fixture(
        InMemoryLedgerKernelStore Store,
        RootReversalFenceRegistry Fences,
        TransactionalPostingService Posting,
        ProviderDisputeWorkflow Disputes,
        WalletId WalletId,
        SourceStampId SourceId);
}
