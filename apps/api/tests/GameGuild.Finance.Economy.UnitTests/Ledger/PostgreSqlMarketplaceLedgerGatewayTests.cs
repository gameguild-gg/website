using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class PostgreSqlMarketplaceLedgerGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReserveSettleAndRefundUseTheProtectedPostgreSqlWriters()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_ledger_gateway");
        await using var context = CreateContext(database.ConnectionString);
        var ids = TestIds.Create();
        await SeedAsync(database.ConnectionString, ids);
        var gateway = new PostgreSqlMarketplaceLedgerGateway(context);

        var reservationRequest = new MarketplaceFifoReservationRequest(
            ids.Settlement, new WalletId(ids.BuyerWallet),
            [new CoinAmount(CurrencyCode.HardCoin, 100)], Now);
        var reservations = gateway.Reserve(reservationRequest);
        var reservationReplay = gateway.Reserve(reservationRequest);

        reservations.Should().ContainSingle();
        reservationReplay.Should().BeEquivalentTo(reservations);
        reservations[0].Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 100));
        reservations[0].Range.Length.Should().Be(100_000);

        var settlement = Settlement(ids, reservations.Select(item => item.Id).ToArray());
        var settlementReceipt = gateway.Settle(settlement);
        var settlementReplay = gateway.Settle(settlement);
        settlementReceipt.PostingId.Should().Be(settlement.PostingId);
        settlementReceipt.IsDuplicate.Should().BeFalse();
        settlementReplay.IsDuplicate.Should().BeTrue();
        settlementReplay.JournalSequence.Should().Be(settlementReceipt.JournalSequence);

        var refund = Refund(ids);
        var refundReceipt = gateway.Refund(refund);
        var refundReplay = gateway.Refund(refund);
        refundReceipt.PostingId.Should().Be(refund.PostingId);
        refundReceipt.IsDuplicate.Should().BeFalse();
        refundReplay.IsDuplicate.Should().BeTrue();
        refundReplay.JournalSequence.Should().Be(refundReceipt.JournalSequence);
    }

    [Fact]
    public async Task DatabaseFailuresAreTranslatedForEveryGatewayOperation()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_gateway_failures");
        await using var context = CreateContext(database.ConnectionString);
        var gateway = new PostgreSqlMarketplaceLedgerGateway(context);
        var ids = TestIds.Create();

        FluentActions.Invoking(() => gateway.Reserve(new MarketplaceFifoReservationRequest(
                Guid.NewGuid(), WalletId.New(), [new CoinAmount(CurrencyCode.HardCoin, 1)], Now)))
            .Should().Throw<RegisteredPostingRejectedException>().WithMessage("*FIFO writer rejected*");
        FluentActions.Invoking(() => gateway.Settle(Settlement(ids, [Guid.NewGuid()])))
            .Should().Throw<RegisteredPostingRejectedException>().WithMessage("*rejected the settlement*");
        FluentActions.Invoking(() => gateway.Refund(Refund(ids)))
            .Should().Throw<RegisteredPostingRejectedException>().WithMessage("*rejected the refund*");
    }

    [Fact]
    public void ReservationAndConstructionValidationFailClosed()
    {
        Action nullContext = () => new PostgreSqlMarketplaceLedgerGateway(null!);
        Action nonRelational = () => new PostgreSqlMarketplaceLedgerGateway(new StubApplicationDbContext());
        nullContext.Should().Throw<ArgumentNullException>();
        nonRelational.Should().Throw<InvalidOperationException>().WithMessage("*relational DbContext*");

        using var context = OfflineContext();
        var gateway = new PostgreSqlMarketplaceLedgerGateway(context);
        var valid = new MarketplaceFifoReservationRequest(
            Guid.NewGuid(), WalletId.New(), [new CoinAmount(CurrencyCode.HardCoin, 1)], Now);
        FluentActions.Invoking(() => gateway.Reserve(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with { OperationId = Guid.Empty })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with { Legs = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with { Legs = [] })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with
            { Legs = [new CoinAmount(CurrencyCode.HardCoin, 0)] })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Reserve(valid with
            {
                Legs =
                [
                    new CoinAmount(CurrencyCode.HardCoin, 1),
                    new CoinAmount(CurrencyCode.HardCoin, 2)
                ]
            })).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SettlementValidationRejectsEveryInvalidBoundary()
    {
        using var context = OfflineContext();
        var gateway = new PostgreSqlMarketplaceLedgerGateway(context);
        var ids = TestIds.Create();
        var valid = Settlement(ids, [Guid.NewGuid()]);

        FluentActions.Invoking(() => gateway.Settle(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Settle(valid with { Authority = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Settle(valid with { CapabilityReceipt = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Settle(valid with { Order = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Settle(valid with { Legs = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Settle(valid with { ReservationIds = null! })).Should().Throw<ArgumentNullException>();
        AssertSettlementInvalid(gateway, valid with { SettlementId = Guid.Empty });
        AssertSettlementInvalid(gateway, valid with { BuyerId = Guid.Empty });
        AssertSettlementInvalid(gateway, valid with { SellerId = Guid.Empty });
        AssertSettlementInvalid(gateway, valid with { EntitlementId = Guid.Empty });
        AssertSettlementInvalid(gateway, valid with { SellerId = valid.BuyerId });
        AssertSettlementInvalid(gateway, valid with { Legs = [] });
        AssertSettlementInvalid(gateway, valid with
            { Legs = [new PersistedMarketplacePriceLeg(CurrencyCode.HardCoin, 0, 0, 0)] });
        AssertSettlementInvalid(gateway, valid with
            { Legs = [new PersistedMarketplacePriceLeg(CurrencyCode.HardCoin, 10, -1, 11)] });
        AssertSettlementInvalid(gateway, valid with
            { Legs = [new PersistedMarketplacePriceLeg(CurrencyCode.HardCoin, 10, 11, -1)] });
        AssertSettlementInvalid(gateway, valid with
            { Legs = [new PersistedMarketplacePriceLeg(CurrencyCode.HardCoin, 10, 8, 1)] });
        AssertSettlementInvalid(gateway, valid with { ReservationIds = [] });
        AssertSettlementInvalid(gateway, valid with { ReservationIds = [Guid.Empty] });
        var reservation = Guid.NewGuid();
        AssertSettlementInvalid(gateway, valid with { ReservationIds = [reservation, reservation] });
        AssertSettlementInvalid(gateway, valid with { RefundHoldUntil = valid.SettledAt });
        FluentActions.Invoking(() => gateway.Settle(valid with
            {
                Legs = [new PersistedMarketplacePriceLeg(
                    CurrencyCode.HardCoin, long.MaxValue, long.MaxValue, 1)]
            })).Should().Throw<OverflowException>();
    }

    [Fact]
    public void RefundValidationRejectsEveryInvalidBoundary()
    {
        using var context = OfflineContext();
        var gateway = new PostgreSqlMarketplaceLedgerGateway(context);
        var valid = Refund(TestIds.Create());

        FluentActions.Invoking(() => gateway.Refund(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Refund(valid with { Authority = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Refund(valid with { CapabilityReceipt = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Refund(valid with { Legs = null! })).Should().Throw<ArgumentNullException>();
        AssertRefundInvalid(gateway, valid with { RefundId = Guid.Empty });
        AssertRefundInvalid(gateway, valid with { SettlementId = Guid.Empty });
        AssertRefundInvalid(gateway, valid with { BuyerId = Guid.Empty });
        AssertRefundInvalid(gateway, valid with { MarketplacePolicyVersion = 0 });
        AssertRefundInvalid(gateway, valid with { Quantity = 0 });
        AssertRefundInvalid(gateway, valid with { CumulativeRefundedQuantity = 0 });
        AssertRefundInvalid(gateway, valid with { Legs = [] });
        AssertRefundInvalid(gateway, valid with
            { Legs = [new PersistedMarketplaceRefundLeg(CurrencyCode.HardCoin, 0)] });
        AssertRefundInvalid(gateway, valid with
            {
                Legs =
                [
                    new PersistedMarketplaceRefundLeg(CurrencyCode.HardCoin, 1),
                    new PersistedMarketplaceRefundLeg(CurrencyCode.HardCoin, 1)
                ]
            });
        FluentActions.Invoking(() => gateway.Refund(valid with { ReasonCode = " " })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Refund(valid with { ReasonHash = " " })).Should().Throw<ArgumentException>();
    }

    private static void AssertSettlementInvalid(
        PostgreSqlMarketplaceLedgerGateway gateway,
        PersistedMarketplaceSettlementRequest request) =>
        FluentActions.Invoking(() => gateway.Settle(request)).Should().Throw<ArgumentException>();

    private static void AssertRefundInvalid(
        PostgreSqlMarketplaceLedgerGateway gateway,
        PersistedMarketplaceRefundRequest request) =>
        FluentActions.Invoking(() => gateway.Refund(request)).Should().Throw<ArgumentException>();

    private static PersistedMarketplaceSettlementRequest Settlement(TestIds ids, IReadOnlyList<Guid> reservations) => new(
        new RegisteredPostingAuthority(ids.Capability, ids.Actor, ids.Tenant, ids.SettlementRisk,
            "marketplace-settlement-risk", 1),
        Receipt(ids, true),
        ids.Settlement,
        new PostingId(ids.SettlementPosting),
        new IdempotencyKey("marketplace-settlement"),
        new PersistedMarketplaceOrderSnapshot(
            ids.Order, ids.LineItem, ids.Product, ids.Pricing, 1, 1, 1m, "USD", "order-snapshot"),
        ids.Buyer,
        new WalletId(ids.BuyerWallet),
        ids.Seller,
        new WalletId(ids.SellerWallet),
        new WalletId(ids.FeeWallet),
        1,
        1,
        [new PersistedMarketplacePriceLeg(CurrencyCode.HardCoin, 100, 90, 10)],
        reservations,
        ids.Entitlement,
        Now.AddDays(7),
        Now);

    private static PersistedMarketplaceRefundRequest Refund(TestIds ids) => new(
        new RegisteredPostingAuthority(ids.Capability, ids.Actor, ids.Tenant, ids.RefundRisk,
            "marketplace-refund-risk", 1),
        Receipt(ids, false),
        ids.Refund,
        ids.Settlement,
        new PostingId(ids.RefundPosting),
        new IdempotencyKey("marketplace-refund"),
        ids.Buyer,
        1,
        1,
        1,
        [new PersistedMarketplaceRefundLeg(CurrencyCode.HardCoin, 100)],
        " CUSTOMER_REQUEST ",
        "refund-reason",
        Now.AddMinutes(2));

    private static CapabilityAuthorizationReceipt Receipt(TestIds ids, bool settlement) => new(
        settlement ? ids.SettlementReceipt : ids.RefundReceipt,
        ids.Tenant,
        ids.Actor,
        "buyer",
        "US",
        settlement ? EconomyValueMovementCapability.MarketplaceSettlement : EconomyValueMovementCapability.MarketplaceRefund,
        settlement ? "marketplace-settlement-risk" : "marketplace-refund-risk",
        1,
        1,
        settlement ? ids.SettlementRisk : ids.RefundRisk,
        0,
        "provider",
        "destination",
        ["root"],
        [settlement ? "settlement-evidence" : "refund-evidence"],
        Now.AddMinutes(-1),
        Now.AddMinutes(5),
        settlement ? "settlement-receipt" : "refund-receipt",
        "test-key",
        "signature");

    private static async Task SeedAsync(string connectionString, TestIds ids)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('{ids.BuyerWallet}', '{ids.Buyer}', '{ids.Tenant}', 1, '{Now.AddDays(-40):O}'),
                ('{ids.SellerWallet}', '{ids.Seller}', '{ids.Tenant}', 1, '{Now.AddDays(-40):O}'),
                ('{ids.FeeWallet}', '{ids.FeeOwner}', '{ids.Tenant}', 1, '{Now.AddDays(-40):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', '{ids.BuyerWallet}', 2, 1, 1, '{Now.AddDays(-40):O}'),
                ('{Guid.NewGuid()}', '{ids.SellerWallet}', 3, 1, 2, '{Now.AddDays(-40):O}'),
                ('{Guid.NewGuid()}', '{ids.FeeWallet}', 3, 1, 2, '{Now.AddDays(-40):O}'),
                ('{Guid.NewGuid()}', NULL, 13, 1, NULL, '{Now.AddDays(-40):O}');
            INSERT INTO public.economy_registered_capabilities
                ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{ids.Capability}', 'marketplace-production-writer', '[25,26]'::jsonb, true, '{Now.AddDays(-1):O}', NULL);
            INSERT INTO public.economy_risk_counters
                ("Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                 "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt") VALUES
                ('{ids.SettlementCounter}', 1, 'marketplace-settlement', 1, 1, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 1000, 0, '{Now.AddHours(-1):O}'),
                ('{ids.RefundCounter}', 1, 'marketplace-refund', 1, 1, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 1000, 0, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions
                ("Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
                 "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
                 "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "ReserveAuthorizationEpoch",
                 "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                 "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt") VALUES
                ('{ids.SettlementRisk}', 1, 'marketplace-settlement-risk', 'actor', 25, '{ids.BuyerWallet}', '{ids.SellerWallet}', 1, 100, '[]', '[]', 'provider', 1, 1, 1, 1, 0, 1, 1, 'graph', '[]', '{Now.AddMinutes(-5):O}', '{Now.AddHours(1):O}'),
                ('{ids.RefundRisk}', 1, 'marketplace-refund-risk', 'actor', 26, '{ids.SellerWallet}', '{ids.BuyerWallet}', 1, 100, '[]', '[]', 'provider', 1, 1, 1, 1, 0, 1, 1, 'graph', '[]', '{Now.AddMinutes(-5):O}', '{Now.AddHours(1):O}');
            INSERT INTO public.economy_risk_counter_reservations
                ("Id", "RiskDecisionId", "RiskCounterId", "AmountUnits", "ReservedAt", "ExpiresAt",
                 "InputFingerprint", "ReservationGroupId", "Status") VALUES
                ('{Guid.NewGuid()}', '{ids.SettlementRisk}', '{ids.SettlementCounter}', 100, '{Now.AddMinutes(-4):O}', '{Now.AddHours(1):O}', 'settlement-reservation', '{Guid.NewGuid()}', 1),
                ('{Guid.NewGuid()}', '{ids.RefundRisk}', '{ids.RefundCounter}', 100, '{Now.AddMinutes(-4):O}', '{Now.AddHours(1):O}', 'refund-reservation', '{Guid.NewGuid()}', 1);
            INSERT INTO public.economy_capability_receipts
                ("Id", "TenantId", "ActorId", "SubjectReference", "JurisdictionCode", "Capability",
                 "OperationFingerprint", "PolicyVersion", "ReserveVersion", "RiskDecisionId", "KillSwitchEpoch",
                 "ProviderHash", "DestinationHash", "SourceRootHashes", "EvidenceHashes", "IssuedAt", "ExpiresAt",
                 "ReceiptHash", "KeyId", "Signature") VALUES
                ('{ids.SettlementReceipt}', '{ids.Tenant}', '{ids.Actor}', 'buyer', 'US', 8, 'marketplace-settlement-risk', 1, 1, '{ids.SettlementRisk}', 0, 'provider', 'destination', '["root"]', '["settlement-evidence"]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}', 'settlement-receipt', 'test-key', 'signature'),
                ('{ids.RefundReceipt}', '{ids.Tenant}', '{ids.Actor}', 'buyer', 'US', 11, 'marketplace-refund-risk', 1, 1, '{ids.RefundRisk}', 0, 'provider', 'destination', '["root"]', '["refund-evidence"]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}', 'refund-receipt', 'test-key', 'signature');
            INSERT INTO public.economy_capability_receipt_consumptions
                ("Id", "ReceiptId", "TenantId", "ActorId", "OperationFingerprint", "KillSwitchEpoch", "ConsumedAt") VALUES
                ('{Guid.NewGuid()}', '{ids.SettlementReceipt}', '{ids.Tenant}', '{ids.Actor}', 'marketplace-settlement-risk', 0, '{Now:O}'),
                ('{Guid.NewGuid()}', '{ids.RefundReceipt}', '{ids.Tenant}', '{ids.Actor}', 'marketplace-refund-risk', 0, '{Now.AddMinutes(2):O}');
            INSERT INTO public.economy_marketplace_currency_policy_versions
                ("TenantId", "ProductId", "Version", "SellerId", "Mode", "HardPriceUnits", "SoftPriceUnits",
                 "PlatformFeePpm", "EffectiveAt", "ExpiresAt", "PlatformFeeWalletId", "RefundHoldTicks",
                 "CanonicalPayload", "PayloadHash", "KeyId", "Signature", "ProposedBy", "ApprovedBy", "PublishedAt")
            VALUES ('{ids.Tenant}', '{ids.Product}', 1, '{ids.Seller}', 1, 100, 0, 100000, '{Now.AddDays(-1):O}',
                '{Now.AddDays(30):O}', '{ids.FeeWallet}', {TimeSpan.FromDays(7).Ticks}, 'policy', 'policy-hash',
                'kms-key', 'signature', '{ids.PolicyProposer}', '{ids.PolicyApprover}', '{Now.AddDays(-1):O}');
            INSERT INTO public.economy_source_stamps
                ("Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference",
                 "EvidenceHash", "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId",
                 "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('{ids.Root}', 'test', 'marketplace-root', 'principal', NULL, NULL, 'source-evidence', 1, 2,
                '{ids.Actor}', '{ids.Tenant}', NULL, 1, 100, '{Now.AddDays(-30):O}', '{Now.AddDays(-30):O}');
            INSERT INTO public.economy_credit_lots
                ("Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance", "CreditedAt",
                 "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ('{ids.BuyerLot}', '{ids.BuyerWallet}', '{ids.Root}', 1, 100, 1, '{Now.AddDays(-30):O}',
                '{Now.AddDays(-30):O}', '{Now.AddDays(-30):O}', false, 1, 1, 0);
            INSERT INTO public.economy_root_reversal_states
                ("RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits", "State", "TargetedRanges", "UpdatedAt")
            VALUES ('{ids.Root}', 0, 0, 0, 'active', '[]', '{Now.AddDays(-30):O}');
            INSERT INTO public.economy_fragment_root_ranges
                ("Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ('{Guid.NewGuid()}', '{ids.Root}', '{ids.BuyerLot}', NULL, 0, 100000, 0);
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

    private static ApplicationDbContext OfflineContext() => CreateContext(
        "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1");

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed record TestIds(
        Guid Tenant, Guid Actor, Guid Buyer, Guid Seller, Guid FeeOwner,
        Guid BuyerWallet, Guid SellerWallet, Guid FeeWallet,
        Guid Capability, Guid SettlementCounter, Guid RefundCounter,
        Guid SettlementRisk, Guid RefundRisk, Guid SettlementReceipt, Guid RefundReceipt,
        Guid Product, Guid PolicyProposer, Guid PolicyApprover, Guid Root, Guid BuyerLot,
        Guid Settlement, Guid SettlementPosting, Guid Order, Guid LineItem, Guid Pricing,
        Guid Entitlement, Guid Refund, Guid RefundPosting)
    {
        public static TestIds Create() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }
}
