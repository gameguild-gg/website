using System.Reflection;
using System.Runtime.ExceptionServices;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountyFactoryBoundaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void ClaimFactoryRejectsEveryInvalidIdentityAndLifecycleBoundary()
    {
        var claimant = Guid.NewGuid();
        var request = ClaimRequest(BountyId.New(), claimant, WalletId.New());
        var escrow = Escrow(request.BountyId, CurrencyCode.HardCoin,
            [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10)]);

        FluentActions.Invoking(() => BountyClaimPostingFactory.Create(null!, request))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => BountyClaimPostingFactory.Create(escrow, null!))
            .Should().Throw<ArgumentNullException>();
        AssertThrows<ArgumentException>(() => BountyClaimPostingFactory.Create(
            escrow, request with { BountyId = BountyId.New() }));
        AssertThrows<BountyTerminalConflictException>(() => BountyClaimPostingFactory.Create(
            escrow with { Status = BountyStatus.Claimed }, request));
        AssertThrows<BountyClaimIneligibleException>(() => BountyClaimPostingFactory.Create(
            escrow with { PosterId = claimant }, request));
        AssertThrows<BountyClaimIneligibleException>(() => BountyClaimPostingFactory.Create(
            escrow with { PosterWalletId = request.ClaimantWalletId }, request));
        AssertThrows<BountyClaimIneligibleException>(() => BountyClaimPostingFactory.Create(
            escrow with { EscrowWalletId = request.ClaimantWalletId }, request));
        AssertThrows<ArgumentException>(() => BountyClaimPostingFactory.Create(
            escrow, request with { Authority = Authority(Guid.NewGuid(), "claim") }));
    }

    [Fact]
    public void ClaimFactoryRejectsEveryIncompleteEscrowShape()
    {
        var request = ClaimRequest(BountyId.New(), Guid.NewGuid(), WalletId.New());
        var valid = Escrow(request.BountyId, CurrencyCode.HardCoin,
            [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10)]);

        AssertThrows<InvalidOperationException>(() => BountyClaimPostingFactory.Create(
            valid with { Fragments = [] }, request));
        AssertThrows<InvalidOperationException>(() => BountyClaimPostingFactory.Create(
            valid with
            {
                Fragments = [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 1),
                    Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 9) with
                    { EscrowLotId = null }]
            },
            request));
        AssertThrows<InvalidOperationException>(() => BountyClaimPostingFactory.Create(
            valid with { Fragments = [Fragment(CurrencyCode.SoftCoin, ProvenanceKind.ConvertedSoft, 10)] },
            request));
        AssertThrows<InvalidOperationException>(() => BountyClaimPostingFactory.Create(
            valid with { Fragments = [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 9)] },
            request));
    }

    [Fact]
    public void ClaimFactorySupportsSoftEscrowAndRejectsUnsupportedAccountMappings()
    {
        var request = ClaimRequest(BountyId.New(), Guid.NewGuid(), WalletId.New());
        var escrow = Escrow(request.BountyId, CurrencyCode.SoftCoin,
            [Fragment(CurrencyCode.SoftCoin, ProvenanceKind.ConvertedSoft, 10)]);

        var posting = BountyClaimPostingFactory.Create(escrow, request);
        posting.Posting.Lines[0].Account.Should().Be(EconomyAccountCode.SoftCoinEscrow);
        posting.Posting.Lines[1].Account.Should().Be(EconomyAccountCode.SoftCoinLiability);
        posting.Posting.Lines[1].Provenance.Should().Be(ProvenanceKind.EscrowReturn);

        AssertPrivateMappingFails(typeof(BountyClaimPostingFactory), "EscrowFor", (CurrencyCode)999);
        AssertPrivateMappingFails(
            typeof(BountyClaimPostingFactory), "LiabilityFor",
            CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard);
        AssertPrivateMappingFails(
            typeof(BountyClaimPostingFactory), "LiabilityFor",
            CurrencyCode.SoftCoin, ProvenanceKind.PurchasedHard);
    }

    [Fact]
    public void EscrowFactoryValidatesNullEmptyAndSoftCoinBoundaries()
    {
        var poster = Guid.NewGuid();
        FluentActions.Invoking(() => BountyEscrowPostingFactory.Create(
                null!, PostingId.New(), Authority(poster, "post"), new ReserveVersion(1), new PolicyVersion(1)))
            .Should().Throw<ArgumentNullException>();

        var hardPosition = Position(poster, CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10);
        FluentActions.Invoking(() => BountyEscrowPostingFactory.Create(
                hardPosition, PostingId.New(), null!, new ReserveVersion(1), new PolicyVersion(1)))
            .Should().Throw<ArgumentNullException>();
        AssertThrows<ArgumentException>(() => BountyEscrowPostingFactory.Create(
            hardPosition, PostingId.New(), Authority(Guid.NewGuid(), "post"),
            new ReserveVersion(1), new PolicyVersion(1)));

        var empty = PositionWithFragments(poster, CurrencyCode.HardCoin, []);
        AssertThrows<ArgumentException>(() => BountyEscrowPostingFactory.Create(
            empty, PostingId.New(), Authority(poster, "post"),
            new ReserveVersion(1), new PolicyVersion(1)));

        var softPosition = Position(poster, CurrencyCode.SoftCoin, ProvenanceKind.ConvertedSoft, 10);
        var softPosting = BountyEscrowPostingFactory.Create(
            softPosition, PostingId.New(), Authority(poster, "post"),
            new ReserveVersion(1), new PolicyVersion(1), "snapshot");
        softPosting.Posting.Lines[0].Account.Should().Be(EconomyAccountCode.SoftCoinLiability);
        softPosting.Posting.Lines[^1].Account.Should().Be(EconomyAccountCode.SoftCoinEscrow);
        softPosting.DispatchSnapshotHash.Should().Be("snapshot");

        AssertPrivateMappingFails(
            typeof(BountyEscrowPostingFactory), "LiabilityFor",
            (CurrencyCode)999, ProvenanceKind.PurchasedHard);
        AssertPrivateMappingFails(typeof(BountyEscrowPostingFactory), "EscrowFor", (CurrencyCode)999);
    }

    [Fact]
    public void ReclaimFactoryRejectsEveryInvalidIdentityAndLifecycleBoundary()
    {
        var poster = Guid.NewGuid();
        var wallet = WalletId.New();
        var request = ReclaimRequest(BountyId.New(), poster, wallet);
        var escrow = Escrow(request.BountyId, CurrencyCode.HardCoin,
            [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10)], poster, wallet);

        FluentActions.Invoking(() => BountyReclaimPostingFactory.Create(null!, request))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => BountyReclaimPostingFactory.Create(escrow, null!))
            .Should().Throw<ArgumentNullException>();
        AssertThrows<ArgumentException>(() => BountyReclaimPostingFactory.Create(
            escrow, request with { BountyId = BountyId.New() }));
        AssertThrows<BountyTerminalConflictException>(() => BountyReclaimPostingFactory.Create(
            escrow with { Status = BountyStatus.Reclaimed }, request));
        AssertThrows<BountyOwnershipException>(() => BountyReclaimPostingFactory.Create(
            escrow with { PosterId = Guid.NewGuid() }, request));
        AssertThrows<BountyOwnershipException>(() => BountyReclaimPostingFactory.Create(
            escrow with { PosterWalletId = WalletId.New() }, request));
        AssertThrows<ArgumentException>(() => BountyReclaimPostingFactory.Create(
            escrow, request with { Authority = Authority(Guid.NewGuid(), "reclaim") }));
        AssertThrows<BountyNotExpiredException>(() => BountyReclaimPostingFactory.Create(
            escrow with { ExpiresAt = request.ReclaimedAt.AddTicks(1) }, request));
    }

    [Fact]
    public void ReclaimFactoryRejectsEveryIncompleteEscrowShape()
    {
        var poster = Guid.NewGuid();
        var wallet = WalletId.New();
        var request = ReclaimRequest(BountyId.New(), poster, wallet);
        var valid = Escrow(request.BountyId, CurrencyCode.HardCoin,
            [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10)], poster, wallet);

        AssertThrows<InvalidOperationException>(() => BountyReclaimPostingFactory.Create(
            valid with { Fragments = [] }, request));
        AssertThrows<InvalidOperationException>(() => BountyReclaimPostingFactory.Create(
            valid with
            {
                Fragments = [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 1),
                    Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 9) with
                    { EscrowLotId = null }]
            },
            request));
        AssertThrows<InvalidOperationException>(() => BountyReclaimPostingFactory.Create(
            valid with { Fragments = [Fragment(CurrencyCode.SoftCoin, ProvenanceKind.ConvertedSoft, 10)] },
            request));
        AssertThrows<InvalidOperationException>(() => BountyReclaimPostingFactory.Create(
            valid with { Fragments = [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 9)] },
            request));

        var malformed = Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10) with
        {
            SelectedRanges = [new RootTraceRange(SourceStampId.New(), 0, 9_000, 0)]
        };
        AssertThrows<InvalidOperationException>(() => BountyReclaimPostingFactory.Create(
            valid with { ReclaimFeePpm = 500_000, Fragments = [malformed] }, request));
    }

    [Fact]
    public void ReclaimFactoryCoversNoFeeAndWholeRangeFeePartitions()
    {
        var poster = Guid.NewGuid();
        var wallet = WalletId.New();
        var request = ReclaimRequest(BountyId.New(), poster, wallet);
        var noFee = Escrow(request.BountyId, CurrencyCode.HardCoin,
            [Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10)], poster, wallet);
        BountyReclaimPostingFactory.Create(noFee, request).Posting.Lines.Should().HaveCount(2);

        var root = SourceStampId.New();
        var splitRanges = Fragment(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, 10) with
        {
            SelectedRanges =
            [
                new RootTraceRange(root, 0, 5_000, 0),
                new RootTraceRange(root, 5_000, 5_000, 0)
            ]
        };
        var halfFee = noFee with { ReclaimFeePpm = 500_000, Fragments = [splitRanges] };
        var posting = BountyReclaimPostingFactory.Create(halfFee, request);
        posting.Allocations.Should().HaveCount(2);
        posting.Allocations[0].RootRanges.Should().ContainSingle().Which.Start.Should().Be(0);
        posting.Allocations[1].RootRanges.Should().ContainSingle().Which.Start.Should().Be(5_000);

        AssertPrivateMappingFails(typeof(BountyReclaimPostingFactory), "EscrowFor", (CurrencyCode)999);
        AssertPrivateMappingFails(
            typeof(BountyReclaimPostingFactory), "LiabilityFor",
            CurrencyCode.HardCoin, ProvenanceKind.ConvertedSoft);
        AssertPrivateMappingFails(typeof(BountyReclaimPostingFactory), "FeeDestinationFor", (CurrencyCode)999);
    }

    [Fact]
    public void EscrowStoreRejectsPersistedRowsMissingHashOrFragmentRanges()
    {
        var id = BountyId.New();
        var withoutHash = new ScriptedRelationalInterceptor();
        withoutHash.EnqueueReader(BountyRows(id, null).Build());
        using (var context = new ScriptedBountiesContext(withoutHash))
            FluentActions.Invoking(() => new PostgreSqlBountyEscrowStore(context).Get(TenantId, id))
                .Should().Throw<InvalidOperationException>();

        var withoutRanges = new ScriptedRelationalInterceptor();
        withoutRanges.EnqueueReader(BountyRows(id, "hash").Build());
        withoutRanges.EnqueueReader(FragmentRows().AddRow(
            CreditLotId.New().Value, CreditLotId.New().Value, (int)CurrencyCode.HardCoin,
            (int)ProvenanceKind.PurchasedHard, 1L, 1000L, "null").Build());
        using (var context = new ScriptedBountiesContext(withoutRanges))
            FluentActions.Invoking(() => new PostgreSqlBountyEscrowStore(context).Get(TenantId, id))
                .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReclaimPartitionInvariantRejectsEachIncompleteShape()
    {
        InvokePrivate(typeof(BountyReclaimPostingFactory), "EnsurePartitionComplete", [0L, 2, 10L, 10L]);
        FluentActions.Invoking(() => InvokePrivate(
                typeof(BountyReclaimPostingFactory), "EnsurePartitionComplete", [1L, 2, 10L, 10L]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => InvokePrivate(
                typeof(BountyReclaimPostingFactory), "EnsurePartitionComplete", [0L, 0, 10L, 10L]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => InvokePrivate(
                typeof(BountyReclaimPostingFactory), "EnsurePartitionComplete", [0L, 2, 9L, 10L]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PrivateReservationRangeExposesEveryComparedBoundary()
    {
        var workflowType = typeof(PostgreSqlDurableBountyEscrowPostWorkflow);
        var rangeType = workflowType.GetNestedType("ReservationRange", BindingFlags.NonPublic)!;
        var parent = CreditLotId.New();
        var root = SourceStampId.New();
        var range = Activator.CreateInstance(
            rangeType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [parent, root, 10L, 20L, 3L],
            culture: null)!;

        rangeType.GetProperty("ParentLotId")!.GetValue(range).Should().Be(parent);
        rangeType.GetProperty("RootSourceStampId")!.GetValue(range).Should().Be(root);
        rangeType.GetProperty("StartInclusive")!.GetValue(range).Should().Be(10L);
        rangeType.GetProperty("EndExclusive")!.GetValue(range).Should().Be(20L);
        rangeType.GetProperty("ReversalEpoch")!.GetValue(range).Should().Be(3L);
    }

    [Fact]
    public void TerminalEventRecordExposesItsIdentifier()
    {
        var id = Guid.NewGuid();
        var terminal = new PersistedBountyTerminalEvent(
            id, TenantId, BountyId.New(), BountyStatus.Reclaimed, Guid.NewGuid(), WalletId.New(),
            new IdempotencyKey("terminal"), null, null, null, 1, 0, 1, [], Now);
        terminal.Id.Should().Be(id);
    }

    private static void AssertPrivateMappingFails(Type type, string name, params object[] arguments) =>
        FluentActions.Invoking(() => InvokePrivate(type, name, arguments))
            .Should().Throw<ArgumentOutOfRangeException>();

    private static object? InvokePrivate(Type type, string name, object[] arguments)
    {
        try
        {
            return type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void AssertThrows<T>(Action action) where T : Exception =>
        FluentActions.Invoking(action).Should().Throw<T>();

    private static DurableBountyClaimRequest ClaimRequest(BountyId id, Guid claimant, WalletId wallet) => new(
        id, claimant, wallet, Now, new IdempotencyKey($"claim-{Guid.NewGuid():N}"), "evidence",
        Authority(claimant, "claim"), new ReserveVersion(1), new PolicyVersion(1));

    private static DurableBountyReclaimRequest ReclaimRequest(BountyId id, Guid poster, WalletId wallet) => new(
        id, poster, wallet, Now, new IdempotencyKey($"reclaim-{Guid.NewGuid():N}"),
        Authority(poster, "reclaim"), new ReserveVersion(1), new PolicyVersion(1));

    private static RegisteredPostingAuthority Authority(Guid actor, string operation) => new(
        Guid.NewGuid(), actor, Guid.NewGuid(), Guid.NewGuid(), operation, 1);

    private static PersistedBountyEscrow Escrow(
        BountyId id,
        CurrencyCode currency,
        IReadOnlyList<PersistedBountyEscrowFragment> fragments,
        Guid? poster = null,
        WalletId? posterWallet = null) => new(
        id, TenantId, poster ?? Guid.NewGuid(), posterWallet ?? WalletId.New(), WalletId.New(),
        new CoinAmount(currency, 10), BountyEligibilityRequirements.None, 0, BountyStatus.Open,
        new IdempotencyKey($"post-{id.Value:N}"), "hash", Now.AddDays(-2), Now.AddDays(-1), 1, fragments);

    private static PersistedBountyEscrowFragment Fragment(
        CurrencyCode currency,
        ProvenanceKind provenance,
        long units,
        CreditLotId? escrowLotId = null) => new(
        CreditLotId.New(), escrowLotId ?? CreditLotId.New(), new CoinAmount(currency, units), provenance,
        CurrencyTraceScale.For(currency),
        [new RootTraceRange(SourceStampId.New(), 0, checked(units * CurrencyTraceScale.For(currency)), 0)]);

    private static BountyEscrowPosition Position(
        Guid poster,
        CurrencyCode currency,
        ProvenanceKind provenance,
        long units)
    {
        var wallet = WalletId.New();
        var scale = CurrencyTraceScale.For(currency);
        var lot = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(currency, units), provenance,
            Now.AddDays(-2), Now.AddDays(-1), 1, CreditLotState.Active,
            [new RootTraceRange(SourceStampId.New(), 0, checked(units * scale), 0)], scale);
        return BountyEscrowPositionFactory.Create(new PostBountyCommand(
            BountyId.New(), poster, wallet, WalletId.New(), new CoinAmount(currency, units), [lot],
            BountyEligibilityRequirements.None, 0, Now, Now.AddDays(1),
            new IdempotencyKey($"position-{Guid.NewGuid():N}")));
    }

    private static BountyEscrowPosition PositionWithFragments(
        Guid poster,
        CurrencyCode currency,
        IReadOnlyCollection<BountyEscrowFragment> fragments)
    {
        var constructor = typeof(BountyEscrowPosition)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (BountyEscrowPosition)constructor.Invoke(
        [
            BountyId.New(), poster, WalletId.New(), WalletId.New(), new CoinAmount(currency, 1),
            fragments, BountyEligibilityRequirements.None, 0, Now, Now.AddDays(1)
        ]);
    }

    private static TestDataTable BountyRows(BountyId id, string? requestHash) =>
        new TestDataTable(
                ("Id", typeof(Guid)), ("TenantId", typeof(Guid)), ("PosterId", typeof(Guid)), ("PosterWalletId", typeof(Guid)),
                ("EscrowWalletId", typeof(Guid)), ("Currency", typeof(int)), ("AmountUnits", typeof(long)),
                ("ReclaimFeePpm", typeof(int)), ("RequiresPrerequisite", typeof(bool)),
                ("MinimumReputation", typeof(int)), ("RequiresInstructorVerification", typeof(bool)),
                ("Status", typeof(int)), ("IdempotencyKey", typeof(string)), ("RequestHash", typeof(string)),
                ("PostedAt", typeof(DateTimeOffset)), ("ExpiresAt", typeof(DateTimeOffset)), ("Version", typeof(long)))
            .AddRow(
                id.Value, TenantId, Guid.NewGuid(), WalletId.New().Value, WalletId.New().Value,
                (int)CurrencyCode.HardCoin, 1L, 0, false, 0, false, (int)BountyStatus.Open,
                "post", requestHash, Now.AddDays(-2), Now.AddDays(-1), 1L);

    private static TestDataTable FragmentRows() => new(
        ("ParentLotId", typeof(Guid)), ("EscrowLotId", typeof(Guid)), ("Currency", typeof(int)),
        ("Provenance", typeof(int)), ("AmountUnits", typeof(long)),
        ("TraceUnitsPerCoinUnit", typeof(long)), ("SelectedRootRanges", typeof(string)));
}
