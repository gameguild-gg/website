using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class PostgreSqlMarketplaceOutboxProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 22, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("c1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ClaimsPublishesAndDoesNotRedeliverDurableMessage()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_outbox");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var settlement = SeedSettlement(context);
        var outbox = SeedOutbox(context, settlement, "marketplace.entitlement.grant.v1");
        await context.SaveChangesAsync();
        await RoundTripPayloadHashAsync(context, outbox.Id);
        var handler = new StubHandler();
        var processor = new PostgreSqlMarketplaceOutboxProcessor(context, handler);

        var published = await processor.ProcessNextAsync("worker-one", Now, CancellationToken.None);
        var noWork = await processor.ProcessNextAsync("worker-one", Now, CancellationToken.None);

        published.Status.Should().Be(MarketplaceOutboxProcessStatus.Published);
        noWork.Status.Should().Be(MarketplaceOutboxProcessStatus.NoWork);
        handler.Messages.Should().ContainSingle();
        var persisted = await context.Set<MarketplaceOutboxRow>().AsNoTracking().SingleAsync();
        persisted.PublishedAt.Should().Be(Now);
        persisted.AttemptCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LeaseExpiresAt.Should().BeNull();
        persisted.LastError.Should().BeNull();
    }

    [Fact]
    public async Task InvalidMessageFailsClosedAndReleasesLeaseForRetry()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("marketplace_outbox_invalid");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var settlement = SeedSettlement(context);
        var outbox = SeedOutbox(context, settlement, "marketplace.entitlement.grant.v1");
        outbox.PayloadHash = "invalid";
        await context.SaveChangesAsync();
        var handler = new StubHandler();
        var processor = new PostgreSqlMarketplaceOutboxProcessor(context, handler);

        var result = await processor.ProcessNextAsync("worker-one", Now, CancellationToken.None);

        result.Status.Should().Be(MarketplaceOutboxProcessStatus.Failed);
        result.Error.Should().Contain("hash");
        handler.Messages.Should().BeEmpty();
        var persisted = await context.Set<MarketplaceOutboxRow>().AsNoTracking().SingleAsync();
        persisted.PublishedAt.Should().BeNull();
        persisted.AttemptCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LastError.Should().Contain("hash");
        await FluentActions.Awaiting(() => processor.ProcessNextAsync(
                " ", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("array")]
    [InlineData("missing")]
    [InlineData("format")]
    [InlineData("mismatch")]
    public async Task InvalidSettlementBindingFailsClosed(string invalid)
    {
        await using var context = await SqliteOutboxDbContext.CreateAsync();
        var settlement = SeedSettlement(context);
        var outbox = SeedOutbox(context, settlement, "marketplace.entitlement.grant.v1");
        outbox.Payload = invalid switch
        {
            "array" => "[]",
            "missing" => "{}",
            "format" => "{\"settlementId\":\"invalid\"}",
            _ => JsonSerializer.Serialize(new { settlementId = Guid.NewGuid() })
        };
        outbox.PayloadHash = Hash(outbox.Payload);
        await context.SaveChangesAsync();
        var processor = new PostgreSqlMarketplaceOutboxProcessor(context, new StubHandler());

        var result = await processor.ProcessNextAsync("worker", Now, CancellationToken.None);

        result.Status.Should().Be(MarketplaceOutboxProcessStatus.Failed);
        result.Error.Should().Contain("settlement");
    }

    [Fact]
    public async Task HandlerCancellationIsPropagated()
    {
        await using var context = await SqliteOutboxDbContext.CreateAsync();
        var settlement = SeedSettlement(context);
        SeedOutbox(context, settlement, "marketplace.entitlement.grant.v1");
        await context.SaveChangesAsync();
        using var source = new CancellationTokenSource();
        var processor = new PostgreSqlMarketplaceOutboxProcessor(context, new CancelingHandler(source));

        await FluentActions.Awaiting(() => processor.ProcessNextAsync("worker", Now, source.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LongHandlerErrorIsTruncatedAndLeaseIsReleased()
    {
        await using var context = await SqliteOutboxDbContext.CreateAsync();
        var settlement = SeedSettlement(context);
        SeedOutbox(context, settlement, "marketplace.entitlement.grant.v1");
        await context.SaveChangesAsync();
        var processor = new PostgreSqlMarketplaceOutboxProcessor(
            context, new ThrowingHandler(new InvalidOperationException(new string('x', 1_100))));

        var result = await processor.ProcessNextAsync("worker", Now, CancellationToken.None);

        result.Status.Should().Be(MarketplaceOutboxProcessStatus.Failed);
        result.Error.Should().HaveLength(1_000);
        var row = await context.Set<MarketplaceOutboxRow>().SingleAsync();
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        row.LastError.Should().HaveLength(1_000);
    }

    [Fact]
    public void ConstructorRejectsInvalidDependencies()
    {
        var handler = new StubHandler();
        var context = new StubApplicationDbContext();

        FluentActions.Invoking(() => new PostgreSqlMarketplaceOutboxProcessor(null!, handler))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlMarketplaceOutboxProcessor(context, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlMarketplaceOutboxProcessor(context, handler))
            .Should().Throw<InvalidOperationException>();
    }

    private static MarketplaceSettlementRow SeedSettlement(DbContext context)
    {
        var row = new MarketplaceSettlementRow
        {
            Id = Guid.NewGuid(), TenantId = TenantId, OrderId = Guid.NewGuid(),
            OrderLineItemId = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductPricingVersionId = Guid.NewGuid(),
            PriceVersionSnapshot = 1, Quantity = 1, UnitPriceSnapshot = 1, FiatCurrencySnapshot = "USD",
            OrderSnapshotHash = "snapshot", BuyerId = Guid.NewGuid(), BuyerWalletId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(), SellerWalletId = Guid.NewGuid(), PlatformFeeWalletId = Guid.NewGuid(),
            PolicyVersion = 1, CurrencyMode = ProductCurrencyMode.HardOnly,
            Status = MarketplaceSettlementStatus.Settled, IdempotencyKey = "settlement-key",
            EntitlementId = Guid.NewGuid(), EntitlementStatus = MarketplaceEntitlementStatus.PendingGrant,
            PostingId = Guid.NewGuid(), JournalSequence = 1, JournalHash = "journal",
            CapabilityReceiptId = Guid.NewGuid(), CapabilityReceiptHash = "receipt", ReserveVersion = 1,
            RiskDecisionId = Guid.NewGuid(), JurisdictionCode = "BR", EvidenceHashes = "[]",
            RefundHoldUntil = Now.AddDays(1), SettledAt = Now.AddMinutes(-1), UpdatedAt = Now, Version = 1
        };
        context.Set<MarketplaceSettlementRow>().Add(row);
        return row;
    }

    private static MarketplaceOutboxRow SeedOutbox(
        DbContext context,
        MarketplaceSettlementRow settlement,
        string messageType)
    {
        var payload = JsonSerializer.Serialize(new
        {
            settlementId = settlement.Id,
            entitlementId = settlement.EntitlementId,
            orderId = settlement.OrderId,
            productId = settlement.ProductId,
            buyerId = settlement.BuyerId,
            occurredAt = Now
        });
        var row = new MarketplaceOutboxRow
        {
            Id = Guid.NewGuid(), TenantId = TenantId, SettlementId = settlement.Id,
            MessageType = messageType, Payload = payload, PayloadHash = Hash(payload),
            OccurredAt = Now, AttemptCount = 0
        };
        context.Set<MarketplaceOutboxRow>().Add(row);
        return row;
    }

    private static async Task RoundTripPayloadHashAsync(OutboxDbContext context, Guid id)
    {
        context.ChangeTracker.Clear();
        var row = await context.Set<MarketplaceOutboxRow>().SingleAsync(item => item.Id == id);
        row.PayloadHash = Hash(row.Payload);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static string Hash(string payload) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private static OutboxDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<OutboxDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class SqliteOutboxDbContext : DbContext, IApplicationDbContext
    {
        private SqliteOutboxDbContext() : base(new DbContextOptionsBuilder<SqliteOutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options) { }

        public static async Task<SqliteOutboxDbContext> CreateAsync()
        {
            var context = new SqliteOutboxDbContext();
            await context.Database.EnsureCreatedAsync();
            return context;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubHandler : IMarketplaceOutboxHandler
    {
        public List<MarketplaceOutboxDispatchMessage> Messages { get; } = [];

        public ValueTask HandleAsync(
            MarketplaceOutboxDispatchMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : IMarketplaceOutboxHandler
    {
        public ValueTask HandleAsync(MarketplaceOutboxDispatchMessage message,
            CancellationToken cancellationToken) => ValueTask.FromException(exception);
    }

    private sealed class CancelingHandler(CancellationTokenSource source) : IMarketplaceOutboxHandler
    {
        public ValueTask HandleAsync(MarketplaceOutboxDispatchMessage message,
            CancellationToken cancellationToken)
        {
            source.Cancel();
            return ValueTask.FromException(new OperationCanceledException(cancellationToken));
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
