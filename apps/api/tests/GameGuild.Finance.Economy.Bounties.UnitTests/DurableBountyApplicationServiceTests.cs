using FluentAssertions;
using GameGuild.Finance.Economy.Bounties.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class DurableBountyApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelfServiceCommandsExposeOnlyBusinessIntent()
    {
        typeof(CreateDurableBountyRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([
                "Amount", "Eligibility", "ExpiresAt", "IdempotencyKey", "RequestedAt"
            ]);
        typeof(ClaimDurableBountyRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["BountyId", "IdempotencyKey", "RequestedAt"]);
        typeof(ReclaimDurableBountyRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["BountyId", "IdempotencyKey", "RequestedAt"]);
    }

    [Fact]
    public async Task CreateAsync_UsesSignedPolicyServerLotsCapabilityReceiptAndPostingAuthority()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.CreateRequest();

        var result = await fixture.Service.CreateAsync(request);

        result.Status.Should().Be(BountyStatus.Open);
        result.PosterId.Should().Be(fixture.ActorId);
        result.Amount.Should().Be(request.Amount);
        result.Eligibility.Should().Be(request.Eligibility);
        result.ReclaimFeePpm.Should().Be(12_500);
        result.PostedAt.Should().Be(request.RequestedAt);
        result.ExpiresAt.Should().Be(request.ExpiresAt);
        result.Version.Should().Be(1);
        fixture.Lots.Requests.Should().ContainSingle().Which.WalletId.Should().Be(fixture.PosterWallet.WalletId);
        fixture.Orchestrator.Intents.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Capability = EconomyValueMovementCapability.BountyEscrow,
            TemplateKind = PostingTemplateKind.BountyEscrow,
            SourceWalletId = fixture.PosterWallet.WalletId,
            DestinationWalletId = fixture.EscrowWallet.WalletId,
            ProviderReferenceHash = "policy-hash",
            DestinationJurisdictionCode = "BR"
        });
        fixture.Authority.Requests.Should().ContainSingle();
        fixture.Authority.Requests[0].CapabilityName.Should().Be("bounty-escrow");
        fixture.Authority.Requests[0].TemplateKind.Should().Be(PostingTemplateKind.BountyEscrow);
        fixture.Posts.Requests.Should().ContainSingle();
        fixture.Posts.Requests[0].IdempotencyKey.Should().Be(request.IdempotencyKey);
        fixture.Posts.Requests[0].RequestHash.Length.Should().Be(64);
        fixture.Posts.Requests[0].PolicyVersion.Value.Should().Be(7);
        fixture.Posts.Requests[0].ReserveVersion.Value.Should().Be(11);
    }

    [Fact]
    public async Task ClaimAsync_UsesPersistedRootsAndReturnsTerminalView()
    {
        await using var fixture = await Fixture.CreateAsync();
        var escrow = fixture.AddEscrow(BountyStatus.Open);
        var request = new ClaimDurableBountyRequest(
            escrow.Id, new IdempotencyKey("claim-1"), Now);

        var result = await fixture.Service.ClaimAsync(request);

        result.Status.Should().Be(BountyStatus.Claimed);
        result.TerminalEvent.Should().NotBeNull();
        fixture.Claims.Requests.Should().ContainSingle();
        fixture.Claims.Requests[0].ClaimantWalletId.Should().Be(fixture.PosterWallet.WalletId);
        fixture.Orchestrator.Intents.Should().ContainSingle().Which.SourceRoots.Should().ContainSingle();
        fixture.Authority.Requests.Should().ContainSingle();
        fixture.Authority.Requests[0].CapabilityName.Should().Be("bounty-claim");
        fixture.Authority.Requests[0].TemplateKind.Should().Be(PostingTemplateKind.BountyClaim);
    }

    [Fact]
    public async Task ReclaimAsync_UsesPersistedRootsAndReturnsTerminalView()
    {
        await using var fixture = await Fixture.CreateAsync();
        var escrow = fixture.AddEscrow(BountyStatus.Expired);
        var request = new ReclaimDurableBountyRequest(
            escrow.Id, new IdempotencyKey("reclaim-1"), Now);

        var result = await fixture.Service.ReclaimAsync(request);

        result.Status.Should().Be(BountyStatus.Reclaimed);
        fixture.Reclaims.Requests.Should().ContainSingle();
        fixture.Reclaims.Requests[0].PosterWalletId.Should().Be(fixture.PosterWallet.WalletId);
        fixture.Authority.Requests.Should().ContainSingle();
        fixture.Authority.Requests[0].CapabilityName.Should().Be("bounty-reclaim");
        fixture.Authority.Requests[0].TemplateKind.Should().Be(PostingTemplateKind.BountyReclaim);
    }

    [Fact]
    public async Task FindAndList_AreTenantScopedOrderedFilteredAndIncludeTerminalEvidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var older = fixture.AddEscrow(BountyStatus.Open, postedAt: Now.AddHours(-2));
        var newer = fixture.AddEscrow(BountyStatus.Open, postedAt: Now.AddHours(-1));
        var foreign = fixture.AddEscrow(BountyStatus.Open, tenantId: Guid.NewGuid());
        fixture.Terminals.Events[newer.Id] = fixture.Terminal(newer, BountyStatus.Claimed);
        fixture.Context.AddRange(
            Row(older),
            Row(newer, BountyStatus.Claimed),
            Row(foreign));
        await fixture.Context.SaveChangesAsync();

        var found = await fixture.Service.FindAsync(fixture.TenantId, newer.Id);
        var missing = await fixture.Service.FindAsync(fixture.TenantId, BountyId.New());
        var all = await fixture.Service.ListAsync(fixture.TenantId, null);
        var claimed = await fixture.Service.ListAsync(fixture.TenantId, BountyStatus.Claimed);

        found.Should().NotBeNull();
        found!.Status.Should().Be(BountyStatus.Claimed);
        missing.Should().BeNull();
        all.Select(item => item.Id).Should().Equal(newer.Id, older.Id);
        all.Should().NotContain(item => item.Id == foreign.Id);
        claimed.Should().ContainSingle().Which.Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingEligibilityInvalidAmountAndLifetimeOutsidePolicy()
    {
        await using var fixture = await Fixture.CreateAsync();
        var valid = fixture.CreateRequest();
        var missingEligibility = valid with { Eligibility = null! };
        var invalidAmount = valid with { Amount = new CoinAmount(CurrencyCode.HardCoin, 0) };
        var tooShort = valid with { ExpiresAt = valid.RequestedAt.AddSeconds(59) };
        var tooLong = valid with { ExpiresAt = valid.RequestedAt.AddDays(31) };

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(missingEligibility).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(invalidAmount).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(tooShort).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(tooLong).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
    }

    [Fact]
    public async Task CreateAsync_RejectsPosterWalletAsEscrowWallet()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Wallets.Escrow = fixture.PosterWallet;

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(fixture.CreateRequest()).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>()
            .WithMessage("*cannot be the poster wallet*");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"escrowWalletId\":\"00000000-0000-0000-0000-000000000000\",\"reclaimFeePpm\":0,\"minimumLifetimeSeconds\":60,\"maximumLifetimeSeconds\":120}")]
    [InlineData("{\"escrowWalletId\":\"10000000-0000-0000-0000-000000000001\",\"reclaimFeePpm\":-1,\"minimumLifetimeSeconds\":60,\"maximumLifetimeSeconds\":120}")]
    [InlineData("{\"escrowWalletId\":\"10000000-0000-0000-0000-000000000001\",\"reclaimFeePpm\":1000000,\"minimumLifetimeSeconds\":60,\"maximumLifetimeSeconds\":120}")]
    [InlineData("{\"escrowWalletId\":\"10000000-0000-0000-0000-000000000001\",\"reclaimFeePpm\":0,\"minimumLifetimeSeconds\":0,\"maximumLifetimeSeconds\":120}")]
    [InlineData("{\"escrowWalletId\":\"10000000-0000-0000-0000-000000000001\",\"reclaimFeePpm\":0,\"minimumLifetimeSeconds\":120,\"maximumLifetimeSeconds\":60}")]
    public async Task CreateAsync_RejectsMalformedOrUnsafeSignedPolicy(string payload)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Policies.Policy = fixture.Policy(EconomyValueMovementCapability.BountyEscrow, payload);

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(fixture.CreateRequest()).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
    }

    [Theory]
    [InlineData(EconomyCapabilityPolicyState.PendingApproval)]
    [InlineData(EconomyCapabilityPolicyState.Approved)]
    [InlineData(EconomyCapabilityPolicyState.Expired)]
    public async Task Operations_RequireAnActiveSignedPolicy(EconomyCapabilityPolicyState state)
    {
        await using var fixture = await Fixture.CreateAsync();
        var escrow = fixture.AddEscrow(BountyStatus.Open);
        fixture.Policies.Policy = fixture.Policy(EconomyValueMovementCapability.BountyEscrow) with { State = state };

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(fixture.CreateRequest()).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
        await FluentActions.Awaiting(() => fixture.Service.ClaimAsync(new ClaimDurableBountyRequest(
                escrow.Id, new IdempotencyKey("claim-policy"), Now)).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
        await FluentActions.Awaiting(() => fixture.Service.ReclaimAsync(new ReclaimDurableBountyRequest(
                escrow.Id, new IdempotencyKey("reclaim-policy"), Now)).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
    }

    [Fact]
    public async Task Operations_RejectMissingPolicy()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Policies.Policy = null;

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(fixture.CreateRequest()).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
    }

    [Fact]
    public async Task CommandsRequireAuthenticatedTenantActor()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.ActorContexts.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = fixture.ActorId.ToString(),
            TenantId = fixture.TenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = false
        });

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(fixture.CreateRequest()).AsTask())
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("jurisdiction")]
    [InlineData("policy")]
    [InlineData("provider")]
    public async Task CommandsRejectMismatchedProtectedOperationAuthorization(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Orchestrator.Mismatch = scenario;

        await FluentActions.Awaiting(() => fixture.Service.CreateAsync(fixture.CreateRequest()).AsTask())
            .Should().ThrowAsync<BountyPolicyUnavailableException>();
    }

    [Fact]
    public async Task Queries_RejectQuarantineTenant()
    {
        await using var fixture = await Fixture.CreateAsync();

        await FluentActions.Awaiting(() => fixture.Service.FindAsync(Guid.Empty, BountyId.New()).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => fixture.Service.ListAsync(Guid.Empty, null).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Constructor_RejectsNonRelationalContextAndNullPorts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var create = (
            IApplicationDbContext context,
            IEconomyWalletDirectory wallets,
            IBountyPostableLotReader lots,
            IBountyEscrowStore escrows,
            IBountyTerminalEventStore terminals,
            IActorContextAccessor actorContexts,
            IEconomyJurisdictionResolver jurisdictions,
            IEconomyCapabilityPolicyStore policies,
            IEconomyProtectedOperationOrchestrator orchestrator,
            IRegisteredPostingCapabilityResolver authority,
            IDurableBountyEscrowPostWorkflow posts,
            IDurableBountyClaimWorkflow claims,
            IDurableBountyReclaimWorkflow reclaims) =>
            new DurableBountyApplicationService(context, wallets, lots, escrows, terminals,
                actorContexts, jurisdictions, policies, orchestrator, authority, posts, claims, reclaims);

        FluentActions.Invoking(() => create(new NonRelationalApplicationContext(), fixture.Wallets,
                fixture.Lots, fixture.Escrows, fixture.Terminals, fixture.ActorContexts,
                fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator, fixture.Authority,
                fixture.Posts, fixture.Claims, fixture.Reclaims))
            .Should().Throw<InvalidOperationException>();
        Action[] nullPorts =
        [
            () => create(fixture.Context, null!, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, null!, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, null!, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, null!,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                null!, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator, fixture.Authority,
                fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, null!, fixture.Policies, fixture.Orchestrator, fixture.Authority,
                fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, null!, fixture.Orchestrator, fixture.Authority,
                fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, null!, fixture.Authority,
                fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator, null!,
                fixture.Posts, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, null!, fixture.Claims, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, fixture.Posts, null!, fixture.Reclaims),
            () => create(fixture.Context, fixture.Wallets, fixture.Lots, fixture.Escrows, fixture.Terminals,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Policies, fixture.Orchestrator,
                fixture.Authority, fixture.Posts, fixture.Claims, null!)
        ];
        nullPorts.Should().AllSatisfy(action =>
            FluentActions.Invoking(action).Should().Throw<ArgumentNullException>());
    }

    private static BountyRow Row(PersistedBountyEscrow escrow, BountyStatus? status = null) => new()
    {
        Id = escrow.Id.Value,
        TenantId = escrow.TenantId,
        PosterId = escrow.PosterId,
        PosterWalletId = escrow.PosterWalletId.Value,
        EscrowWalletId = escrow.EscrowWalletId.Value,
        Currency = escrow.Amount.Currency,
        AmountUnits = escrow.Amount.Units,
        ReclaimFeePpm = escrow.ReclaimFeePpm,
        RequiresPrerequisite = escrow.Eligibility.RequiresPrerequisite,
        MinimumReputation = escrow.Eligibility.MinimumReputation,
        RequiresInstructorVerification = escrow.Eligibility.RequiresInstructorVerification,
        Status = status ?? escrow.Status,
        IdempotencyKey = escrow.IdempotencyKey.Value,
        RequestHash = escrow.RequestHash,
        PostedAt = escrow.PostedAt,
        ExpiresAt = escrow.ExpiresAt,
        Version = escrow.Version
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(BountiesApplicationContext context)
        {
            Context = context;
            TenantId = Guid.NewGuid();
            ActorId = Guid.NewGuid();
            RiskDecisionId = Guid.NewGuid();
            PosterWallet = new EconomyWalletIdentity(WalletId.New(), TenantId, ActorId, WalletLifecycleState.Active);
            EscrowWallet = new EconomyWalletIdentity(WalletId.New(), TenantId, Guid.NewGuid(), WalletLifecycleState.Active);
            Wallets = new WalletDirectory(PosterWallet, EscrowWallet);
            Lots = new LotReader(CreateLot(PosterWallet.WalletId));
            Escrows = new EscrowStore();
            Terminals = new TerminalStore();
            ActorContexts = new TestActorContextAccessor();
            ActorContexts.SetActorContext(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = ActorId.ToString(),
                TenantId = TenantId,
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
                IsAuthenticated = true
            });
            Jurisdictions = new JurisdictionResolver();
            Policies = new PolicyStore(Policy(EconomyValueMovementCapability.BountyEscrow));
            Orchestrator = new ProtectedOperationOrchestrator(TenantId, ActorId, RiskDecisionId);
            Authority = new PostingAuthorityResolver();
            Posts = new PostWorkflow(Escrows);
            Claims = new ClaimWorkflow(this);
            Reclaims = new ReclaimWorkflow(this);
            Service = new DurableBountyApplicationService(context, Wallets, Lots, Escrows, Terminals,
                ActorContexts, Jurisdictions, Policies, Orchestrator, Authority, Posts, Claims, Reclaims);
        }

        public BountiesApplicationContext Context { get; }
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid RiskDecisionId { get; }
        public EconomyWalletIdentity PosterWallet { get; }
        public EconomyWalletIdentity EscrowWallet { get; }
        public WalletDirectory Wallets { get; }
        public LotReader Lots { get; }
        public EscrowStore Escrows { get; }
        public TerminalStore Terminals { get; }
        public TestActorContextAccessor ActorContexts { get; }
        public JurisdictionResolver Jurisdictions { get; }
        public PolicyStore Policies { get; }
        public ProtectedOperationOrchestrator Orchestrator { get; }
        public PostingAuthorityResolver Authority { get; }
        public PostWorkflow Posts { get; }
        public ClaimWorkflow Claims { get; }
        public ReclaimWorkflow Reclaims { get; }
        public DurableBountyApplicationService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<BountiesApplicationContext>()
                .UseSqlite("Data Source=:memory:").Options;
            var context = new BountiesApplicationContext(options);
            await context.Database.OpenConnectionAsync();
            await context.Database.EnsureCreatedAsync();
            return new Fixture(context);
        }

        public CreateDurableBountyRequest CreateRequest() => new(
            new CoinAmount(CurrencyCode.HardCoin, 25), BountyEligibilityRequirements.None,
            Now.AddDays(2),
            new IdempotencyKey("post-1"), Now);

        public EconomyCapabilityPolicy Policy(
            EconomyValueMovementCapability capability,
            string? payload = null) => new(
            Guid.NewGuid(), $"tenant:{TenantId:N}:{capability}:BR", TenantId, capability, "BR", 7,
            payload ?? $$"""{"escrowWalletId":"{{EscrowWallet.WalletId.Value}}","reclaimFeePpm":12500,"minimumLifetimeSeconds":60,"maximumLifetimeSeconds":2592000}""",
            "policy-hash", "key-1", "signature", Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1),
            Now.AddDays(-1), Now.AddDays(-1), Now.AddDays(30), true, EconomyCapabilityPolicyState.Active);

        public PersistedBountyEscrow AddEscrow(
            BountyStatus status,
            DateTimeOffset? postedAt = null,
            Guid? tenantId = null)
        {
            var ownerTenant = tenantId ?? TenantId;
            var lot = CreateLot(PosterWallet.WalletId);
            var fragment = new PersistedBountyEscrowFragment(
                lot.Id, CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 25), lot.Provenance,
                lot.TraceUnitsPerCoinUnit, [lot.Ranges[0]]);
            var escrow = new PersistedBountyEscrow(
                BountyId.New(), ownerTenant, ActorId, PosterWallet.WalletId, EscrowWallet.WalletId,
                new CoinAmount(CurrencyCode.HardCoin, 25), BountyEligibilityRequirements.None, 12500,
                status, new IdempotencyKey($"escrow-{Guid.NewGuid():N}"), "request-hash",
                postedAt ?? Now.AddHours(-1), Now.AddDays(2), 3, [fragment]);
            Escrows.Items[(ownerTenant, escrow.Id)] = escrow;
            return escrow;
        }

        public PersistedBountyTerminalEvent Terminal(PersistedBountyEscrow escrow, BountyStatus status) => new(
            Guid.NewGuid(), escrow.TenantId, escrow.Id, status, ActorId, PosterWallet.WalletId,
            new IdempotencyKey($"terminal-{Guid.NewGuid():N}"), RiskDecisionId,
            status == BountyStatus.Claimed ? SourceStampId.New() : null,
            status == BountyStatus.Claimed ? CreditLotId.New() : null,
            status == BountyStatus.Reclaimed ? escrow.Amount.Units : 0,
            status == BountyStatus.Reclaimed ? 1 : 0, 10, [], Now);

        public ValueTask DisposeAsync() => Context.DisposeAsync();

        private static CreditLot CreateLot(WalletId walletId)
        {
            var root = SourceStampId.New();
            return new CreditLot(
                CreditLotId.New(), walletId, new CoinAmount(CurrencyCode.HardCoin, 100),
                ProvenanceKind.PurchasedHard, Now.AddDays(-5), Now.AddDays(-4), 1,
                CreditLotState.Active,
                [new RootTraceRange(root, 0, 100 * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)],
                CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
        }
    }

    private sealed class BountiesApplicationContext(DbContextOptions<BountiesApplicationContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new BountiesModelConfiguration().Configure(modelBuilder);
            modelBuilder.Entity<BountyRow>().Property(row => row.PostedAt)
                .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
            modelBuilder.Entity<BountyRow>().Property(row => row.ExpiresAt)
                .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
        }

        Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(
            CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class TestActorContextAccessor : IActorContextAccessor
    {
        public ActorContext ActorContext { get; private set; } = ActorContext.Anonymous;
        public void SetActorContext(ActorContext context) => ActorContext = context;
        public void ClearActorContext() => ActorContext = ActorContext.Anonymous;
    }

    private sealed class WalletDirectory(EconomyWalletIdentity owner, EconomyWalletIdentity escrow)
        : IEconomyWalletDirectory
    {
        public EconomyWalletIdentity Escrow { get; set; } = escrow;
        public ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(Guid tenantId, Guid ownerId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(owner);
        public ValueTask<EconomyWalletIdentity> GetWalletAsync(Guid tenantId, WalletId walletId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Escrow);
    }

    private sealed class LotReader(CreditLot lot) : IBountyPostableLotReader
    {
        public List<(WalletId WalletId, CurrencyCode Currency, DateTimeOffset AsOf)> Requests { get; } = [];
        public IReadOnlyList<CreditLot> Read(WalletId walletId, CurrencyCode currency, DateTimeOffset asOf)
        {
            Requests.Add((walletId, currency, asOf));
            return [new CreditLot(lot.Id, walletId, lot.Amount, lot.Provenance, lot.ConfirmedAt,
                lot.OriginalMaturesAt, lot.JournalSequence, lot.State, lot.Ranges, lot.TraceUnitsPerCoinUnit)];
        }
    }

    private sealed class EscrowStore : IBountyEscrowStore
    {
        public Dictionary<(Guid TenantId, BountyId Id), PersistedBountyEscrow> Items { get; } = [];
        public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId) => Items[(tenantId, bountyId)];
        public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey,
            string requestHash) => Items.Values.SingleOrDefault(item => item.TenantId == tenantId &&
                item.IdempotencyKey == idempotencyKey && item.RequestHash == requestHash);
        public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command) =>
            throw new NotSupportedException();
    }

    private sealed class TerminalStore : IBountyTerminalEventStore
    {
        public Dictionary<BountyId, PersistedBountyTerminalEvent> Events { get; } = [];
        public PersistedBountyTerminalEvent? FindByBounty(Guid tenantId, BountyId bountyId) =>
            Events.GetValueOrDefault(bountyId) is { } item && item.TenantId == tenantId ? item : null;
        public PersistedBountyTerminalEvent? FindByIdempotency(Guid tenantId, IdempotencyKey idempotencyKey) =>
            Events.Values.SingleOrDefault(item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey);
    }

    private sealed class PolicyStore(EconomyCapabilityPolicy? policy) : IEconomyCapabilityPolicyStore
    {
        public EconomyCapabilityPolicy? Policy { get; set; } = policy;
        public ValueTask<EconomyCapabilityPolicy?> CurrentAsync(Guid? tenantId,
            EconomyValueMovementCapability capability, string jurisdictionCode, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Policy is null ? null : Policy with { Capability = capability });
        public ValueTask<EconomyCapabilityPolicy> ProposeAsync(EconomyCapabilityPolicyProposal proposal,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy> ApproveAsync(Guid policyId, Guid actorId,
            string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class JurisdictionResolver : IEconomyJurisdictionResolver
    {
        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId,
            Guid actorId,
            string? providerJurisdiction,
            string? destinationJurisdiction,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken) => ValueTask.FromResult(
            new EconomyJurisdictionResolution("BR", 2, 7, "jurisdiction-evidence"));
    }

    private sealed class ProtectedOperationOrchestrator(
        Guid tenantId,
        Guid actorId,
        Guid riskDecisionId) : IEconomyProtectedOperationOrchestrator
    {
        public List<EconomyProtectedOperationIntent> Intents { get; } = [];
        public string? Mismatch { get; set; }

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            Intents.Add(intent);
            var fingerprint = $"server-{intent.Capability}-{intent.IdempotencyKey.Value}";
            var authorizedTenantId = Mismatch == "tenant" ? Guid.NewGuid() : tenantId;
            var authorizedActorId = Mismatch == "actor" ? Guid.NewGuid() : actorId;
            var jurisdictionCode = Mismatch == "jurisdiction" ? "US" : "BR";
            var policyVersion = Mismatch == "policy" ? 8 : 7;
            var providerHash = Mismatch == "provider" ? "other-provider" : intent.ProviderReferenceHash;
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), authorizedTenantId, authorizedActorId,
                EconomySubjectReference.ForUser(tenantId, actorId),
                jurisdictionCode, intent.Capability, fingerprint, policyVersion, 11, riskDecisionId, 3,
                providerHash, intent.DestinationHash,
                intent.SourceRoots.Select(root => root.Value.ToString("N")).ToArray(),
                ["evidence-hash"], intent.RequestedAt, intent.RequestedAt.AddMinutes(5),
                "receipt-hash", "receipt-key", "receipt-signature");
            return await operation(new EconomyProtectedOperationAuthorization(
                authorizedTenantId, authorizedActorId, jurisdictionCode, riskDecisionId, fingerprint, receipt),
                cancellationToken);
        }
    }

    private sealed class PostingAuthorityResolver : IRegisteredPostingCapabilityResolver
    {
        public List<(string CapabilityName, PostingTemplateKind TemplateKind)> Requests { get; } = [];
        public Task<RegisteredPostingCapability> ResolveAsync(string capabilityName,
            PostingTemplateKind templateKind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegisteredPostingCapability(Guid.NewGuid(), capabilityName, templateKind));
        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(string capabilityName,
            PostingTemplateKind templateKind, CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((capabilityName, templateKind));
            return Task.FromResult(new RegisteredPostingAuthority(
                Guid.NewGuid(), receipt.ActorId, receipt.TenantId, receipt.RiskDecisionId,
                receipt.OperationFingerprint, 1));
        }
    }

    private sealed class PostWorkflow(EscrowStore store) : IDurableBountyEscrowPostWorkflow
    {
        public List<DurableBountyEscrowPostRequest> Requests { get; } = [];
        public Task<PersistedBountyEscrow> PostAsync(DurableBountyEscrowPostRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var persisted = new PersistedBountyEscrow(
                request.Id, request.Authority.TenantId, request.PosterId, request.PosterWalletId,
                request.EscrowWalletId, request.Amount, request.Eligibility, request.ReclaimFeePpm,
                BountyStatus.Open, request.IdempotencyKey, request.RequestHash, request.PostedAt,
                request.ExpiresAt, 1, []);
            store.Items[(persisted.TenantId, persisted.Id)] = persisted;
            return Task.FromResult(persisted);
        }
    }

    private sealed class ClaimWorkflow(Fixture fixture) : IDurableBountyClaimWorkflow
    {
        public List<DurableBountyClaimRequest> Requests { get; } = [];
        public Task<PersistedBountyTerminalEvent> ClaimAsync(DurableBountyClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var terminal = fixture.Terminal(fixture.Escrows.Get(request.Authority.TenantId, request.BountyId),
                BountyStatus.Claimed);
            fixture.Terminals.Events[request.BountyId] = terminal;
            return Task.FromResult(terminal);
        }
    }

    private sealed class ReclaimWorkflow(Fixture fixture) : IDurableBountyReclaimWorkflow
    {
        public List<DurableBountyReclaimRequest> Requests { get; } = [];
        public Task<PersistedBountyTerminalEvent> ReclaimAsync(DurableBountyReclaimRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var terminal = fixture.Terminal(fixture.Escrows.Get(request.Authority.TenantId, request.BountyId),
                BountyStatus.Reclaimed);
            fixture.Terminals.Events[request.BountyId] = terminal;
            return Task.FromResult(terminal);
        }
    }
}
