using FluentAssertions;
using GameGuild.Commerce.Products;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Orders.UnitTests;

/// <summary>
///     Issue #401 coverage on the Orders side: inventory hooks in the order pipeline
///     (reserve on add, release on cancel, restore on refund) and the rule-based
///     recommendations query (co-occurrence, bundle savings, complete-the-bundle, cache).
/// </summary>
public sealed class ProductCatalogIntegrationTests
{
    // ── Add-to-order inventory hooks ──────────────────────────────────────

    [Fact]
    public async Task AddProduct_FailsClosed_WhenTrackedStockIsInsufficient()
    {
        var fixture = CreatePricingFixture();

        var inventory = new Mock<IProductInventoryService>();
        inventory
            .Setup(service => service.TryReserveAsync(fixture.Product.Id, fixture.Command.Quantity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await fixture.WithInventory(inventory.Object).Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.OutOfStock");
        fixture.Order.LineItems.Should().BeEmpty();
        fixture.PricingLedgerWriter.Verify(writer => writer.RecordAsync(
            It.IsAny<GameGuild.Commerce.PricingDecisionRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProduct_ReservesStock_OnSuccess()
    {
        var fixture = CreatePricingFixture();

        var inventory = new Mock<IProductInventoryService>();
        inventory
            .Setup(service => service.TryReserveAsync(fixture.Product.Id, fixture.Command.Quantity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await fixture.WithInventory(inventory.Object).Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        inventory.Verify(service => service.TryReserveAsync(
            fixture.Product.Id, fixture.Command.Quantity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddProduct_WithoutInventoryService_KeepsDigitalGoodsBehavior()
    {
        var fixture = CreatePricingFixture();

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Order.LineItems.Should().HaveCount(1);
    }

    // ── Cancel / refund hooks ─────────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_ReleasesInventoryReservations()
    {
        var (order, lineItem) = CreatePendingOrderWithLine("cancel");
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var inventory = new Mock<IProductInventoryService>();
        var handler = new CancelOrderCommandHandler(repository.Object, inventory.Object);

        var result = await handler.Handle(new CancelOrderCommand(order.Id, "changed mind"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        inventory.Verify(service => service.ReleaseAsync(
            It.Is<List<(Guid ProductId, int Quantity)>>(lines =>
                lines.Count == 1 && lines[0].ProductId == lineItem.ProductId && lines[0].Quantity == lineItem.Quantity),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundOrder_RestoresInventory_OnFullRefund()
    {
        var (order, lineItem) = CreateFulfilledOrderWithLine();

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var entitlements = new Mock<IEntitlementService>();
        entitlements
            .Setup(service => service.RevokeEntitlementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var inventory = new Mock<IProductInventoryService>();

        var handler = new RefundOrderCommandHandler(repository.Object, entitlements.Object, inventory.Object);
        var result = await handler.Handle(new RefundOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        inventory.Verify(service => service.RestoreAsync(
            It.Is<List<(Guid ProductId, int Quantity)>>(lines =>
                lines.Count == 1 && lines[0].ProductId == lineItem.ProductId && lines[0].Quantity == lineItem.Quantity),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Recommendations (rule-based v1) ───────────────────────────────────

    [Fact]
    public async Task Recommendations_CombineCoOccurrence_BundleSavings_AndCache()
    {
        await using var db = new RecommendationsTestDbContext(
            new DbContextOptionsBuilder<RecommendationsTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var tenantId = Guid.NewGuid();
        var anchor = Product.Create("Anchor", isBundle: true, tenantId: tenantId);
        var companion = Product.Create("Companion", tenantId: tenantId);
        var rare = Product.Create("Rare", tenantId: tenantId);
        // Draft only ever appears in the legacy-paid order — that is its distinguishing trait;
        // it must stay published so the tie with Rare is visible in the ranking.
        var draft = Product.Create("Draft", tenantId: tenantId);

        // Bundle containing the anchor: parts 60 + 40, bundle price 80.
        var bundle = Product.Create("Mega bundle", isBundle: true, tenantId: tenantId);
        bundle.Pricing.Add(ProductPricing.CreateWithVersion(bundle.Id, "Bundle price", 80m, isDefault: true, tenantId: tenantId).Pricing);
        bundle.AddToBundleTypeSafe(anchor.Id);
        bundle.AddToBundleTypeSafe(companion.Id);
        companion.Pricing.Add(ProductPricing.CreateWithVersion(companion.Id, "Standard", 40m, isDefault: true, tenantId: tenantId).Pricing);
        anchor.Pricing.Add(ProductPricing.CreateWithVersion(anchor.Id, "Standard", 60m, isDefault: true, tenantId: tenantId).Pricing);

        // The anchor is itself a bundle with one optional component.
        var optional = Product.Create("Optional extra", tenantId: tenantId);
        var optionalItem = anchor.AddToBundleTypeSafe(optional.Id);
        optionalItem.SetRequired(false);

        // Completed orders: anchor+companion twice, anchor+rare once, anchor+draft once.
        var fulfilled = Order.Create(Guid.NewGuid(), $"fulfilled-{Guid.NewGuid():N}", tenantId);
        fulfilled.AddLineItem(anchor, 1);
        fulfilled.AddLineItem(companion, 1);
        fulfilled.AddLineItem(anchor, 1);
        fulfilled.AddLineItem(companion, 1);
        fulfilled.MarkAsPaidPendingFulfillment(Guid.NewGuid());
        fulfilled.MarkAsFulfilled();

        var completed = Order.Create(Guid.NewGuid(), $"completed-{Guid.NewGuid():N}", tenantId);
        completed.AddLineItem(anchor, 1);
        completed.AddLineItem(rare, 1);
        completed.AddLineItem(anchor, 1);
        completed.AddLineItem(draft, 1);
        completed.MarkAsPaid(); // legacy Completed status also feeds co-occurrence

        // Pending orders must not feed recommendations.
        var pending = Order.Create(Guid.NewGuid(), $"pending-{Guid.NewGuid():N}", tenantId);
        pending.AddLineItem(anchor, 1);
        pending.AddLineItem(rare, 1);

        db.Set<Product>().AddRange(anchor, companion, rare, draft, bundle, optional);
        db.Set<Order>().AddRange(fulfilled, completed, pending);
        db.Set<ProductBundleItem>().AddRange(bundle.BundleItems.Concat(anchor.BundleItems).ToList());
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var handler = new GetProductRecommendationsQueryHandler(db, new BundleSavingsCalculator(), cache);

        var first = await handler.Handle(new GetProductRecommendationsQuery(anchor.Id, 5), CancellationToken.None);

        first.Should().NotBeNull();
        first!.Cached.Should().BeFalse();
        first.FrequentlyBoughtTogether.Should().HaveCount(3);
        first.FrequentlyBoughtTogether[0].Name.Should().Be("Companion");
        first.FrequentlyBoughtTogether[0].CoOrderCount.Should().Be(2);
        first.FrequentlyBoughtTogether.Select(item => item.Name).Should().NotContain("Pending-only");
        // Rare and Draft tie at one co-order each; both must appear behind Companion.
        first.FrequentlyBoughtTogether.Skip(1).Select(item => item.Name).Should().BeEquivalentTo("Rare", "Draft");

        var suggestion = first.BundleSuggestions.Single(item => item.BundleId == bundle.Id);
        suggestion.Savings.Should().NotBeNull();
        suggestion.Savings!.SumOfParts.Should().Be(100m);
        suggestion.Savings.BundlePrice.Should().Be(80m);
        suggestion.Savings.Savings.Should().Be(20m);

        first.CompleteTheBundle.Should().ContainSingle().Which.ProductId.Should().Be(optional.Id);

        var second = await handler.Handle(new GetProductRecommendationsQuery(anchor.Id, 5), CancellationToken.None);
        second!.Cached.Should().BeTrue();
        second.FrequentlyBoughtTogether.Should().BeEquivalentTo(first.FrequentlyBoughtTogether);
    }

    [Fact]
    public async Task Recommendations_ReturnNull_ForUnknownProduct()
    {
        await using var db = new RecommendationsTestDbContext(
            new DbContextOptionsBuilder<RecommendationsTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var handler = new GetProductRecommendationsQueryHandler(
            db, new BundleSavingsCalculator(), new MemoryCache(new MemoryCacheOptions()));

        var result = await handler.Handle(new GetProductRecommendationsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Should().BeNull();
    }

    // ── Fixtures ──────────────────────────────────────────────────────────

    private static PricingFixture CreatePricingFixture(int quantity = 2)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var order = Order.Create(userId, $"inventory-{Guid.NewGuid():N}", tenantId);
        var product = Product.Create("Inventory product", tenantId: tenantId);
        var (pricing, version) = ProductPricing.CreateWithVersion(
            product.Id, "Explicit tier", 100m, tenantId: tenantId);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(mock => mock.GetByIdAsync(
                product.Id,
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>()))
            .ReturnsAsync(product);

        var pricingRepository = new Mock<IProductPricingRepository>();
        pricingRepository.Setup(mock => mock.GetByIdAsync(pricing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pricing);

        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.Set<ProductPricingVersion>())
            .Returns(new TestAsyncDbSet<ProductPricingVersion>([version]));

        var ledgerWriter = new Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter>();
        ledgerWriter
            .Setup(mock => mock.RecordAsync(It.IsAny<GameGuild.Commerce.PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameGuild.Commerce.PricingDecisionRecordingStatus.Recorded, new GameGuild.Commerce.PricingDecisionLedgerEntry()));

        var actor = CreateActor(userId, tenantId);
        var command = new AddProductToOrderCommand(order.Id, product.Id, pricing.Id, version.Id, quantity);
        return new PricingFixture(
            order, product, command,
            orderRepository, productRepository, pricingRepository, dbContext, ledgerWriter, actor);
    }

    private static (Order Order, OrderLineItem LineItem) CreatePendingOrderWithLine(string keyPrefix)
    {
        var order = Order.Create(Guid.NewGuid(), $"{keyPrefix}-{Guid.NewGuid():N}", Guid.NewGuid());
        var lineItem = AddSimpleLine(order, Guid.NewGuid(), "Any product");
        return (order, lineItem);
    }

    private static (Order Order, OrderLineItem LineItem) CreateFulfilledOrderWithLine()
    {
        var order = Order.Create(Guid.NewGuid(), $"refund-{Guid.NewGuid():N}", Guid.NewGuid());
        var productId = Guid.NewGuid();
        var lineItem = AddSimpleLine(order, productId, "Any product");
        // Refunds are accepted for Completed orders (the legacy paid status); the fulfilled
        // status is a post-entitlement state the refund path intentionally rejects.
        order.MarkAsPaid();
        return (order, lineItem);
    }

    private static OrderLineItem AddSimpleLine(Order order, Guid productId, string productName)
    {
        return order.AddLineItem(
            productId,
            productName,
            new OrderLineItemPricingSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, 50m, null, 50m, "USD"));
    }

    private static IActorContextAccessor CreateActor(Guid userId, Guid tenantId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(mock => mock.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return accessor.Object;
    }

    private sealed class PricingFixture
    {
        private readonly Mock<IOrderRepository> _orderRepository;
        private readonly Mock<IProductRepository> _productRepository;
        private readonly Mock<IProductPricingRepository> _pricingRepository;
        private readonly Mock<IApplicationDbContext> _dbContext;
        private readonly Mock<IPromoCodeService> _promoService = new();
        private readonly Mock<IPromoCodeUsageRecorder> _usageRecorder = new();
        private readonly Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter> _ledgerWriter;
        private readonly IActorContextAccessor _actor;

        public PricingFixture(
            Order order,
            Product product,
            AddProductToOrderCommand command,
            Mock<IOrderRepository> orderRepository,
            Mock<IProductRepository> productRepository,
            Mock<IProductPricingRepository> pricingRepository,
            Mock<IApplicationDbContext> dbContext,
            Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter> ledgerWriter,
            IActorContextAccessor actor)
        {
            Order = order;
            Product = product;
            Command = command;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _pricingRepository = pricingRepository;
            _dbContext = dbContext;
            _ledgerWriter = ledgerWriter;
            _actor = actor;
            Handler = Build(inventory: null);
        }

        public Order Order { get; }

        public Product Product { get; }

        public AddProductToOrderCommand Command { get; }

        public Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter> PricingLedgerWriter => _ledgerWriter;

        public AddProductToOrderCommandHandler Handler { get; }

        public AddProductToOrderCommandHandler WithInventory(IProductInventoryService inventory) => Build(inventory);

        private AddProductToOrderCommandHandler Build(IProductInventoryService? inventory) => new(
            _orderRepository.Object,
            _productRepository.Object,
            _pricingRepository.Object,
            _promoService.Object,
            _usageRecorder.Object,
            _ledgerWriter.Object,
            _dbContext.Object,
            _actor,
            inventory);
    }
}

/// <summary>
///     In-memory context for the recommendations handler: products (with pricing and
///     bundle items), orders and order line items.
/// </summary>
internal sealed class RecommendationsTestDbContext(DbContextOptions<RecommendationsTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Ignore(p => p.Creator);
            entity.Ignore(p => p.IncludedInBundles);
            entity.Ignore(p => p.SubscriptionPlans);
            entity.Ignore(p => p.CommissionConfig);
            entity.Ignore(p => p.Category);
            entity.Ignore(p => p.Inventory);
            entity.HasMany(p => p.Pricing)
                .WithOne(pricing => pricing.Product)
                .HasForeignKey(pricing => pricing.ProductId);
            entity.HasMany(p => p.BundleItems)
                .WithOne(item => item.BundleProduct)
                .HasForeignKey(item => item.BundleProductId);
        });

        modelBuilder.Entity<ProductPricing>(entity =>
        {
            entity.Ignore(p => p.Versions);
            entity.Ignore(p => p.Product);
        });

        modelBuilder.Entity<ProductBundleItem>(entity =>
        {
            entity.Ignore(item => item.BundleProduct);
            entity.Ignore(item => item.IncludedProduct);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasMany(order => order.LineItems)
                .WithOne(line => line.Order)
                .HasForeignKey(line => line.OrderId);
        });

        modelBuilder.Entity<OrderLineItem>(entity =>
        {
            entity.Ignore(line => line.Order);
            entity.Ignore(line => line.Product);
            entity.Ignore(line => line.UserProduct);
        });
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Transactions are not needed for these tests.");
}

internal static class OrderCatalogTestExtensions
{
    /// <summary>Adds a simple one-unit line for the given product (USD 50, version 1).</summary>
    public static OrderLineItem AddLineItem(this Order order, Product product, int quantity = 1)
    {
        return order.AddLineItem(
            product.Id,
            product.Name,
            new OrderLineItemPricingSnapshot(
                Guid.NewGuid(), Guid.NewGuid(), 1, 50m, null, 50m, "USD"),
            quantity);
    }
}
