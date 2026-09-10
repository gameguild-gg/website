using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class DurableMarketplaceServicesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Settlement_UsesAuthoritativeSnapshotSignedPolicyFifoAndCapabilityReceipt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.SettlementRequest();

        var result = await fixture.Settlements.SettleAsync(request);

        result.OrderId.Should().Be(fixture.Order.OrderId);
        result.ProductId.Should().Be(fixture.Order.ProductId);
        result.BuyerId.Should().Be(fixture.BuyerId);
        result.SellerId.Should().Be(fixture.SellerId);
        result.Status.Should().Be(MarketplaceSettlementStatus.Settled);
        result.EntitlementStatus.Should().Be(MarketplaceEntitlementStatus.PendingGrant);
        result.Legs.Should().ContainSingle().Which.Should().Be(
            new MarketplacePriceLegSnapshot(CurrencyCode.HardCoin, 200, 180, 20));
        result.JournalSequence.Should().Be(100);
        result.JournalHash.Should().Be("settlement-journal-hash");
        result.IsDuplicate.Should().BeFalse();
        result.SettledAt.Should().Be(Now);
        fixture.Reservations.Requests.Should().ContainSingle();
        fixture.Reservations.Requests[0].Legs.Should().Equal(new CoinAmount(CurrencyCode.HardCoin, 200));
        fixture.Orchestrator.Authorizations.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            fixture.TenantId,
            ActorId = fixture.BuyerId,
            JurisdictionCode = "BR",
        });
        fixture.Orchestrator.Intents.Should().ContainSingle().Which.Capability
            .Should().Be(EconomyValueMovementCapability.MarketplaceSettlement);
        fixture.Authority.Requests.Should().ContainSingle();
        fixture.Authority.Requests[0].Name.Should().Be("marketplace-settlement");
        fixture.Authority.Requests[0].Kind.Should().Be(PostingTemplateKind.MarketplaceSettlement);
        fixture.SettlementLedger.Requests.Should().ContainSingle();
        fixture.SettlementLedger.Requests[0].Order.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Settlement_ReplaysSameOrderAndRejectsDifferentIdempotencyKey()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.SettlementRequest();
        var first = await fixture.Settlements.SettleAsync(request);

        var replay = await fixture.Settlements.SettleAsync(request);

        replay.SettlementId.Should().Be(first.SettlementId);
        replay.IsDuplicate.Should().BeTrue();
        fixture.SettlementLedger.Requests.Should().ContainSingle();
        await FluentActions.Awaiting(() => fixture.Settlements.SettleAsync(
                request with { IdempotencyKey = new IdempotencyKey("different-settlement") }).AsTask())
            .Should().ThrowAsync<MarketplaceIdempotencyConflictException>();
    }

    [Theory]
    [InlineData("settlement")]
    [InlineData("refund")]
    public async Task ProtectedOperation_RequiresAuthenticatedTenantActor(string operation)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.ActorContexts.ClearActorContext();

        Func<Task> act = operation == "settlement"
            ? () => fixture.Settlements.SettleAsync(fixture.SettlementRequest()).AsTask()
            : () => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(Guid.NewGuid(), 1, "unauthenticated")).AsTask();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        fixture.Orchestrator.Intents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("jurisdiction")]
    [InlineData("provider")]
    [InlineData("destination")]
    public async Task Settlement_RejectsMismatchedProtectedOperationAuthorization(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Orchestrator.Mismatch = scenario;

        await FluentActions.Awaiting(() => fixture.Settlements.SettleAsync(fixture.SettlementRequest()).AsTask())
            .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("jurisdiction")]
    [InlineData("provider")]
    [InlineData("destination")]
    public async Task Refund_RejectsMismatchedProtectedOperationAuthorization(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        fixture.Orchestrator.Mismatch = scenario;

        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, scenario)).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();
    }

    [Fact]
    public async Task Settlement_RejectsPolicySellerWalletAliasingAndUnexpectedPosting()
    {
        await using (var fixture = await Fixture.CreateAsync())
        {
            fixture.Policies.Snapshot = fixture.Policies.Snapshot with
            {
                Policy = ProductCurrencyPolicyVersion.Create(
                    fixture.ProductId, Guid.NewGuid(), 7, ProductCurrencyMode.HardOnly,
                    100, 0, 100_000, Now.AddDays(-1))
            };
            await FluentActions.Awaiting(() => fixture.Settlements.SettleAsync(fixture.SettlementRequest()).AsTask())
                .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
        }

        await using (var fixture = await Fixture.CreateAsync())
        {
            fixture.Wallets.Platform = fixture.Wallets.Buyer;
            await FluentActions.Awaiting(() => fixture.Settlements.SettleAsync(fixture.SettlementRequest()).AsTask())
                .Should().ThrowAsync<MarketplaceOrderSnapshotException>();
        }

        await using (var fixture = await Fixture.CreateAsync())
        {
            fixture.SettlementLedger.ReturnUnexpectedPosting = true;
            await FluentActions.Awaiting(() => fixture.Settlements.SettleAsync(fixture.SettlementRequest()).AsTask())
                .Should().ThrowAsync<RegisteredPostingRejectedException>();
        }
    }

    [Fact]
    public async Task Refund_PersistsPartialThenFullRefundAndMapsDebtAndEntitlementState()
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        fixture.RefundLedger.AddDebt = true;

        var partial = await fixture.Refunds.RefundAsync(fixture.RefundRequest(settlement.SettlementId, 1, " customer_request "));
        var full = await fixture.Refunds.RefundAsync(fixture.RefundRequest(
            settlement.SettlementId, 1, "duplicate_charge", "refund-2"));

        partial.Quantity.Should().Be(1);
        partial.CumulativeRefundedQuantity.Should().Be(1);
        partial.SettlementStatus.Should().Be(MarketplaceSettlementStatus.PartiallyRefunded);
        partial.EntitlementStatus.Should().Be(MarketplaceEntitlementStatus.PendingGrant);
        partial.Legs.Should().Equal(new CoinAmount(CurrencyCode.HardCoin, 100));
        partial.Debts.Should().ContainSingle().Which.Should().Be(new DurableMarketplaceRefundDebt(
            fixture.SellerWallet.WalletId, CurrencyCode.HardCoin, 10, "debt-evidence"));
        partial.Debts[0].ResponsibleWalletId.Should().Be(fixture.SellerWallet.WalletId);
        partial.Debts[0].Currency.Should().Be(CurrencyCode.HardCoin);
        partial.Debts[0].Units.Should().Be(10);
        partial.Debts[0].EvidenceHash.Should().Be("debt-evidence");
        partial.SettlementId.Should().Be(settlement.SettlementId);
        partial.PostingId.Value.Should().NotBeEmpty();
        partial.JournalSequence.Should().BeGreaterThan(0);
        partial.JournalHash.Should().Be("refund-journal-hash");
        partial.IsDuplicate.Should().BeFalse();
        full.CumulativeRefundedQuantity.Should().Be(2);
        full.SettlementStatus.Should().Be(MarketplaceSettlementStatus.Refunded);
        full.EntitlementStatus.Should().Be(MarketplaceEntitlementStatus.Revoked);
        fixture.Orchestrator.Intents.Last().Capability.Should().Be(EconomyValueMovementCapability.MarketplaceRefund);
        fixture.Authority.Requests.Last().Should().Be(("marketplace-refund", PostingTemplateKind.MarketplaceRefund));
        settlement.PostingId.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Refund_ReplaysIdenticalRequestAndRejectsEveryChangedBinding()
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        var request = fixture.RefundRequest(settlement.SettlementId, 1, "customer_request");
        var first = await fixture.Refunds.RefundAsync(request);

        var replay = await fixture.Refunds.RefundAsync(request);

        replay.RefundId.Should().Be(first.RefundId);
        replay.IsDuplicate.Should().BeTrue();
        fixture.RefundLedger.Requests.Should().ContainSingle();
        var conflicts = new[]
        {
            request with { SettlementId = Guid.NewGuid() },
            request with { Quantity = 2 },
            request with { ReasonCode = "different" }
        };
        foreach (var conflict in conflicts)
            await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(conflict).AsTask())
                .Should().ThrowAsync<MarketplaceIdempotencyConflictException>();
    }

    [Fact]
    public async Task Refund_RejectsMissingSettlementWrongBuyerExcessAndCompletedSettlement()
    {
        await using var fixture = await Fixture.CreateAsync();
        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(Guid.NewGuid(), 1, "missing")).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();

        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        fixture.SetActor(Guid.NewGuid());
        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, "wrong-buyer")).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();
        fixture.SetActor(fixture.BuyerId);
        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 3, "excess")).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();

        var row = await fixture.Context.Set<MarketplaceSettlementRow>().SingleAsync();
        row.Status = MarketplaceSettlementStatus.Refunded;
        row.RefundedQuantity = row.Quantity;
        await fixture.Context.SaveChangesAsync();
        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, "completed")).AsTask())
            .Should().ThrowAsync<MarketplaceAlreadyRefundedException>();
    }

    [Fact]
    public async Task Refund_OperationsAuthorityCanActForBuyer()
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        fixture.SetActor(Guid.NewGuid());

        var result = await fixture.Refunds.RefundAsync(fixture.RefundRequest(
            settlement.SettlementId, 1, "operations") with
        {
            Authority = MarketplaceRefundAuthority.Operations
        });

        result.Quantity.Should().Be(1);
    }

    [Theory]
    [InlineData("seller")]
    [InlineData("platform")]
    public async Task Refund_RejectsHistoricalPolicyThatDoesNotMatchSettlement(string mismatch)
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        if (mismatch == "seller")
        {
            fixture.Policies.Snapshot = fixture.Policies.Snapshot with
            {
                Policy = ProductCurrencyPolicyVersion.Create(
                    fixture.ProductId, Guid.NewGuid(), 7, ProductCurrencyMode.HardOnly,
                    100, 0, 100_000, Now.AddDays(-1))
            };
        }
        else
        {
            fixture.Policies.Snapshot = fixture.Policies.Snapshot with { PlatformFeeWalletId = Guid.NewGuid() };
        }

        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, mismatch)).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();
    }

    [Fact]
    public async Task Refund_RejectsSettlementWithoutDurablePriceLegs()
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        fixture.Context.RemoveRange(fixture.Context.Set<MarketplaceSettlementLegRow>());
        await fixture.Context.SaveChangesAsync();

        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, "missing-legs")).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();
    }

    [Theory]
    [InlineData("refunded-over-target")]
    [InlineData("target-over-total")]
    [InlineData("zero-rounded")]
    public async Task Refund_RejectsInconsistentOrZeroPriceLegs(string invalid)
    {
        await using var fixture = await Fixture.CreateAsync(bypassDatabaseConstraints: true);
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        var leg = await fixture.Context.Set<MarketplaceSettlementLegRow>().SingleAsync();
        if (invalid == "refunded-over-target") leg.RefundedUnits = 101;
        if (invalid == "target-over-total") leg.Units = -1;
        if (invalid == "zero-rounded")
        {
            leg.Units = 1;
            leg.SellerUnits = 1;
            leg.PlatformFeeUnits = 0;
        }
        await fixture.Context.SaveChangesAsync();

        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, invalid)).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("name")]
    [InlineData("kind")]
    [InlineData("guid")]
    public async Task Refund_RejectsFundingWithoutSourceRoots(string invalid)
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        var funding = await fixture.Context.Set<MarketplaceFundingFragmentRow>().SingleAsync();
        funding.SelectedRootRanges = invalid switch
        {
            "name" => "[{\"other\":\"value\"}]",
            "kind" => "[{\"rootSourceStampId\":7}]",
            "guid" => "[{\"rootSourceStampId\":\"invalid\"}]",
            _ => "[]"
        };
        await fixture.Context.SaveChangesAsync();

        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, "no-roots")).AsTask())
            .Should().ThrowAsync<MarketplaceRefundException>();
    }

    [Fact]
    public async Task Refund_RejectsUnexpectedPostingIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var settlement = await fixture.Settlements.SettleAsync(fixture.SettlementRequest());
        fixture.RefundLedger.ReturnUnexpectedPosting = true;

        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(
                fixture.RefundRequest(settlement.SettlementId, 1, "unexpected-posting")).AsTask())
            .Should().ThrowAsync<RegisteredPostingRejectedException>();
    }

    [Fact]
    public async Task DurableServices_RejectNonDbContextApplicationContext()
    {
        await using var fixture = await Fixture.CreateAsync();
        var context = new StubApplicationDbContext();

        FluentActions.Invoking(() => new DurableMarketplaceSettlementService(
                context, fixture.Orders, fixture.Policies, fixture.Wallets, fixture.Reservations,
                fixture.ActorContexts, fixture.Jurisdictions, fixture.Orchestrator,
                fixture.Authority, fixture.SettlementLedger))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new DurableMarketplaceRefundService(
                context, fixture.Policies, fixture.ActorContexts, fixture.Jurisdictions,
                fixture.Orchestrator, fixture.Authority, fixture.RefundLedger))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("order")]
    [InlineData("choice")]
    public async Task Settlement_RejectsInvalidBusinessIntent(string invalid)
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.SettlementRequest() with
        {
            OrderId = invalid == "order" ? Guid.Empty : fixture.OrderId,
            CurrencyChoice = invalid == "choice" ? (MarketplaceCurrencyChoice)999 : MarketplaceCurrencyChoice.Hard
        };
        await FluentActions.Awaiting(() => fixture.Settlements.SettleAsync(request).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("settlement")]
    [InlineData("authority")]
    [InlineData("quantity")]
    [InlineData("reason")]
    [InlineData("reason-long")]
    public async Task Refund_RejectsInvalidBusinessIntent(string invalid)
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.RefundRequest(Guid.NewGuid(), 1, "reason") with
        {
            SettlementId = invalid == "settlement" ? Guid.Empty : Guid.NewGuid(),
            Authority = invalid == "authority" ? (MarketplaceRefundAuthority)999 : MarketplaceRefundAuthority.SelfService,
            Quantity = invalid == "quantity" ? 0 : 1,
            ReasonCode = invalid == "reason" ? " " : invalid == "reason-long" ? new string('x', 101) : "reason"
        };
        await FluentActions.Awaiting(() => fixture.Refunds.RefundAsync(request).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(MarketplaceTestContext context)
        {
            Context = context;
            Order = new AuthoritativeMarketplaceOrderSnapshot(
                TenantId, OrderId, Guid.NewGuid(), BuyerId, ProductId, SellerId, Guid.NewGuid(),
                3, 2, 5.5m, "USD", "order-snapshot-hash");
            Orders = new OrderReader(Order);
            var policy = ProductCurrencyPolicyVersion.Create(
                ProductId, SellerId, 7, ProductCurrencyMode.HardOnly,
                100, 0, 100_000, Now.AddDays(-1));
            Policies = new PolicyReader(new DurableMarketplacePolicySnapshot(
                TenantId, policy, PlatformWallet.WalletId.Value, TimeSpan.FromDays(7),
                "policy-hash", "key-1", "signature"));
            Wallets = new WalletDirectory(BuyerWallet, SellerWallet, PlatformWallet, BuyerId, SellerId);
            Reservations = new ReservationGateway();
            ActorContexts = new TestActorContextAccessor();
            SetActor(BuyerId);
            Jurisdictions = new JurisdictionResolver();
            Orchestrator = new ProtectedOperationOrchestrator(ActorContexts, RiskDecisionId);
            Authority = new AuthorityResolver();
            SettlementLedger = new SettlementGateway(context, Reservations);
            RefundLedger = new RefundGateway(context);
            Settlements = new DurableMarketplaceSettlementService(
                context, Orders, Policies, Wallets, Reservations, ActorContexts, Jurisdictions,
                Orchestrator, Authority, SettlementLedger);
            Refunds = new DurableMarketplaceRefundService(
                context, Policies, ActorContexts, Jurisdictions, Orchestrator, Authority, RefundLedger);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid BuyerId { get; } = Guid.NewGuid();
        public Guid SellerId { get; } = Guid.NewGuid();
        public Guid ProductId { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();
        public Guid RiskDecisionId { get; } = Guid.NewGuid();
        public EconomyWalletIdentity BuyerWallet { get; } = new(WalletId.New(), Guid.Empty, Guid.Empty, WalletLifecycleState.Active);
        public EconomyWalletIdentity SellerWallet { get; } = new(WalletId.New(), Guid.Empty, Guid.Empty, WalletLifecycleState.Active);
        public EconomyWalletIdentity PlatformWallet { get; } = new(WalletId.New(), Guid.Empty, Guid.Empty, WalletLifecycleState.Active);
        public MarketplaceTestContext Context { get; }
        public AuthoritativeMarketplaceOrderSnapshot Order { get; }
        public OrderReader Orders { get; }
        public PolicyReader Policies { get; }
        public WalletDirectory Wallets { get; }
        public ReservationGateway Reservations { get; }
        public TestActorContextAccessor ActorContexts { get; }
        public JurisdictionResolver Jurisdictions { get; }
        public ProtectedOperationOrchestrator Orchestrator { get; }
        public AuthorityResolver Authority { get; }
        public SettlementGateway SettlementLedger { get; }
        public RefundGateway RefundLedger { get; }
        public DurableMarketplaceSettlementService Settlements { get; }
        public DurableMarketplaceRefundService Refunds { get; }

        public static async Task<Fixture> CreateAsync(bool bypassDatabaseConstraints = false)
        {
            var context = new MarketplaceTestContext(bypassDatabaseConstraints);
            if (!bypassDatabaseConstraints)
                await context.Database.OpenConnectionAsync();
            await context.Database.EnsureCreatedAsync();
            return new Fixture(context);
        }

        public SettleAuthoritativeMarketplaceOrderRequest SettlementRequest() => new(
            OrderId, MarketplaceCurrencyChoice.Hard, new IdempotencyKey("settlement-1"), Now);

        public RefundAuthoritativeMarketplaceOrderRequest RefundRequest(
            Guid settlementId, int quantity, string reason, string key = "refund-1") => new(
            MarketplaceRefundAuthority.SelfService, settlementId, quantity, reason,
            new IdempotencyKey(key), Now.AddHours(1));

        public void SetActor(Guid actorId) => ActorContexts.Set(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = TenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class MarketplaceTestContext : DbContext, IApplicationDbContext
    {
        public MarketplaceTestContext(bool bypassDatabaseConstraints) : base(CreateOptions(bypassDatabaseConstraints)) { }

        private static DbContextOptions<MarketplaceTestContext> CreateOptions(bool bypassDatabaseConstraints)
        {
            var builder = new DbContextOptionsBuilder<MarketplaceTestContext>();
            if (bypassDatabaseConstraints)
                builder.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            else
                builder.UseSqlite("Data Source=:memory:");
            return builder.Options;
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);
        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(
            CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class OrderReader(AuthoritativeMarketplaceOrderSnapshot snapshot) : IAuthoritativeMarketplaceOrderReader
    {
        public ValueTask<AuthoritativeMarketplaceOrderSnapshot> ReadAsync(Guid tenantId, Guid buyerId,
            Guid orderId, CancellationToken cancellationToken = default) => ValueTask.FromResult(snapshot);
    }

    private sealed class PolicyReader(DurableMarketplacePolicySnapshot snapshot) : IDurableMarketplacePolicyReader
    {
        public DurableMarketplacePolicySnapshot Snapshot { get; set; } = snapshot;
        public ValueTask<DurableMarketplacePolicySnapshot> GetEffectiveAsync(Guid tenantId, Guid productId,
            DateTimeOffset at, CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);
        public ValueTask<DurableMarketplacePolicySnapshot> GetVersionAsync(Guid tenantId, Guid productId,
            long version, CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);
    }

    private sealed class WalletDirectory(
        EconomyWalletIdentity buyer,
        EconomyWalletIdentity seller,
        EconomyWalletIdentity platform,
        Guid buyerId,
        Guid sellerId) : IEconomyWalletDirectory
    {
        public EconomyWalletIdentity Buyer { get; } = buyer;
        public EconomyWalletIdentity Seller { get; } = seller;
        public EconomyWalletIdentity Platform { get; set; } = platform;
        public ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(Guid tenantId, Guid ownerId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ownerId == buyerId ? Buyer : ownerId == sellerId ? Seller : throw new KeyNotFoundException());
        public ValueTask<EconomyWalletIdentity> GetWalletAsync(Guid tenantId, WalletId walletId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Platform);
    }

    private sealed class ReservationGateway : IMarketplaceFifoReservationGateway
    {
        public List<MarketplaceFifoReservationRequest> Requests { get; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Last { get; private set; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Reserve(MarketplaceFifoReservationRequest request)
        {
            Requests.Add(request);
            Last = request.Legs.Select((leg, index) =>
            {
                var root = SourceStampId.New();
                var range = new RootTraceRange(root, 0, leg.Units * CurrencyTraceScale.For(leg.Currency), 0);
                return new PersistedFragmentReservation(
                    Guid.NewGuid(), request.OperationId, CreditLotId.New(), root, 0, range, leg);
            }).ToArray();
            return Last;
        }
    }

    private sealed class JurisdictionResolver : IEconomyJurisdictionResolver
    {
        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId,
            Guid actorId,
            string? providerJurisdiction,
            string? destinationJurisdiction,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new EconomyJurisdictionResolution("BR", 1, 1, "jurisdiction-evidence"));
    }

    private sealed class TestActorContextAccessor : IActorContextAccessor
    {
        public ActorContext ActorContext { get; private set; } = ActorContext.Anonymous;
        public void Set(ActorContext context) => SetActorContext(context);
        public void SetActorContext(ActorContext context) => ActorContext = context;
        public void ClearActorContext() => ActorContext = ActorContext.Anonymous;
    }

    private sealed class ProtectedOperationOrchestrator(
        TestActorContextAccessor actorContexts,
        Guid riskDecisionId) : IEconomyProtectedOperationOrchestrator
    {
        public List<EconomyProtectedOperationIntent> Intents { get; } = [];
        public List<EconomyProtectedOperationAuthorization> Authorizations { get; } = [];
        public string? Mismatch { get; set; }

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            Intents.Add(intent);
            var actor = actorContexts.ActorContext;
            var tenantId = actor.TenantId ?? throw new InvalidOperationException();
            var actorId = actor.SubjectIdAsGuid ?? throw new InvalidOperationException();
            var fingerprint = $"server-fingerprint-{Intents.Count}";
            var authorizedTenantId = Mismatch == "tenant" ? Guid.NewGuid() : tenantId;
            var authorizedActorId = Mismatch == "actor" ? Guid.NewGuid() : actorId;
            var jurisdictionCode = Mismatch == "jurisdiction" ? "US" : "BR";
            var providerHash = Mismatch == "provider" ? "other-provider" : intent.ProviderReferenceHash;
            var destinationHash = Mismatch == "destination" ? "other-destination" : intent.DestinationHash;
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), authorizedTenantId, authorizedActorId,
                EconomySubjectReference.ForUser(tenantId, actorId),
                jurisdictionCode, intent.Capability, fingerprint,
                7, 11, riskDecisionId, 3, providerHash, destinationHash,
                intent.SourceRoots.Select(root => root.Value.ToString("N")).ToArray(),
                ["evidence"], intent.RequestedAt, intent.RequestedAt.AddMinutes(5),
                "receipt-hash", "receipt-key", "receipt-signature");
            var authorization = new EconomyProtectedOperationAuthorization(
                authorizedTenantId, authorizedActorId, jurisdictionCode, riskDecisionId, fingerprint, receipt);
            Authorizations.Add(authorization);
            return await operation(authorization, cancellationToken);
        }
    }

    private sealed class AuthorityResolver : IRegisteredPostingCapabilityResolver
    {
        public List<(string Name, PostingTemplateKind Kind)> Requests { get; } = [];
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

    private sealed class SettlementGateway(MarketplaceTestContext context, ReservationGateway reservations)
        : IMarketplaceSettlementLedgerGateway
    {
        public List<PersistedMarketplaceSettlementRequest> Requests { get; } = [];
        public bool ReturnUnexpectedPosting { get; set; }
        public RegisteredPostingReceipt Settle(PersistedMarketplaceSettlementRequest request)
        {
            Requests.Add(request);
            if (ReturnUnexpectedPosting)
                return new RegisteredPostingReceipt(PostingId.New(), 100, "unexpected", false);
            context.Add(new MarketplaceSettlementRow
            {
                Id = request.SettlementId,
                TenantId = request.Authority.TenantId,
                OrderId = request.Order.OrderId,
                OrderLineItemId = request.Order.OrderLineItemId,
                ProductId = request.Order.ProductId,
                ProductPricingVersionId = request.Order.ProductPricingVersionId,
                PriceVersionSnapshot = request.Order.PriceVersion,
                Quantity = request.Order.Quantity,
                RefundedQuantity = 0,
                UnitPriceSnapshot = request.Order.UnitPrice,
                FiatCurrencySnapshot = request.Order.FiatCurrency,
                OrderSnapshotHash = request.Order.SnapshotHash,
                BuyerId = request.BuyerId,
                BuyerWalletId = request.BuyerWalletId.Value,
                SellerId = request.SellerId,
                SellerWalletId = request.SellerWalletId.Value,
                PlatformFeeWalletId = request.PlatformFeeWalletId.Value,
                PolicyVersion = request.MarketplacePolicyVersion,
                CurrencyMode = (ProductCurrencyMode)request.CurrencyMode,
                Status = MarketplaceSettlementStatus.Settled,
                IdempotencyKey = request.IdempotencyKey.Value,
                EntitlementId = request.EntitlementId,
                EntitlementStatus = MarketplaceEntitlementStatus.PendingGrant,
                PostingId = request.PostingId.Value,
                JournalSequence = 100,
                JournalHash = "settlement-journal-hash",
                CapabilityReceiptId = request.CapabilityReceipt.Id,
                CapabilityReceiptHash = request.CapabilityReceipt.ReceiptHash,
                ReserveVersion = request.CapabilityReceipt.ReserveVersion,
                RiskDecisionId = request.CapabilityReceipt.RiskDecisionId,
                KillSwitchEpoch = request.CapabilityReceipt.KillSwitchEpoch,
                JurisdictionCode = request.CapabilityReceipt.JurisdictionCode,
                EvidenceHashes = "[\"evidence\"]",
                RefundHoldUntil = request.RefundHoldUntil,
                SettledAt = request.SettledAt,
                UpdatedAt = request.SettledAt,
                Version = 1
            });
            foreach (var leg in request.Legs)
                context.Add(new MarketplaceSettlementLegRow
                {
                    SettlementId = request.SettlementId,
                    Currency = leg.Currency,
                    Units = leg.Units,
                    SellerUnits = leg.SellerUnits,
                    PlatformFeeUnits = leg.PlatformFeeUnits,
                    RefundedUnits = 0
                });
            foreach (var reservation in reservations.Last)
                context.Add(new MarketplaceFundingFragmentRow
                {
                    Id = Guid.NewGuid(),
                    SettlementId = request.SettlementId,
                    ParentLotId = reservation.ParentLotId.Value,
                    Currency = reservation.Amount.Currency,
                    AmountUnits = reservation.Amount.Units,
                    ReservationId = reservation.Id,
                    TraceUnitsPerCoinUnit = CurrencyTraceScale.For(reservation.Amount.Currency),
                    SelectedRootRanges = $$"""[{"rootSourceStampId":"{{reservation.RootSourceStampId.Value}}"}]"""
                });
            context.SaveChanges();
            return new RegisteredPostingReceipt(request.PostingId, 100, "settlement-journal-hash", false);
        }
    }

    private sealed class RefundGateway(MarketplaceTestContext context) : IMarketplaceRefundLedgerGateway
    {
        public List<PersistedMarketplaceRefundRequest> Requests { get; } = [];
        public bool AddDebt { get; set; }
        public bool ReturnUnexpectedPosting { get; set; }
        public RegisteredPostingReceipt Refund(PersistedMarketplaceRefundRequest request)
        {
            Requests.Add(request);
            var settlement = context.Set<MarketplaceSettlementRow>().Single(row => row.Id == request.SettlementId);
            settlement.RefundedQuantity = request.CumulativeRefundedQuantity;
            settlement.Status = settlement.RefundedQuantity == settlement.Quantity
                ? MarketplaceSettlementStatus.Refunded
                : MarketplaceSettlementStatus.PartiallyRefunded;
            if (settlement.Status == MarketplaceSettlementStatus.Refunded)
                settlement.EntitlementStatus = MarketplaceEntitlementStatus.Revoked;
            settlement.UpdatedAt = request.RefundedAt;
            settlement.Version++;
            context.Add(new MarketplaceRefundRow
            {
                Id = request.RefundId,
                TenantId = request.Authority.TenantId,
                SettlementId = request.SettlementId,
                BuyerId = request.BuyerId,
                IdempotencyKey = request.IdempotencyKey.Value,
                IsFullRefund = settlement.Status == MarketplaceSettlementStatus.Refunded,
                EntitlementRevoked = settlement.EntitlementStatus == MarketplaceEntitlementStatus.Revoked,
                FirstJournalSequence = 200 + Requests.Count,
                PostingId = request.PostingId.Value,
                JournalHash = "refund-journal-hash",
                ReasonCode = request.ReasonCode,
                ReasonHash = request.ReasonHash,
                Quantity = request.Quantity,
                RefundedQuantity = request.CumulativeRefundedQuantity,
                MarketplacePolicyVersion = request.MarketplacePolicyVersion,
                PolicyVersion = request.CapabilityReceipt.PolicyVersion,
                CapabilityReceiptId = request.CapabilityReceipt.Id,
                CapabilityReceiptHash = request.CapabilityReceipt.ReceiptHash,
                ReserveVersion = request.CapabilityReceipt.ReserveVersion,
                RiskDecisionId = request.CapabilityReceipt.RiskDecisionId,
                KillSwitchEpoch = request.CapabilityReceipt.KillSwitchEpoch,
                JurisdictionCode = request.CapabilityReceipt.JurisdictionCode,
                EvidenceHashes = "[\"evidence\"]",
                RefundedAt = request.RefundedAt
            });
            foreach (var leg in request.Legs)
            {
                context.Add(new MarketplaceRefundLegRow
                {
                    RefundId = request.RefundId,
                    SettlementId = request.SettlementId,
                    Currency = leg.Currency,
                    Units = leg.Units
                });
                var settlementLeg = context.Set<MarketplaceSettlementLegRow>()
                    .Single(item => item.SettlementId == request.SettlementId && item.Currency == leg.Currency);
                settlementLeg.RefundedUnits += leg.Units;
            }
            if (AddDebt)
                context.Add(new MarketplaceRefundDebtRow
                {
                    Id = Guid.NewGuid(), TenantId = request.Authority.TenantId, RefundId = request.RefundId,
                    SettlementId = request.SettlementId, ResponsibleWalletId = settlement.SellerWalletId,
                    Currency = request.Legs[0].Currency, AmountUnits = 10,
                    EvidenceHash = "debt-evidence", RecordedAt = request.RefundedAt
                });
            context.SaveChanges();
            return new RegisteredPostingReceipt(
                ReturnUnexpectedPosting ? PostingId.New() : request.PostingId,
                200 + Requests.Count, "refund-journal-hash", false);
        }
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
