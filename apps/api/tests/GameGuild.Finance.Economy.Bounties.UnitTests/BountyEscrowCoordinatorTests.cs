using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountyEscrowCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Post_SelectsPosterLotsInFifoOrderAndPreservesExactEscrowProvenance()
    {
        var harness = new Harness();
        var purchased = harness.Lot(6, ProvenanceKind.PurchasedHard, Now.AddDays(-20), 1);
        var earned = harness.Lot(8, ProvenanceKind.EarnedHard, Now.AddDays(-10), 2);

        var bounty = harness.Post(10, [earned, purchased]);

        bounty.Status.Should().Be(BountyStatus.Open);
        bounty.EscrowFragments.Select(fragment => fragment.ParentLot.Id)
            .Should().Equal(purchased.Id, earned.Id);
        bounty.EscrowFragments.Select(fragment => fragment.Amount.Units).Should().Equal(6, 4);
        bounty.EscrowFragments.Select(fragment => fragment.ParentLot.Provenance)
            .Should().Equal(ProvenanceKind.PurchasedHard, ProvenanceKind.EarnedHard);
        bounty.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges)
            .Should().Equal(purchased.Ranges.Concat([earned.Ranges[0].Take(4_000).Selected]));
    }

    [Fact]
    public void PositionFactory_SelectsOnlyConfirmedServerLotsWithoutCoordinatorState()
    {
        var harness = new Harness();
        var confirmed = harness.Lot(5, ProvenanceKind.PurchasedHard, Now.AddDays(-2), 1);
        var future = harness.Lot(5, ProvenanceKind.PurchasedHard, Now.AddHours(1), 2);

        var position = BountyEscrowPositionFactory.Create(harness.PostCommand(5, [future, confirmed]));

        position.Status.Should().Be(BountyStatus.Open);
        position.EscrowFragments.Should().ContainSingle();
        position.EscrowFragments[0].ParentLot.Id.Should().Be(confirmed.Id);
        position.EscrowFragments[0].Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 5));
    }

    [Fact]
    public void Claim_HardBountyCreatesFreshEarnedSourceAndIndependentMaturity()
    {
        var harness = new Harness();
        var oldMaturity = Now.AddDays(-200).AddDays(120);
        var posted = harness.Post(10,
            [harness.Lot(10, ProvenanceKind.PurchasedHard, Now.AddDays(-200), 1, oldMaturity)]);

        var result = harness.Claim(posted, Now.AddHours(1));

        result.Status.Should().Be(BountyStatus.Claimed);
        result.ProceedsSource.Id.Should().NotBe(posted.EscrowFragments[0].ParentLot.Ranges[0].Root);
        result.ProceedsSource.State.Should().Be(SourceConfirmationState.Confirmed);
        result.ProceedsSource.ConfirmedAt.Should().Be(Now.AddHours(1));
        result.ProceedsLot.Provenance.Should().Be(ProvenanceKind.EarnedHard);
        result.ProceedsLot.ConfirmedAt.Should().Be(Now.AddHours(1));
        result.ProceedsLot.OriginalMaturesAt.Should().Be(Now.AddHours(1).AddDays(120));
        result.ProceedsLot.OriginalMaturesAt.Should().NotBe(oldMaturity);
        result.ProceedsLot.Ranges.Should().ContainSingle()
            .Which.Root.Should().Be(result.ProceedsSource.Id);
        result.FundingParents.SelectMany(parent => parent.Ranges)
            .Should().Equal(posted.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges));
    }

    [Fact]
    public void Claim_RepeatedBountiesCreateIndependentAuthoritativeCredits()
    {
        var harness = new Harness();
        var first = harness.Post(4, [harness.Lot(4, ProvenanceKind.PurchasedHard, Now.AddDays(-180), 1)]);
        var second = harness.Post(4, [harness.Lot(4, ProvenanceKind.PurchasedHard, Now.AddDays(-90), 2)]);

        var firstCredit = harness.Claim(first, Now.AddHours(1));
        var secondCredit = harness.Claim(second, Now.AddHours(2));

        firstCredit.ProceedsSource.Id.Should().NotBe(secondCredit.ProceedsSource.Id);
        firstCredit.ProceedsLot.Id.Should().NotBe(secondCredit.ProceedsLot.Id);
        firstCredit.ProceedsLot.ConfirmedAt.Should().Be(Now.AddHours(1));
        secondCredit.ProceedsLot.ConfirmedAt.Should().Be(Now.AddHours(2));
        firstCredit.ProceedsLot.OriginalMaturesAt.Should().Be(Now.AddHours(1).AddDays(120));
        secondCredit.ProceedsLot.OriginalMaturesAt.Should().Be(Now.AddHours(2).AddDays(120));
    }

    [Fact]
    public void Claim_SoftBountyCreatesFreshNonCashableProceedsWithoutMaturityDelay()
    {
        var harness = new Harness(CurrencyCode.SoftCoin);
        var posted = harness.Post(250,
            [harness.Lot(250, ProvenanceKind.AdRewardSoft, Now.AddDays(-2), 1)]);

        var result = harness.Claim(posted, Now.AddMinutes(30));

        result.ProceedsLot.Amount.Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 250));
        result.ProceedsLot.Provenance.Should().Be(ProvenanceKind.EscrowReturn);
        result.ProceedsLot.ConfirmedAt.Should().Be(Now.AddMinutes(30));
        result.ProceedsLot.OriginalMaturesAt.Should().Be(Now.AddMinutes(30));
        result.ProceedsLot.TraceUnitsPerCoinUnit.Should().Be(CurrencyTraceScale.SoftCoinTraceUnitsPerCoin);
    }

    [Fact]
    public void Reclaim_RestoresOriginalProvenanceAndRoutesExactFeeLineage()
    {
        var harness = new Harness(reclaimFeePpm: 200_000);
        var purchased = harness.Lot(6, ProvenanceKind.PurchasedHard, Now.AddDays(-40), 1);
        var earned = harness.Lot(4, ProvenanceKind.EarnedHard, Now.AddDays(-30), 2);
        var bounty = harness.Post(10, [purchased, earned], expiresAt: Now.AddHours(1));

        var result = harness.Reclaim(bounty, Now.AddHours(1));

        result.Status.Should().Be(BountyStatus.Reclaimed);
        result.ReturnedAmount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 8));
        result.FeeAmount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 2));
        result.RestoredLots.Select(lot => lot.Provenance)
            .Should().Equal(ProvenanceKind.PurchasedHard, ProvenanceKind.EarnedHard);
        result.RestoredLots.Select(lot => lot.Amount.Units).Should().Equal(6, 2);
        result.RestoredLots[0].ConfirmedAt.Should().Be(purchased.ConfirmedAt);
        result.RestoredLots[0].OriginalMaturesAt.Should().Be(purchased.OriginalMaturesAt);
        result.FeeLots.Should().ContainSingle().Which.Amount.Units.Should().Be(2);
        var outputByRoot = result.RestoredLots.SelectMany(lot => lot.Ranges)
            .Concat(result.FeeLots.SelectMany(lot => lot.Ranges))
            .GroupBy(range => range.Root)
            .ToDictionary(group => group.Key, group => group.Sum(range => range.Length));
        var inputByRoot = bounty.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges)
            .GroupBy(range => range.Root)
            .ToDictionary(group => group.Key, group => group.Sum(range => range.Length));
        outputByRoot.Should().Equal(inputByRoot);
    }

    [Fact]
    public void Reclaim_ZeroFeeRestoresEveryDepositedFragmentAndNoFeeLot()
    {
        var harness = new Harness(reclaimFeePpm: 0);
        var source = harness.Lot(3, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var bounty = harness.Post(3, [source], expiresAt: Now.AddMinutes(1));

        var result = harness.Reclaim(bounty, Now.AddMinutes(1));

        result.FeeAmount.Units.Should().Be(0);
        result.FeeLots.Should().BeEmpty();
        result.RestoredLots.Should().ContainSingle().Which.Provenance.Should().Be(source.Provenance);
        result.RestoredLots[0].Ranges.Should().Equal(source.Ranges);
    }

    [Fact]
    public void TerminalCommands_AreIdempotentButRejectConflictingOutcomes()
    {
        var harness = new Harness();
        var bounty = harness.Post(2, [harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1)]);
        var command = harness.ClaimCommand(bounty, Now.AddMinutes(1), "claim-stable");

        var first = harness.Coordinator.Claim(command);
        var duplicate = harness.Coordinator.Claim(command);

        duplicate.Should().BeSameAs(first);
        FluentActions.Invoking(() => harness.Reclaim(bounty, Now.AddDays(8)))
            .Should().Throw<BountyTerminalConflictException>();
    }

    [Fact]
    public async Task ClaimAndReclaimRace_HasExactlyOneTerminalWinner()
    {
        var harness = new Harness();
        var bounty = harness.Post(5, [harness.Lot(5, ProvenanceKind.PurchasedHard, Now, 1)],
            expiresAt: Now.AddHours(1));
        using var start = new ManualResetEventSlim(false);

        var claim = Task.Run(() => Attempt(() =>
        {
            start.Wait();
            return (object)harness.Coordinator.Claim(
                harness.ClaimCommand(bounty, Now.AddHours(1).AddTicks(-1), "race-claim"));
        }));
        var reclaim = Task.Run(() => Attempt(() =>
        {
            start.Wait();
            return (object)harness.Coordinator.Reclaim(
                harness.ReclaimCommand(bounty, Now.AddHours(1), "race-reclaim"));
        }));
        start.Set();

        var outcomes = await Task.WhenAll(claim, reclaim);

        outcomes.Count(outcome => outcome.Result is not null).Should().Be(1);
        outcomes.Count(outcome => outcome.Error is BountyTerminalConflictException).Should().Be(1);
        harness.Coordinator.Get(bounty.Id).Status.Should().BeOneOf(BountyStatus.Claimed, BountyStatus.Reclaimed);
    }

    [Fact]
    public void Claim_RejectsExpiredOrIneligibleClaimWithoutClosingEscrow()
    {
        var harness = new Harness();
        var bounty = harness.Post(2, [harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1)],
            requirements: new BountyEligibilityRequirements(true, 50, true),
            expiresAt: Now.AddHours(1));

        var ineligible = harness.ClaimCommand(bounty, Now.AddMinutes(1)) with
        {
            Eligibility = new BountyEligibilitySnapshot(
                harness.ClaimantId, false, 49, false, Now, Now.AddHours(2))
        };
        FluentActions.Invoking(() => harness.Coordinator.Claim(ineligible))
            .Should().Throw<BountyClaimIneligibleException>();
        harness.Coordinator.Get(bounty.Id).Status.Should().Be(BountyStatus.Open);

        FluentActions.Invoking(() => harness.Claim(bounty, Now.AddHours(1)))
            .Should().Throw<BountyExpiredException>();
        harness.Coordinator.Get(bounty.Id).Status.Should().Be(BountyStatus.Open);
        harness.Coordinator.GetStatus(bounty.Id, Now.AddHours(1)).Should().Be(BountyStatus.Expired);
    }

    [Fact]
    public void Claim_RequiresBoundRiskApprovalAndEveryAbuseExposure()
    {
        var harness = new Harness();
        var bounty = harness.Post(2, [harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1)]);
        var valid = harness.ClaimCommand(bounty, Now.AddMinutes(1));

        foreach (var type in BountyClaimRiskGate.RequiredEntityTypes)
        {
            var nodes = valid.Risk.EntityCluster.Nodes.Where(node => node.Type != type).ToArray();
            var approval = valid.Risk with { EntityCluster = valid.Risk.EntityCluster with { Nodes = nodes } };
            FluentActions.Invoking(() => harness.Coordinator.Claim(valid with { Risk = approval }))
                .Should().Throw<BountyRiskExposureException>();
        }

        foreach (var dimension in BountyClaimRiskGate.RequiredLimitDimensions)
        {
            var limits = valid.Risk.Limits.Where(limit => limit.Key.Dimension != dimension).ToArray();
            FluentActions.Invoking(() => harness.Coordinator.Claim(
                    valid with { Risk = valid.Risk with { Limits = limits } }))
                .Should().Throw<BountyRiskExposureException>();
        }

        harness.Coordinator.Get(bounty.Id).Status.Should().Be(BountyStatus.Open);
        harness.Coordinator.Claim(valid).Status.Should().Be(BountyStatus.Claimed);
        harness.RiskCounters.Reservations.Should().ContainSingle();
    }

    [Fact]
    public void PostAndReclaim_EnforceOwnershipExpiryIdempotencyAndRootFences()
    {
        var harness = new Harness();
        var foreign = harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1, walletId: WalletId.New());
        FluentActions.Invoking(() => harness.Post(2, [foreign]))
            .Should().Throw<InsufficientFragmentsException>();

        var source = harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 2);
        var key = new IdempotencyKey("same-post");
        var command = harness.PostCommand(2, [source], key: key);
        var posted = harness.Coordinator.Post(command);
        harness.Coordinator.Post(command).Should().BeSameAs(posted);
        FluentActions.Invoking(() => harness.Coordinator.Post(
                harness.PostCommand(2, [source], key: key) with { Id = BountyId.New() }))
            .Should().Throw<BountyIdempotencyConflictException>();
        FluentActions.Invoking(() => harness.Coordinator.Reclaim(
                harness.ReclaimCommand(posted, Now.AddMinutes(1))))
            .Should().Throw<BountyNotExpiredException>();
        FluentActions.Invoking(() => harness.Coordinator.Reclaim(
                harness.ReclaimCommand(posted, Now.AddDays(8)) with { PosterId = Guid.NewGuid() }))
            .Should().Throw<BountyOwnershipException>();

        var root = source.Ranges[0].Root;
        harness.Fences.BeginReversal(root);
        FluentActions.Invoking(() => harness.Claim(posted, Now.AddMinutes(2)))
            .Should().Throw<RootReversalInProgressException>();
        harness.Coordinator.Get(posted.Id).Status.Should().Be(BountyStatus.Open);
    }

    [Fact]
    public void EligibilityPolicy_RejectsEveryInvalidSnapshotAndAcceptsFreshEvidence()
    {
        var policy = new BountyEligibilityPolicy();
        var claimant = Guid.NewGuid();
        var requirements = new BountyEligibilityRequirements(true, 10, true);
        var valid = new BountyEligibilitySnapshot(
            claimant, true, 10, true, Now.AddMinutes(-1), Now.AddMinutes(1));

        FluentActions.Invoking(() => policy.EnsureEligible(null!, valid, claimant, Now))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => policy.EnsureEligible(requirements, null!, claimant, Now))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => policy.EnsureEligible(requirements, valid, Guid.Empty, Now))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements, valid with { ClaimantId = Guid.NewGuid() }, claimant, Now))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements, valid with { ObservedAt = Now.AddSeconds(1) }, claimant, Now))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements, valid with { ExpiresAt = Now }, claimant, Now))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements,
                valid with { ObservedAt = Now.AddSeconds(-1), ExpiresAt = Now.AddSeconds(-2) },
                claimant, Now.AddMinutes(-1)))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements, valid with { PrerequisiteCompleted = false }, claimant, Now))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements, valid with { Reputation = 9 }, claimant, Now))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => policy.EnsureEligible(
                requirements, valid with { InstructorVerified = false }, claimant, Now))
            .Should().Throw<BountyClaimIneligibleException>();

        policy.Invoking(value => value.EnsureEligible(requirements, valid, claimant, Now))
            .Should().NotThrow();
    }

    [Fact]
    public void RiskGate_RejectsEveryMaterialContextMutation()
    {
        var harness = new Harness();
        var bounty = harness.Post(2, [harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1)]);
        var valid = harness.ClaimCommand(bounty, Now.AddMinutes(1));
        var context = valid.Risk.Context;
        var mutations = new ProtectedOperationContext[]
        {
            context with { ActorId = Guid.NewGuid() },
            context with { Operation = PostingTemplateKind.Spend },
            context with { SourceWalletId = WalletId.New() },
            context with { DestinationWalletId = WalletId.New() },
            context with { Amount = new CoinAmount(context.Amount.Currency, context.Amount.Units + 1) },
            context with { CurrencyLegs = [] },
            context with
            {
                CurrencyLegs =
                [
                    new RiskCurrencyLeg(context.Amount.Currency, context.Amount.Units + 1)
                ]
            },
            context with { SourceRoots = [] },
            context with { ProviderReferenceHash = "another-bounty" },
            context with { EntityGraphVersion = context.EntityGraphVersion + 1 },
            context with { EntityGraphEvidenceHash = "another-graph" }
        };

        foreach (var mutation in mutations)
        {
            var gate = new BountyClaimRiskGate(
                new RiskDecisionAuthorizer(), new AggregateRiskCounterStore());
            FluentActions.Invoking(() => gate.Authorize(
                    bounty,
                    valid.ClaimantId,
                    valid.ClaimantWalletId,
                    valid.Risk with { Context = mutation },
                    valid.ClaimedAt))
                .Should().Throw<BountyRiskExposureException>();
        }

        FluentActions.Invoking(() => new BountyClaimRiskGate(
                new RiskDecisionAuthorizer(), new AggregateRiskCounterStore()).Authorize(
                bounty, Guid.Empty, valid.ClaimantWalletId, valid.Risk, valid.ClaimedAt))
            .Should().Throw<BountyRiskExposureException>();
    }

    [Fact]
    public void RiskGate_RejectsUnboundRootsDestinationsCounterpartiesAndNullDependencies()
    {
        var harness = new Harness();
        var bounty = harness.Post(2, [harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1)]);
        var valid = harness.ClaimCommand(bounty, Now.AddMinutes(1));

        FluentActions.Invoking(() => new BountyClaimRiskGate(null!, new AggregateRiskCounterStore()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new BountyClaimRiskGate(new RiskDecisionAuthorizer(), null!))
            .Should().Throw<ArgumentNullException>();

        var gate = new BountyClaimRiskGate(
            new RiskDecisionAuthorizer(), new AggregateRiskCounterStore());
        FluentActions.Invoking(() => gate.Authorize(
                null!, valid.ClaimantId, valid.ClaimantWalletId, valid.Risk, valid.ClaimedAt))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gate.Authorize(
                bounty, valid.ClaimantId, valid.ClaimantWalletId, null!, valid.ClaimedAt))
            .Should().Throw<ArgumentNullException>();
        foreach (var approval in new[]
                 {
                     valid.Risk with { Decision = null! },
                     valid.Risk with { Context = null! },
                     valid.Risk with { EntityCluster = null! },
                     valid.Risk with { Limits = null! }
                 })
        {
            FluentActions.Invoking(() => gate.Authorize(
                    bounty, valid.ClaimantId, valid.ClaimantWalletId, approval, valid.ClaimedAt))
                .Should().Throw<ArgumentNullException>();
        }

        AssertLimitSubjectRejected(
            valid,
            bounty,
            RiskLimitDimension.SourceRoot,
            "wrong-root");
        AssertLimitSubjectRejected(
            valid,
            bounty,
            RiskLimitDimension.Destination,
            "wrong-destination");
        AssertLimitSubjectRejected(
            valid,
            bounty,
            RiskLimitDimension.CounterpartyPair,
            "wrong-counterparty");

        FluentActions.Invoking(() => BountyClaimRiskGate.CounterpartyPair(Guid.Empty, Guid.NewGuid()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => BountyClaimRiskGate.CounterpartyPair(Guid.NewGuid(), Guid.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContractsAndFeePolicy_RejectInvalidValuesAndExposeTerminalIdentity()
    {
        FluentActions.Invoking(() => new BountyId(Guid.Empty)).Should().Throw<ArgumentException>();
        BountyId.New().Value.Should().NotBeEmpty();
        FluentActions.Invoking(() => new BountyEligibilityRequirements(false, -1, false))
            .Should().Throw<ArgumentOutOfRangeException>();
        BountyFeePolicy.Calculate(long.MaxValue, 999_999).Should().Be(9_223_362_813_482_738_952);
        FluentActions.Invoking(() => BountyFeePolicy.Calculate(0, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => BountyFeePolicy.Calculate(1, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => BountyFeePolicy.Calculate(1, BountyFeePolicy.PartsPerMillion))
            .Should().Throw<ArgumentOutOfRangeException>();

        var harness = new Harness();
        var lot = harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1);
        var selection = FifoFragmentSelector.Select(
            [lot], new CoinAmount(CurrencyCode.HardCoin, 1)).Selections[0];
        FluentActions.Invoking(() => new BountyEscrowFragment(null!, selection))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new BountyEscrowFragment(
                lot, selection with { ParentLotId = CreditLotId.New() }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new BountyEscrowFragment(
                lot, selection with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 1) }))
            .Should().Throw<ArgumentException>();

        var bounty = harness.Post(2, [lot], expiresAt: Now.AddMinutes(1));
        bounty.PostedAt.Should().Be(Now);
        var claimHarness = new Harness();
        var claimBounty = claimHarness.Post(
            2, [claimHarness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1)]);
        claimHarness.Claim(claimBounty, Now.AddSeconds(1)).BountyId.Should().Be(claimBounty.Id);
        harness.Reclaim(bounty, Now.AddMinutes(1)).BountyId.Should().Be(bounty.Id);
    }

    [Fact]
    public void Coordinator_RejectsInvalidConstructionPostingAndUnknownBounties()
    {
        var decisions = new RiskDecisionAuthorizer();
        var counters = new AggregateRiskCounterStore();
        var risk = new BountyClaimRiskGate(decisions, counters);
        var fences = new RootReversalFenceRegistry();
        FluentActions.Invoking(() => new BountyEscrowCoordinator(null!, risk, fences))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new BountyEscrowCoordinator(
                new BountyEligibilityPolicy(), null!, fences))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new BountyEscrowCoordinator(
                new BountyEligibilityPolicy(), risk, null!))
            .Should().Throw<ArgumentNullException>();

        var harness = new Harness();
        var lot = harness.Lot(2, ProvenanceKind.PurchasedHard, Now, 1);
        var valid = harness.PostCommand(2, [lot]);
        FluentActions.Invoking(() => harness.Coordinator.Post(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(valid with { AvailableLots = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(valid with { Eligibility = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(valid with { PosterId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(
                valid with { EscrowWalletId = valid.PosterWalletId }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(
                valid with { ExpiresAt = valid.PostedAt }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(
                valid with { Amount = new CoinAmount(valid.Amount.Currency, 0) }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(
                valid with { ReclaimFeePpm = BountyFeePolicy.PartsPerMillion }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => harness.Coordinator.Post(
                valid with { AvailableLots = [lot, lot] }))
            .Should().Throw<ArgumentException>();

        var posted = harness.Coordinator.Post(valid);
        FluentActions.Invoking(() => harness.Coordinator.Post(
                valid with { IdempotencyKey = new IdempotencyKey("same-id-new-key") }))
            .Should().Throw<BountyIdempotencyConflictException>();
        FluentActions.Invoking(() => harness.Coordinator.Get(BountyId.New()))
            .Should().Throw<KeyNotFoundException>();
        posted.Status.Should().Be(BountyStatus.Open);
    }

    [Fact]
    public void ClaimAndReclaim_ValidateCommandsAliasesAndTerminalIdempotency()
    {
        var harness = new Harness();
        var bounty = harness.Post(
            3,
            [harness.Lot(3, ProvenanceKind.PurchasedHard, Now, 1)],
            expiresAt: Now.AddMinutes(2));
        var validClaim = harness.ClaimCommand(bounty, Now.AddMinutes(1), "terminal-key");

        FluentActions.Invoking(() => harness.Coordinator.Claim(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Claim(validClaim with { Eligibility = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Claim(validClaim with { Risk = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Claim(validClaim with { JournalSequence = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => harness.Coordinator.Claim(
                validClaim with { ClaimantId = harness.PosterId }))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => harness.Coordinator.Claim(
                validClaim with { ClaimantWalletId = harness.PosterWalletId }))
            .Should().Throw<BountyClaimIneligibleException>();
        FluentActions.Invoking(() => harness.Coordinator.Claim(
                validClaim with { ClaimantWalletId = harness.EscrowWalletId }))
            .Should().Throw<BountyClaimIneligibleException>();

        harness.Coordinator.Claim(validClaim);
        FluentActions.Invoking(() => harness.Coordinator.Reclaim(
                harness.ReclaimCommand(bounty, Now.AddMinutes(2), "terminal-key")))
            .Should().Throw<BountyIdempotencyConflictException>();
        harness.Coordinator.GetStatus(bounty.Id, Now.AddDays(1)).Should().Be(BountyStatus.Claimed);

        FluentActions.Invoking(() => harness.Coordinator.Reclaim(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Reclaim(
                harness.ReclaimCommand(bounty, Now.AddDays(1)) with { FirstJournalSequence = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TerminalIdempotencyKey_CannotMoveBetweenBounties()
    {
        var harness = new Harness();
        var first = harness.Post(
            3, [harness.Lot(3, ProvenanceKind.PurchasedHard, Now, 1)],
            expiresAt: Now.AddMinutes(3));
        var second = harness.Post(
            3, [harness.Lot(3, ProvenanceKind.PurchasedHard, Now, 2)],
            expiresAt: Now.AddMinutes(3));
        harness.Coordinator.Claim(harness.ClaimCommand(first, Now.AddMinutes(1), "cross-bounty-key"));

        FluentActions.Invoking(() => harness.Coordinator.Claim(
                harness.ClaimCommand(second, Now.AddMinutes(1), "cross-bounty-key")))
            .Should().Throw<BountyIdempotencyConflictException>();
    }

    [Fact]
    public void Reclaim_IsIdempotentAndSoftFeeCreditRemainsNonCashable()
    {
        var hard = new Harness();
        var hardBounty = hard.Post(
            4,
            [hard.Lot(4, ProvenanceKind.PurchasedHard, Now, 1)],
            expiresAt: Now.AddMinutes(1));
        var command = hard.ReclaimCommand(hardBounty, Now.AddMinutes(1), "stable-reclaim");
        var first = hard.Coordinator.Reclaim(command);
        hard.Coordinator.Reclaim(command).Should().BeSameAs(first);

        var soft = new Harness(CurrencyCode.SoftCoin, reclaimFeePpm: 500_000);
        var softBounty = soft.Post(
            4,
            [soft.Lot(4, ProvenanceKind.AdRewardSoft, Now, 1)],
            expiresAt: Now.AddMinutes(1));
        var result = soft.Reclaim(softBounty, Now.AddMinutes(1));

        result.FeeLots.Should().ContainSingle();
        result.FeeLots[0].Provenance.Should().Be(ProvenanceKind.EscrowReturn);
        result.FeeLots[0].ConfirmedAt.Should().Be(Now.AddMinutes(1));
        result.FeeLots[0].OriginalMaturesAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void RiskGate_AuthorizesBountyFundedByMultipleSourceRoots()
    {
        var harness = new Harness();
        var bounty = harness.Post(
            2,
            [
                harness.Lot(1, ProvenanceKind.PurchasedHard, Now, 1),
                harness.Lot(1, ProvenanceKind.PurchasedHard, Now, 2)
            ]);
        var command = harness.ClaimCommand(bounty, Now.AddMinutes(1));
        var authorization = new BountyClaimRiskGate(
            harness.RiskAuthorizer,
            harness.RiskCounters).Authorize(
                bounty,
                command.ClaimantId,
                command.ClaimantWalletId,
                command.Risk,
                command.ClaimedAt);

        authorization.Decision.DecisionId.Should().Be(command.Risk.Decision.Id);
    }

    private static void AssertLimitSubjectRejected(
        ClaimBountyCommand valid,
        BountyEscrowPosition bounty,
        RiskLimitDimension dimension,
        string subject)
    {
        var limits = valid.Risk.Limits.Select(limit =>
            limit.Key.Dimension == dimension
                ? new AggregateRiskLimit(
                    new RiskLimitKey(dimension, subject),
                    limit.CounterVersion,
                    limit.MaxUnits,
                    limit.Window)
                : limit).ToArray();
        var gate = new BountyClaimRiskGate(
            new RiskDecisionAuthorizer(), new AggregateRiskCounterStore());

        FluentActions.Invoking(() => gate.Authorize(
                bounty,
                valid.ClaimantId,
                valid.ClaimantWalletId,
                valid.Risk with { Limits = limits },
                valid.ClaimedAt))
            .Should().Throw<BountyRiskExposureException>();
    }
    private static AttemptResult Attempt(Func<object> action)
    {
        try { return new AttemptResult(action(), null); }
        catch (Exception error) { return new AttemptResult(null, error); }
    }

    private sealed record AttemptResult(object? Result, Exception? Error);

    private sealed class Harness
    {
        private long _journalSequence = 10;

        internal Harness(CurrencyCode currency = CurrencyCode.HardCoin, int reclaimFeePpm = 100_000)
        {
            Currency = currency;
            ReclaimFeePpm = reclaimFeePpm;
            Coordinator = new BountyEscrowCoordinator(
                new BountyEligibilityPolicy(),
                new BountyClaimRiskGate(RiskAuthorizer, RiskCounters),
                Fences);
        }

        internal CurrencyCode Currency { get; }
        internal int ReclaimFeePpm { get; }
        internal Guid PosterId { get; } = Guid.Parse("b1000000-0000-0000-0000-000000000001");
        internal Guid ClaimantId { get; } = Guid.Parse("b2000000-0000-0000-0000-000000000002");
        internal WalletId PosterWalletId { get; } = new(Guid.Parse("b3000000-0000-0000-0000-000000000003"));
        internal WalletId EscrowWalletId { get; } = new(Guid.Parse("b4000000-0000-0000-0000-000000000004"));
        internal WalletId ClaimantWalletId { get; } = new(Guid.Parse("b5000000-0000-0000-0000-000000000005"));
        internal WalletId FeeWalletId { get; } = new(Guid.Parse("b6000000-0000-0000-0000-000000000006"));
        internal RiskDecisionAuthorizer RiskAuthorizer { get; } = new();
        internal AggregateRiskCounterStore RiskCounters { get; } = new();
        internal RootReversalFenceRegistry Fences { get; } = new();
        internal BountyEscrowCoordinator Coordinator { get; }

        internal CreditLot Lot(
            long units,
            ProvenanceKind provenance,
            DateTimeOffset confirmedAt,
            long sequence,
            DateTimeOffset? maturity = null,
            WalletId? walletId = null)
        {
            var root = SourceStampId.New();
            var scale = CurrencyTraceScale.For(Currency);
            return new CreditLot(
                CreditLotId.New(), walletId ?? PosterWalletId, new CoinAmount(Currency, units), provenance,
                confirmedAt, maturity ?? confirmedAt.AddDays(Currency == CurrencyCode.HardCoin ? 120 : 0),
                sequence, CreditLotState.Active,
                [new RootTraceRange(root, 0, checked(units * scale), 0)], scale);
        }

        internal PostBountyCommand PostCommand(
            long units,
            IReadOnlyList<CreditLot> lots,
            BountyEligibilityRequirements? requirements = null,
            DateTimeOffset? expiresAt = null,
            IdempotencyKey? key = null) => new(
            BountyId.New(), PosterId, PosterWalletId, EscrowWalletId,
            new CoinAmount(Currency, units), lots,
            requirements ?? BountyEligibilityRequirements.None,
            ReclaimFeePpm, Now, expiresAt ?? Now.AddDays(7), key ?? new IdempotencyKey($"post-{Guid.NewGuid():N}"));

        internal BountyEscrowPosition Post(
            long units,
            IReadOnlyList<CreditLot> lots,
            BountyEligibilityRequirements? requirements = null,
            DateTimeOffset? expiresAt = null) =>
            Coordinator.Post(PostCommand(units, lots, requirements, expiresAt));

        internal ClaimBountyCommand ClaimCommand(
            BountyEscrowPosition bounty,
            DateTimeOffset claimedAt,
            string? key = null)
        {
            var roots = bounty.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var cluster = Cluster();
            var idempotency = new IdempotencyKey(key ?? $"claim-{Guid.NewGuid():N}");
            var context = new ProtectedOperationContext(
                idempotency, ClaimantId, PostingTemplateKind.Reclaim,
                EscrowWalletId, ClaimantWalletId, bounty.Amount,
                [new RiskCurrencyLeg(bounty.Amount.Currency, bounty.Amount.Units)], roots,
                BountyClaimRiskGate.ProviderReference(bounty.Id), new PolicyVersion(1),
                new ReserveVersion(1), 1, 1, cluster.Version, cluster.EvidenceHash);
            var decision = RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Allow, context,
                claimedAt.AddMinutes(-1), claimedAt.AddMinutes(1), [RiskReasonCode.WithinLimits]);
            var limits = new List<AggregateRiskLimit>
            {
                Limit(RiskLimitDimension.Wallet, EscrowWalletId.Value.ToString("N")),
                Limit(RiskLimitDimension.IdentityCluster, cluster.Id),
                Limit(RiskLimitDimension.Destination, ClaimantWalletId.Value.ToString("N")),
                Limit(RiskLimitDimension.CounterpartyPair,
                    BountyClaimRiskGate.CounterpartyPair(PosterId, ClaimantId)),
                Limit(RiskLimitDimension.DeviceIpAsnCluster, "device-cluster")
            };
            limits.AddRange(roots.Select(root =>
                Limit(RiskLimitDimension.SourceRoot, root.Value.ToString("N"))));
            return new ClaimBountyCommand(
                bounty.Id, ClaimantId, ClaimantWalletId,
                new BountyEligibilitySnapshot(ClaimantId, true, 100, true,
                    claimedAt.AddMinutes(-1), claimedAt.AddMinutes(1)),
                new BountyClaimRiskApproval(decision, context, cluster, limits, Guid.NewGuid()),
                NextSequence(), claimedAt, idempotency);
        }

        internal BountyClaimResult Claim(BountyEscrowPosition bounty, DateTimeOffset claimedAt) =>
            Coordinator.Claim(ClaimCommand(bounty, claimedAt));

        internal ReclaimBountyCommand ReclaimCommand(
            BountyEscrowPosition bounty,
            DateTimeOffset reclaimedAt,
            string? key = null) => new(
            bounty.Id, PosterId, PosterWalletId, FeeWalletId, NextSequence(), reclaimedAt,
            new IdempotencyKey(key ?? $"reclaim-{Guid.NewGuid():N}"));

        internal BountyReclaimResult Reclaim(BountyEscrowPosition bounty, DateTimeOffset reclaimedAt) =>
            Coordinator.Reclaim(ReclaimCommand(bounty, reclaimedAt));

        private long NextSequence() => Interlocked.Add(ref _journalSequence, 10);

        private EntityRiskCluster Cluster() => new(
            "bounty-cluster", 1, "bounty-graph-evidence",
            [
                new RiskEntityNode(RiskEntityType.Account, "claimant-account"),
                new RiskEntityNode(RiskEntityType.Referral, "referral-cluster"),
                new RiskEntityNode(RiskEntityType.DeviceRiskToken, "device-cluster"),
                new RiskEntityNode(RiskEntityType.PaymentInstrument, "payment-cluster"),
                new RiskEntityNode(RiskEntityType.PayoutDestination, "payout-cluster"),
                new RiskEntityNode(RiskEntityType.MarketplaceCounterparty, "counterparty-cluster")
            ]);

        private static AggregateRiskLimit Limit(RiskLimitDimension dimension, string subject) =>
            new(new RiskLimitKey(dimension, subject), 1, 100_000, TimeSpan.FromDays(1));
    }
}
