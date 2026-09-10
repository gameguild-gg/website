using System.Text.Json;
using FluentAssertions;
using GameGuild.Commerce.Orders;
using GameGuild.Commerce.Products;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed class MarketplaceOutboxHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("pending")]
    [InlineData("processing")]
    [InlineData("completed")]
    public async Task Grant_ProjectsEntitlementAndCompletesCommerceOrder(string initialState)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (initialState == "processing") fixture.Order.StartPaymentProcessing();
        if (initialState == "completed") fixture.Order.MarkAsPaid();

        await fixture.Handler.HandleAsync(fixture.Message(CommerceMarketplaceOutboxHandler.GrantMessageType),
            CancellationToken.None);

        fixture.Entitlements.GrantCalls.Should().Be(1);
        fixture.Entitlements.GrantedUserId.Should().Be(fixture.Settlement.BuyerId);
        fixture.Entitlements.GrantedProductId.Should().Be(fixture.Settlement.ProductId);
        fixture.Entitlements.GrantedOrderId.Should().Be(fixture.Settlement.OrderId);
        fixture.Order.IsSuccessfullyCompleted.Should().BeTrue();
        fixture.Orders.UpdateCalls.Should().Be(1);
        fixture.Orders.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Revoke_RevokesEntitlementRefundsOrderAndIsIdempotentForRefundedOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Order.MarkAsPaid();
        fixture.Order.MarkAsFulfilled();
        var message = fixture.Message(CommerceMarketplaceOutboxHandler.RevokeMessageType);

        await fixture.Handler.HandleAsync(message, CancellationToken.None);
        await fixture.Handler.HandleAsync(message, CancellationToken.None);

        fixture.Entitlements.RevokeCalls.Should().Be(2);
        fixture.Order.Status.Should().Be(OrderStatus.Refunded);
        fixture.Order.RefundAmount.Should().Be(fixture.Order.Total);
        fixture.Orders.UpdateCalls.Should().Be(2);
        fixture.Orders.SaveCalls.Should().Be(2);
    }

    [Theory]
    [InlineData("entitlement-error")]
    [InlineData("entitlement-default")]
    [InlineData("revoke")]
    [InlineData("order-state")]
    public async Task Handler_FailsClosedWhenCommerceRejectsProjection(string failure)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (failure == "entitlement-error") fixture.Entitlements.GrantResult = EntitlementResult.Failed("denied");
        if (failure == "entitlement-default") fixture.Entitlements.GrantResult = new EntitlementResult();
        if (failure == "revoke") fixture.Entitlements.RevokeResult = false;
        if (failure == "order-state") fixture.Order.PlaceOnHold("review");
        var type = failure == "revoke"
            ? CommerceMarketplaceOutboxHandler.RevokeMessageType
            : CommerceMarketplaceOutboxHandler.GrantMessageType;

        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(fixture.Message(type),
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<MarketplaceOutboxException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("tenant-null")]
    [InlineData("buyer")]
    [InlineData("line-count")]
    [InlineData("line-id")]
    [InlineData("product")]
    public async Task Handler_RejectsCommerceOrderThatNoLongerMatchesSnapshot(string mismatch)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (mismatch == "tenant") fixture.Settlement.TenantId = Guid.NewGuid();
        if (mismatch == "tenant-null") fixture.Order.TenantId = null;
        if (mismatch == "buyer") fixture.Settlement.BuyerId = Guid.NewGuid();
        if (mismatch == "line-count") fixture.Orders.Order = Order.Create(
            fixture.Settlement.BuyerId, "empty-order", fixture.Settlement.TenantId);
        if (mismatch == "line-id") fixture.Settlement.OrderLineItemId = Guid.NewGuid();
        if (mismatch == "product") fixture.Settlement.ProductId = Guid.NewGuid();
        await fixture.Context.SaveChangesAsync();

        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(
                fixture.Message(CommerceMarketplaceOutboxHandler.GrantMessageType), CancellationToken.None).AsTask())
            .Should().ThrowAsync<MarketplaceOutboxException>();
    }

    [Fact]
    public async Task Handler_RejectsMissingCommerceOrderAndSettlement()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Orders.Order = null;
        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(
                fixture.Message(CommerceMarketplaceOutboxHandler.GrantMessageType), CancellationToken.None).AsTask())
            .Should().ThrowAsync<MarketplaceOutboxException>();

        fixture.Orders.Order = fixture.Order;
        var missing = fixture.Message(CommerceMarketplaceOutboxHandler.GrantMessageType) with
        {
            SettlementId = Guid.NewGuid()
        };
        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(missing, CancellationToken.None).AsTask())
            .Should().ThrowAsync<MarketplaceOutboxException>();
    }

    [Theory]
    [InlineData("entitlement-missing")]
    [InlineData("entitlement-format")]
    [InlineData("entitlement-value")]
    [InlineData("order-missing")]
    [InlineData("order-format")]
    [InlineData("order-value")]
    [InlineData("product-missing")]
    [InlineData("product-value")]
    [InlineData("buyer-missing")]
    [InlineData("buyer-value")]
    public async Task Handler_RejectsPayloadThatIsNotBoundToSettlement(string invalid)
    {
        await using var fixture = await Fixture.CreateAsync();
        var values = new Dictionary<string, object?>
        {
            ["settlementId"] = fixture.Settlement.Id,
            ["entitlementId"] = fixture.Settlement.EntitlementId,
            ["orderId"] = fixture.Settlement.OrderId,
            ["productId"] = fixture.Settlement.ProductId,
            ["buyerId"] = fixture.Settlement.BuyerId
        };
        var property = invalid.Split('-')[0] + "Id";
        if (invalid.EndsWith("missing", StringComparison.Ordinal)) values.Remove(property);
        else if (invalid.EndsWith("format", StringComparison.Ordinal)) values[property] = "not-a-guid";
        else values[property] = Guid.NewGuid();
        var message = fixture.Message(CommerceMarketplaceOutboxHandler.GrantMessageType) with
        {
            Payload = JsonSerializer.SerializeToElement(values)
        };

        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(message, CancellationToken.None).AsTask())
            .Should().ThrowAsync<MarketplaceOutboxException>();
    }

    [Fact]
    public async Task Handler_RejectsUnsupportedMessageAndNullMessage()
    {
        await using var fixture = await Fixture.CreateAsync();

        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(
                fixture.Message("unsupported"), CancellationToken.None).AsTask())
            .Should().ThrowAsync<MarketplaceOutboxException>();
        await FluentActions.Awaiting(() => fixture.Handler.HandleAsync(null!, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Handler_RejectsInvalidDependencies()
    {
        var context = new StubApplicationDbContext();
        var entitlements = new EntitlementService();
        var orders = new OrderRepository();

        FluentActions.Invoking(() => new CommerceMarketplaceOutboxHandler(null!, entitlements, orders))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new CommerceMarketplaceOutboxHandler(context, null!, orders))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new CommerceMarketplaceOutboxHandler(context, entitlements, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new CommerceMarketplaceOutboxHandler(context, entitlements, orders))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OutboxContracts_ExposeAllDispatchAndResultBindings()
    {
        var payload = JsonSerializer.SerializeToElement(new { value = 1 });
        var dispatch = new MarketplaceOutboxDispatchMessage(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "type", payload, "hash", Now);
        var result = new MarketplaceOutboxProcessResult(
            MarketplaceOutboxProcessStatus.Failed, dispatch.Id, "error");

        dispatch.Id.Should().NotBeEmpty();
        dispatch.TenantId.Should().NotBeEmpty();
        dispatch.SettlementId.Should().NotBeEmpty();
        dispatch.MessageType.Should().Be("type");
        dispatch.Payload.GetProperty("value").GetInt32().Should().Be(1);
        dispatch.PayloadHash.Should().Be("hash");
        dispatch.OccurredAt.Should().Be(Now);
        result.Status.Should().Be(MarketplaceOutboxProcessStatus.Failed);
        result.MessageId.Should().Be(dispatch.Id);
        result.Error.Should().Be("error");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(HandlerContext context)
        {
            Context = context;
            Order = Order.Create(Guid.NewGuid(), Guid.NewGuid().ToString("N"), Guid.NewGuid());
            var line = Order.AddLineItem(Guid.NewGuid(), "Product",
                new OrderLineItemPricingSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, 10m, null, 10m, "USD"));
            Settlement = SeedSettlement(context, Order, line);
            Entitlements = new EntitlementService();
            Orders = new OrderRepository { Order = Order };
            Handler = new CommerceMarketplaceOutboxHandler(context, Entitlements, Orders);
        }

        public HandlerContext Context { get; }
        public Order Order { get; }
        public MarketplaceSettlementRow Settlement { get; }
        public EntitlementService Entitlements { get; }
        public OrderRepository Orders { get; }
        public CommerceMarketplaceOutboxHandler Handler { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var context = new HandlerContext();
            await context.Database.OpenConnectionAsync();
            await context.Database.EnsureCreatedAsync();
            var fixture = new Fixture(context);
            await context.SaveChangesAsync();
            return fixture;
        }

        public MarketplaceOutboxDispatchMessage Message(string messageType)
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                settlementId = Settlement.Id,
                entitlementId = Settlement.EntitlementId,
                orderId = Settlement.OrderId,
                productId = Settlement.ProductId,
                buyerId = Settlement.BuyerId
            });
            return new MarketplaceOutboxDispatchMessage(
                Guid.NewGuid(), Settlement.TenantId, Settlement.Id, messageType, payload, "hash", Now);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private static MarketplaceSettlementRow SeedSettlement(
        HandlerContext context,
        Order order,
        OrderLineItem line)
    {
        var row = new MarketplaceSettlementRow
        {
            Id = Guid.NewGuid(), TenantId = order.TenantId!.Value, OrderId = order.Id,
            OrderLineItemId = line.Id, ProductId = line.ProductId,
            ProductPricingVersionId = line.ProductPricingVersionId, PriceVersionSnapshot = 1,
            Quantity = 1, UnitPriceSnapshot = 10, FiatCurrencySnapshot = "USD",
            OrderSnapshotHash = "snapshot", BuyerId = order.UserId, BuyerWalletId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(), SellerWalletId = Guid.NewGuid(), PlatformFeeWalletId = Guid.NewGuid(),
            PolicyVersion = 1, CurrencyMode = ProductCurrencyMode.HardOnly,
            Status = MarketplaceSettlementStatus.Settled, IdempotencyKey = Guid.NewGuid().ToString("N"),
            EntitlementId = Guid.NewGuid(), EntitlementStatus = MarketplaceEntitlementStatus.PendingGrant,
            PostingId = Guid.NewGuid(), JournalSequence = 1, JournalHash = "journal",
            CapabilityReceiptId = Guid.NewGuid(), CapabilityReceiptHash = "receipt", ReserveVersion = 1,
            RiskDecisionId = Guid.NewGuid(), KillSwitchEpoch = 1, JurisdictionCode = "BR",
            EvidenceHashes = "[]", RefundHoldUntil = Now.AddDays(1), SettledAt = Now,
            UpdatedAt = Now, Version = 1
        };
        context.Add(row);
        return row;
    }

    private sealed class HandlerContext : DbContext, IApplicationDbContext
    {
        public HandlerContext() : base(new DbContextOptionsBuilder<HandlerContext>()
            .UseSqlite("Data Source=:memory:").Options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new MarketplaceModelConfiguration().Configure(modelBuilder);

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class EntitlementService : IEntitlementService
    {
        public EntitlementResult GrantResult { get; set; } = new() { Success = true };
        public bool RevokeResult { get; set; } = true;
        public int GrantCalls { get; private set; }
        public int RevokeCalls { get; private set; }
        public Guid GrantedUserId { get; private set; }
        public Guid GrantedProductId { get; private set; }
        public Guid? GrantedOrderId { get; private set; }

        public Task<EntitlementResult> GrantEntitlementAsync(Guid userId, Guid productId,
            ProductAcquisitionType acquisitionType, decimal pricePaid = 0, string currency = "USD",
            DateTime? expiresAt = null, Guid? orderId = null, CancellationToken cancellationToken = default)
        {
            GrantCalls++;
            GrantedUserId = userId;
            GrantedProductId = productId;
            GrantedOrderId = orderId;
            return Task.FromResult(GrantResult);
        }

        public Task<bool> RevokeEntitlementAsync(Guid userId, Guid productId, string? reason = null,
            CancellationToken cancellationToken = default)
        {
            RevokeCalls++;
            return Task.FromResult(RevokeResult);
        }

        public Task<bool> HasAccessAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDictionary<Guid, bool>> HasAccessAsync(Guid userId, IEnumerable<Guid> productIds,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<EntitlementInfo>> GetUserEntitlementsAsync(Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ValidateSubscriptionAsync(Guid userId, Guid productId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<EntitlementInfo>> GetExpiringEntitlementsAsync(int daysUntilExpiration = 7,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<EntitlementInfo>> GetAllActiveEntitlementsAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> ProcessExpiredSubscriptionsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class OrderRepository : IOrderRepository
    {
        public Order? Order { get; set; }
        public int UpdateCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public Task<Order?> GetWithLineItemsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Order);
        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<Order>> GetByUserIdAsync(Guid userId, OrderStatus? status = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<Order>> GetByTenantIdAsync(Guid tenantId, OrderStatus? status = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<Order>> GetByDateRangeAsync(DateTime startDate, DateTime endDate,
            OrderStatus? status = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(Order order, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
