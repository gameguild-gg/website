using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class PostgreSqlMarketplaceOperationalQueryReaderTests
{
    private static readonly Guid TenantId = Guid.Parse("af000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListsOnlyActorTenantSettlementsRefundsAndOutboxWithStableCursors()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_queries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var settlements = Enumerable.Range(1, 3).Select(index => Settlement(TenantId, index)).ToArray();
        var foreign = Settlement(Guid.NewGuid(), 4);
        context.Set<MarketplaceSettlementRow>().AddRange(settlements.Append(foreign));
        context.Set<MarketplaceRefundRow>().AddRange(
            Refund(TenantId, settlements[0], 1),
            Refund(Guid.NewGuid(), foreign, 2));
        context.Set<MarketplaceOutboxRow>().AddRange(
            Outbox(TenantId, settlements[0], 1),
            Outbox(Guid.NewGuid(), foreign, 2));
        await context.SaveChangesAsync();
        var reader = new PostgreSqlMarketplaceOperationalQueryReader(context);

        var first = await reader.ListSettlementsAsync(TenantId, null, 2, null, default);
        var second = await reader.ListSettlementsAsync(TenantId, null, 2, first.NextCursor, default);
        var refunds = await reader.ListRefundsAsync(TenantId, 20, null, default);
        var outbox = await reader.ListOutboxAsync(TenantId, null, 20, null, default);

        first.Items.Select(item => item.OrderId).Should().Equal(settlements[0].OrderId, settlements[1].OrderId);
        second.Items.Select(item => item.OrderId).Should().Equal(settlements[2].OrderId);
        refunds.Items.Should().ContainSingle().Which.TenantId.Should().Be(TenantId);
        outbox.Items.Should().ContainSingle().Which.TenantId.Should().Be(TenantId);

        await FluentActions.Awaiting(() => reader.ListSettlementsAsync(
                Guid.Empty, null, 20, null, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListSettlementsAsync(
                TenantId, null, 0, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.ListSettlementsAsync(
                TenantId, null, 101, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task DetailIncludesSettlementLegsEventsRefundsAndRedactedOutboxMetadata()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_query_detail");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var settlement = Settlement(TenantId, 1);
        var refund = Refund(TenantId, settlement, 1);
        var outbox = Outbox(TenantId, settlement, 1);
        context.Set<MarketplaceSettlementRow>().Add(settlement);
        context.Set<MarketplaceSettlementLegRow>().Add(new MarketplaceSettlementLegRow
        {
            SettlementId = settlement.Id,
            Currency = CurrencyCode.HardCoin,
            Units = 100,
            SellerUnits = 90,
            PlatformFeeUnits = 10
        });
        context.Set<MarketplaceEventRow>().Add(new MarketplaceEventRow
        {
            Id = Guid.NewGuid(), TenantId = TenantId, SettlementId = settlement.Id,
            Sequence = 1, EventKind = "Settled", EvidenceHash = "event-evidence", OccurredAt = Now
        });
        context.Set<MarketplaceRefundRow>().Add(refund);
        context.Set<MarketplaceOutboxRow>().Add(outbox);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlMarketplaceOperationalQueryReader(context);

        var detail = await reader.FindSettlementAsync(TenantId, settlement.Id, default);
        var refundDetail = await reader.FindRefundAsync(TenantId, refund.Id, default);

        detail.Should().NotBeNull();
        detail!.Legs.Should().ContainSingle().Which.Units.Should().Be(100);
        detail.Events.Should().ContainSingle().Which.Kind.Should().Be("Settled");
        detail.Refunds.Should().ContainSingle();
        detail.Outbox.Should().ContainSingle().Which.PayloadHash.Should().Be("payload-hash-1");
        typeof(MarketplaceOutboxOperationalStatus).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(["Payload", "LastError", "LeaseOwner"]);
        refundDetail!.ReasonCode.Should().Be("buyer-request");
        (await reader.FindSettlementAsync(Guid.NewGuid(), settlement.Id, default)).Should().BeNull();
        await FluentActions.Awaiting(() => reader.FindSettlementAsync(
                Guid.Empty, settlement.Id, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindSettlementAsync(
                TenantId, Guid.Empty, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ConstructorAndCursorCodecRejectInvalidInfrastructureAndComponents()
    {
        FluentActions.Invoking(() => new PostgreSqlMarketplaceOperationalQueryReader(new NonDbContext()))
            .Should().Throw<InvalidOperationException>();

        var identifier = Guid.NewGuid().ToString("N");
        PostgreSqlMarketplaceOperationalQueryReader.DecodeCursor(null, "Settlement").Should().BeNull();
        PostgreSqlMarketplaceOperationalQueryReader.DecodeCursor("   ", "Settlement").Should().BeNull();
        PostgreSqlMarketplaceOperationalQueryReader.DecodeCursor(
            $"{Now.UtcTicks:X16}{identifier}", "Settlement").Should().NotBeNull();

        foreach (var cursor in new[]
                 {
                     "invalid",
                     $"ZZZZZZZZZZZZZZZZ{identifier}",
                     $"0000000000000001{new string('Z', 32)}",
                     $"FFFFFFFFFFFFFFFF{identifier}",
                     $"7FFFFFFFFFFFFFFF{identifier}"
                 })
        {
            FluentActions.Invoking(() =>
                    PostgreSqlMarketplaceOperationalQueryReader.DecodeCursor(cursor, "Settlement"))
                .Should().Throw<ArgumentException>();
        }
    }

    private static MarketplaceSettlementRow Settlement(Guid tenantId, int index) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OrderId = Guid.NewGuid(),
        OrderLineItemId = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        ProductPricingVersionId = Guid.NewGuid(),
        PriceVersionSnapshot = 1,
        Quantity = 1,
        UnitPriceSnapshot = 1,
        FiatCurrencySnapshot = "USD",
        OrderSnapshotHash = $"order-{index}",
        BuyerId = Guid.NewGuid(),
        BuyerWalletId = Guid.NewGuid(),
        SellerId = Guid.NewGuid(),
        SellerWalletId = Guid.NewGuid(),
        PlatformFeeWalletId = Guid.NewGuid(),
        PolicyVersion = 1,
        CurrencyMode = ProductCurrencyMode.HardOnly,
        Status = MarketplaceSettlementStatus.Settled,
        IdempotencyKey = $"settlement-{index}",
        EntitlementId = Guid.NewGuid(),
        EntitlementStatus = MarketplaceEntitlementStatus.Granted,
        PostingId = Guid.NewGuid(),
        JournalSequence = index,
        JournalHash = $"journal-{index}",
        CapabilityReceiptId = Guid.NewGuid(),
        CapabilityReceiptHash = $"receipt-{index}",
        ReserveVersion = 1,
        RiskDecisionId = Guid.NewGuid(),
        KillSwitchEpoch = 1,
        JurisdictionCode = "BR",
        EvidenceHashes = "[]",
        RefundHoldUntil = Now.AddHours(1),
        SettledAt = Now.AddMinutes(-index),
        UpdatedAt = Now,
        Version = 1
    };

    private static MarketplaceRefundRow Refund(Guid tenantId, MarketplaceSettlementRow settlement, int index) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, SettlementId = settlement.Id, BuyerId = settlement.BuyerId,
        IdempotencyKey = $"refund-{index}", IsFullRefund = true, EntitlementRevoked = true,
        FirstJournalSequence = index, PostingId = Guid.NewGuid(), JournalHash = $"refund-journal-{index}",
        ReasonCode = "buyer-request", ReasonHash = $"reason-{index}", Quantity = 1, RefundedQuantity = 1,
        MarketplacePolicyVersion = 1, PolicyVersion = 1, CapabilityReceiptId = Guid.NewGuid(),
        CapabilityReceiptHash = $"refund-receipt-{index}", ReserveVersion = 1, RiskDecisionId = Guid.NewGuid(),
        KillSwitchEpoch = 1, JurisdictionCode = "BR", EvidenceHashes = "[]", RefundedAt = Now
    };

    private static MarketplaceOutboxRow Outbox(Guid tenantId, MarketplaceSettlementRow settlement, int index) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, SettlementId = settlement.Id,
        MessageType = "MarketplaceEntitlementGranted", Payload = "{}", PayloadHash = $"payload-hash-{index}",
        OccurredAt = Now.AddMinutes(-index), AttemptCount = 0
    };

    private static QueryDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<QueryDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class QueryDbContext(DbContextOptions<QueryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class NonDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
