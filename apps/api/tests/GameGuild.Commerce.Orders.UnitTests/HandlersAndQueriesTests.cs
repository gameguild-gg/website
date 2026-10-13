using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild.Commerce.Products;
using GameGuild.Identity.Context.Actors;
using Moq;
using System.Collections;
using System.Linq.Expressions;
using Xunit;

namespace GameGuild.Commerce.Orders.UnitTests;

public sealed class OrderQueryHandlerTests
{
    [Fact]
    public async Task GetAllOrdersQueryHandler_FiltersAndOrdersNewestFirst()
    {
        var tenantId = Guid.NewGuid();

        var oldestPending = OrderTestFactory.CreatePendingOrder(tenantId, "pending-old");
        oldestPending.CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var completed = OrderTestFactory.CreatePendingOrder(tenantId, "completed");
        completed.MarkAsPaid("ref", "card", "ext-1");
        completed.CreatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        var newestPending = OrderTestFactory.CreatePendingOrder(tenantId, "pending-new");
        newestPending.CreatedAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);

        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.Set<Order>())
            .Returns(new TestAsyncDbSet<Order>(new[] { oldestPending, completed, newestPending }));

        var handler = new GetAllOrdersQueryHandler(dbContext.Object);

        var result = (await handler.Handle(new GetAllOrdersQuery(OrderStatus.Pending), CancellationToken.None)).ToList();

        result.Select(order => order.Id).Should().Equal(newestPending.Id, oldestPending.Id);
    }

    [Fact]
    public async Task GetOrderQueryHandler_ReturnsRepositoryOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new GetOrderQueryHandler(repository.Object);

        var result = await handler.Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        result.Should().BeSameAs(order);
    }

    [Fact]
    public async Task GetUserOrdersQueryHandler_DelegatesToRepositoryWithStatus()
    {
        var userId = Guid.NewGuid();
        var expected = new[] { OrderTestFactory.CreatePendingOrder() };
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByUserIdAsync(userId, OrderStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected.AsEnumerable());

        var handler = new GetUserOrdersQueryHandler(repository.Object);

        var result = await handler.Handle(new GetUserOrdersQuery(userId, OrderStatus.Pending), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task OrderExistsQueryHandler_ReturnsExpectedBoolean()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new OrderExistsQueryHandler(repository.Object);

        var found = await handler.Handle(new OrderExistsQuery(order.Id), CancellationToken.None);
        var missing = await handler.Handle(new OrderExistsQuery(Guid.Empty), CancellationToken.None);

        found.Should().BeTrue();
        missing.Should().BeFalse();
    }

    private static Mock<DbSet<T>> BuildMockDbSet<T>(IEnumerable<T> items) where T : class
    {
        var queryable = new TestAsyncEnumerable<T>(items);
        IQueryable<T> asyncQueryable = queryable;
        var dbSet = new Mock<DbSet<T>>();

        dbSet.As<IAsyncEnumerable<T>>()
            .Setup(mock => mock.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken cancellationToken) => queryable.GetAsyncEnumerator(cancellationToken));

        dbSet.As<IQueryable<T>>().Setup(mock => mock.Provider)
            .Returns(new TestAsyncQueryProvider<T>(asyncQueryable.Provider));
        dbSet.As<IQueryable<T>>().Setup(mock => mock.Expression)
            .Returns(asyncQueryable.Expression);
        dbSet.As<IQueryable<T>>().Setup(mock => mock.ElementType)
            .Returns(asyncQueryable.ElementType);
        dbSet.As<IQueryable<T>>().Setup(mock => mock.GetEnumerator())
            .Returns(() => queryable.AsEnumerable().GetEnumerator());

        return dbSet;
    }

}

public sealed class OrderCommandHandlerTests
{
    [Fact]
    public async Task CreateOrderCommandHandler_ReturnsDuplicateWhenIdempotencyKeyAlreadyExists()
    {
        var existingOrder = OrderTestFactory.CreatePendingOrder();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdempotencyKeyAsync(existingOrder.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        var handler = new CreateOrderCommandHandler(repository.Object, OrderTestFactory.CreateActor(existingOrder));

        var result = await handler.Handle(
            new CreateOrderCommand(existingOrder.IdempotencyKey),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WasDuplicate.Should().BeTrue();
        result.Value.Order.Id.Should().Be(existingOrder.Id);
        repository.Verify(mock => mock.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderCommandHandler_AddsNewOrderWhenIdempotencyKeyIsUnused()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new Mock<IOrderRepository>();
        Order? addedOrder = null;

        repository.Setup(mock => mock.GetByIdempotencyKeyAsync("new-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repository.Setup(mock => mock.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => addedOrder = order)
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateOrderCommandHandler(
            repository.Object,
            OrderTestFactory.CreateActor(userId: userId, tenantId: tenantId));

        var result = await handler.Handle(
            new CreateOrderCommand("new-key", "127.0.0.1", "unit-test"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedOrder.Should().NotBeNull();
        addedOrder!.UserId.Should().Be(userId);
        addedOrder.Currency.Should().Be("USD");
        addedOrder.TenantId.Should().Be(tenantId);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderCommandHandler_RejectsMissingActorContext()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdempotencyKeyAsync("missing-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new CreateOrderCommandHandler(repository.Object, Mock.Of<IActorContextAccessor>());

        var result = await handler.Handle(new CreateOrderCommand("missing-tenant"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.Unauthenticated");
        repository.Verify(mock => mock.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new AddProductToOrderCommandHandler(
            repository.Object,
            Mock.Of<IProductRepository>(),
            Mock.Of<IProductPricingRepository>(),
            Mock.Of<IPromoCodeService>(),
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreateActor());

        var result = await handler.Handle(
            new AddProductToOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_AddsDiscountedSubscriptionLineItem()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var product = Product.Create("Premium plan", ProductType.Subscription, tenantId: order.TenantId);
        product.Id = Guid.NewGuid();
        var (pricing, pricingVersion) = ProductPricing.CreateWithVersion(
            product.Id, "Default", 120m, 90m, isDefault: true, tenantId: order.TenantId);
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "SAVE10",
            Name = "Save 10%",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10m,
            CreatedBy = Guid.NewGuid(),
            TenantId = order.TenantId
        };

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        orderRepository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderRepository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(mock => mock.GetByIdAsync(product.Id, It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>()))
            .ReturnsAsync(product);

        var pricingRepository = new Mock<IProductPricingRepository>();
        pricingRepository.Setup(mock => mock.GetByIdAsync(pricing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pricing);

        var promoCodeService = new Mock<IPromoCodeService>();
        promoCodeService.Setup(mock => mock.GetPromoCodeByCodeAsync("SAVE10"))
            .ReturnsAsync(promo);
        promoCodeService.Setup(mock => mock.ValidatePromoCodeAsync("SAVE10", order.UserId, product.Id))
            .ReturnsAsync(true);

        var handler = new AddProductToOrderCommandHandler(
            orderRepository.Object,
            productRepository.Object,
            pricingRepository.Object,
            promoCodeService.Object,
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            CreatePricingDbContext(pricingVersion).Object,
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new AddProductToOrderCommand(order.Id, product.Id, pricing.Id, pricingVersion.Id, 2, "SAVE10"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.LineItems.Should().ContainSingle();

        var lineItem = order.LineItems.Single();
        lineItem.ProductNameSnapshot.Should().Be("Premium plan");
        lineItem.UnitPriceSnapshot.Should().Be(90m);
        lineItem.BasePriceSnapshot.Should().Be(120m);
        lineItem.DiscountAmount.Should().Be(18m);
        lineItem.IsSubscription.Should().BeTrue();
        lineItem.PromoCodesApplied.Should().Contain("SAVE10");
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_ReturnsInvalidStatusWhenOrderIsNotPending()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaid("provider-ref", "card", "ext-101");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new AddProductToOrderCommandHandler(
            repository.Object,
            Mock.Of<IProductRepository>(),
            Mock.Of<IProductPricingRepository>(),
            Mock.Of<IPromoCodeService>(),
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new AddProductToOrderCommand(order.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_ReturnsNotFoundWhenProductDoesNotExist()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(mock => mock.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>()))
            .ReturnsAsync((Product?)null);

        var handler = new AddProductToOrderCommandHandler(
            orderRepository.Object,
            productRepository.Object,
            Mock.Of<IProductPricingRepository>(),
            Mock.Of<IPromoCodeService>(),
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new AddProductToOrderCommand(order.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.Unavailable");
        orderRepository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_UsesExplicitPricingAndIgnoresInvalidPromo()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var product = Product.Create("Consulting session", ProductType.Program, tenantId: order.TenantId);
        var (firstPricing, firstVersion) = ProductPricing.CreateWithVersion(product.Id, "Standard", 50m, "USD", null, null, null, false, tenantId: order.TenantId);
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "SAVE5",
            Name = "Save 5",
            Type = PromoCodeType.FixedAmountOff,
            DiscountAmount = 5m,
            CreatedBy = Guid.NewGuid(),
            TenantId = order.TenantId
        };

        var orderRepository = CreateRepositoryWithOrder(order, includeLineItems: true);
        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(mock => mock.GetByIdAsync(product.Id, It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>()))
            .ReturnsAsync(product);

        var pricingRepository = new Mock<IProductPricingRepository>();
        pricingRepository.Setup(mock => mock.GetByIdAsync(firstPricing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPricing);

        var promoCodeService = new Mock<IPromoCodeService>();
        promoCodeService.Setup(mock => mock.GetPromoCodeByCodeAsync("SAVE5"))
            .ReturnsAsync(promo);
        promoCodeService.Setup(mock => mock.ValidatePromoCodeAsync("SAVE5", order.UserId, product.Id))
            .ReturnsAsync(false);

        var handler = new AddProductToOrderCommandHandler(
            orderRepository.Object,
            productRepository.Object,
            pricingRepository.Object,
            promoCodeService.Object,
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            CreatePricingDbContext(firstVersion).Object,
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new AddProductToOrderCommand(order.Id, product.Id, firstPricing.Id, firstVersion.Id, 2, "SAVE5"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var lineItem = order.LineItems.Should().ContainSingle().Subject;
        lineItem.UnitPriceSnapshot.Should().Be(50m);
        lineItem.BasePriceSnapshot.Should().Be(50m);
        lineItem.ProductPricingId.Should().Be(firstPricing.Id);
        lineItem.PricingTierNameSnapshot.Should().Be("Standard");
        lineItem.DiscountAmount.Should().Be(0m);
        lineItem.PromoCodesApplied.Should().BeNull();
        lineItem.IsSubscription.Should().BeFalse();
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_AppliesFixedAmountPromo()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var product = Product.Create("Coaching", ProductType.Program, tenantId: order.TenantId);
        var (pricing, pricingVersion) = ProductPricing.CreateWithVersion(product.Id, "Base", 30m, "USD", null, null, null, true, tenantId: order.TenantId);
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "LESS5",
            Name = "Less 5",
            Type = PromoCodeType.FixedAmountOff,
            DiscountAmount = 5m,
            CreatedBy = Guid.NewGuid(),
            TenantId = order.TenantId
        };

        var orderRepository = CreateRepositoryWithOrder(order, includeLineItems: true);
        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(mock => mock.GetByIdAsync(product.Id, It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>()))
            .ReturnsAsync(product);

        var pricingRepository = new Mock<IProductPricingRepository>();
        pricingRepository.Setup(mock => mock.GetByIdAsync(pricing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pricing);

        var promoCodeService = new Mock<IPromoCodeService>();
        promoCodeService.Setup(mock => mock.GetPromoCodeByCodeAsync("LESS5"))
            .ReturnsAsync(promo);
        promoCodeService.Setup(mock => mock.ValidatePromoCodeAsync("LESS5", order.UserId, product.Id))
            .ReturnsAsync(true);

        var handler = new AddProductToOrderCommandHandler(
            orderRepository.Object,
            productRepository.Object,
            pricingRepository.Object,
            promoCodeService.Object,
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            CreatePricingDbContext(pricingVersion).Object,
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new AddProductToOrderCommand(order.Id, product.Id, pricing.Id, pricingVersion.Id, 3, "LESS5"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var lineItem = order.LineItems.Should().ContainSingle().Subject;
        lineItem.UnitPriceSnapshot.Should().Be(30m);
        // A fixed-amount code discounts once per application (engine semantics), not per unit.
        lineItem.DiscountAmount.Should().Be(5m);
        lineItem.LineTotal.Should().Be(85m);
        lineItem.PromoCodesApplied.Should().Contain("LESS5");
    }

    [Fact]
    public async Task AddProductToOrderCommandHandler_RejectsMissingExplicitPricing()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var product = Product.Create("Free guide", ProductType.Program, tenantId: order.TenantId);

        var orderRepository = CreateRepositoryWithOrder(order, includeLineItems: true);
        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(mock => mock.GetByIdAsync(product.Id, It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>()))
            .ReturnsAsync(product);

        var pricingRepository = new Mock<IProductPricingRepository>();
        pricingRepository.Setup(mock => mock.GetByProductIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProductPricing>());

        var handler = new AddProductToOrderCommandHandler(
            orderRepository.Object,
            productRepository.Object,
            pricingRepository.Object,
            Mock.Of<IPromoCodeService>(),
            Mock.Of<IPromoCodeUsageRecorder>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new AddProductToOrderCommand(order.Id, product.Id, Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PricingNotFound");
        order.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelOrderCommandHandler_CancelsPendingOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        // The cancel handler loads the order with its line items so pending reservations can
        // be released (issue #401) — the fixture must mock that load path.
        var repository = CreateRepositoryWithOrder(order, includeLineItems: true);
        var handler = new CancelOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new CancelOrderCommand(order.Id, "customer request"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new CancelOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new CancelOrderCommand(Guid.NewGuid(), "reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task CancelOrderCommandHandler_ReturnsInvalidStatusWhenOrderIsNotPending()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaid("provider-ref", "card", "ext-102");
        // Same line-items load path as the happy-path cancel (issue #401).
        var repository = CreateRepositoryWithOrder(order, includeLineItems: true);
        var handler = new CancelOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new CancelOrderCommand(order.Id, "reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CaptureOrderCommandHandler_RejectsMissingPayableSnapshot()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            Mock.Of<IOrderPaymentProcessor>(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidPayableOrder");
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task CaptureOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            Mock.Of<IOrderPaymentProcessor>(),
            OrderTestFactory.CreateActor());

        var result = await handler.Handle(new CaptureOrderCommand(Guid.NewGuid(), "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task CaptureOrderCommandHandler_RejectsOrderThatIsNotPending()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.Cancel("customer request");
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            Mock.Of<IOrderPaymentProcessor>(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_RejectsLegacyCompletedOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaid("provider-ref", "card", "ext-123");
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentAuthorityRequired");
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor());

        var result = await handler.Handle(new CompleteOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_ReturnsDuplicateForAlreadyFulfilledOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaidPendingFulfillment(Guid.NewGuid(), "ext-fulfilled");
        order.MarkAsFulfilled();

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WasDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_RequiresAuthoritativePaymentState()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.Cancel("cancelled");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentAuthorityRequired");
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_DelegatesEconomySettlementAndLeavesFulfillmentToOutbox()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Marketplace product", 40m);
        var settlementId = Guid.NewGuid();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var settlements = new Mock<IOrderMarketplaceSettlementAuthority>();
        settlements.Setup(mock => mock.SettleAsync(
                It.Is<OrderMarketplaceSettlementRequest>(request =>
                    request.OrderId == order.Id &&
                    request.CurrencyChoice == OrderMarketplaceCurrencyChoice.Hard &&
                    request.IdempotencyKey == "complete-order"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderMarketplaceSettlementDecision.Accepted(settlementId, isDuplicate: false));

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order),
            settlements.Object);

        var result = await handler.Handle(
            new CompleteOrderCommand(
                order.Id,
                MarketplaceSettlement: new CompleteOrderMarketplaceSettlement(
                    OrderMarketplaceCurrencyChoice.Hard,
                    "complete-order")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WasDuplicate.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Pending);
        repository.Verify(mock => mock.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_FailsClosedWhenEconomySettlementIsUnavailable()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Marketplace product", 40m);
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var settlements = new Mock<IOrderMarketplaceSettlementAuthority>();
        settlements.Setup(mock => mock.SettleAsync(
                It.IsAny<OrderMarketplaceSettlementRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderMarketplaceSettlementDecision.Rejected(
                "Orders.EconomyMarketplaceDisabled",
                "Economy Marketplace is disabled."));
        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order),
            settlements.Object);

        var result = await handler.Handle(
            new CompleteOrderCommand(
                order.Id,
                MarketplaceSettlement: new CompleteOrderMarketplaceSettlement(
                    OrderMarketplaceCurrencyChoice.Soft,
                    "complete-order")),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.EconomyMarketplaceDisabled");
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_GrantsEntitlementsAndCommitsWhenOrderIsPaid()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var purchaseLine = OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Starter course", 40m);
        var addOnLine = OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Course add-on", 25m);
        order.MarkAsPaidPendingFulfillment(Guid.NewGuid(), "ext-234");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(mock => mock.GrantEntitlementAsync(
                order.UserId,
                It.IsAny<Guid>(),
                It.IsAny<ProductAcquisitionType>(),
                It.IsAny<decimal>(),
                order.Currency,
                null,
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid productId, ProductAcquisitionType acquisitionType, decimal pricePaid, string _, DateTime? _, Guid? _, CancellationToken _) =>
                EntitlementResult.Succeeded(new UserProduct
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    UserId = order.UserId,
                    AcquisitionType = acquisitionType,
                    PricePaid = pricePaid,
                    TenantId = order.TenantId
                }));

        var transaction = new RecordingDbContextTransaction();
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            entitlementService.Object,
            dbContext.Object,
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Fulfilled);
        order.FulfilledAt.Should().NotBeNull();
        purchaseLine.UserProductId.Should().NotBeNull();
        addOnLine.UserProductId.Should().NotBeNull();
        transaction.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_RollsBackAndRethrowsWhenEntitlementGrantFails()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Starter course", 40m);
        order.MarkAsPaidPendingFulfillment(Guid.NewGuid(), "ext-999");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(mock => mock.GrantEntitlementAsync(
                order.UserId,
                It.IsAny<Guid>(),
                It.IsAny<ProductAcquisitionType>(),
                It.IsAny<decimal>(),
                order.Currency,
                null,
                order.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("grant failed"));

        var transaction = new RecordingDbContextTransaction();
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            entitlementService.Object,
            dbContext.Object,
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var act = () => handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("grant failed");
        transaction.RollbackCalled.Should().BeTrue();
        repository.Verify(mock => mock.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_PreservesAuthoritativePaymentBindingWhenServiceReturnsNoUserProduct()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var lineItem = OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Course", 30m);
        var paymentId = Guid.NewGuid();
        order.MarkAsPaidPendingFulfillment(paymentId, "provider-ref");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(mock => mock.GrantEntitlementAsync(
                order.UserId,
                lineItem.ProductId,
                ProductAcquisitionType.Purchase,
                lineItem.LineTotal,
                order.Currency,
                null,
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitlementResult { Success = true, UserProduct = null });

        var transaction = new RecordingDbContextTransaction();
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            entitlementService.Object,
            dbContext.Object,
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Fulfilled);
        order.PaymentId.Should().Be(paymentId);
        order.ExternalPaymentId.Should().Be("provider-ref");
        lineItem.UserProductId.Should().BeNull();
        transaction.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteOrderCommandHandler_RejectsLegacyProviderReferencePath()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var lineItem = OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Course", 25m);

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(mock => mock.GrantEntitlementAsync(
                order.UserId,
                lineItem.ProductId,
                ProductAcquisitionType.Purchase,
                lineItem.LineTotal,
                order.Currency,
                null,
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntitlementResult.Failed("not granted"));

        var transaction = new RecordingDbContextTransaction();
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            entitlementService.Object,
            dbContext.Object,
            OrderTestFactory.CreatePaymentAuthority(),
            OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(
            new CompleteOrderCommand(order.Id, null, "ext-legacy", "pix"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentAuthorityRequired");
        order.Status.Should().Be(OrderStatus.Pending);
        order.FulfilledAt.Should().BeNull();
        order.PaymentMethod.Should().BeNull();
        order.ExternalPaymentId.Should().BeNull();
        lineItem.UserProductId.Should().BeNull();
        transaction.CommitCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteOrderCommandHandler_SoftDeletesPendingOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = CreateRepositoryWithOrder(order);
        var handler = new DeleteOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new DeleteOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new DeleteOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task DeleteOrderCommandHandler_DeletesCancelledOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.Cancel("cleanup");
        var repository = CreateRepositoryWithOrder(order);
        var handler = new DeleteOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOrderCommandHandler_ReturnsInvalidStatusWhenOrderCannotBeDeleted()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaid("provider-ref", "card", "ext-103");
        var repository = CreateRepositoryWithOrder(order);
        var handler = new DeleteOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task HoldOrderCommandHandler_PlacesOrderOnHold()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = CreateRepositoryWithOrder(order);
        var handler = new HoldOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new HoldOrderCommand(order.Id, "manual review"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.OnHold);
    }

    [Fact]
    public async Task HoldOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new HoldOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new HoldOrderCommand(Guid.NewGuid(), "review"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task HoldOrderCommandHandler_AllowsProcessingOrders()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        SetOrderStatus(order, OrderStatus.Processing);
        var repository = CreateRepositoryWithOrder(order);
        var handler = new HoldOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new HoldOrderCommand(order.Id, "risk check"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.OnHold);
    }

    [Fact]
    public async Task HoldOrderCommandHandler_ReturnsInvalidStatusWhenOrderCannotBeHeld()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaid("provider-ref", "card", "ext-104");
        var repository = CreateRepositoryWithOrder(order);
        var handler = new HoldOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new HoldOrderCommand(order.Id, "review"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task RefundOrderCommandHandler_FullRefundRevokesEntitlements()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Starter course", 40m);
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Add-on", 10m);
        order.MarkAsPaid("provider-ref", "card", "ext-456");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(mock => mock.RevokeEntitlementAsync(order.UserId, It.IsAny<Guid>(), "Order refunded", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RefundOrderCommandHandler(repository.Object, entitlementService.Object);

        var result = await handler.Handle(new RefundOrderCommand(order.Id, order.Total, "customer requested"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Refunded);
        entitlementService.Verify(
            mock => mock.RevokeEntitlementAsync(order.UserId, It.IsAny<Guid>(), "Order refunded", It.IsAny<CancellationToken>()),
            Times.Exactly(order.LineItems.Count));
    }

    [Fact]
    public async Task RefundOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new RefundOrderCommandHandler(repository.Object, Mock.Of<IEntitlementService>());

        var result = await handler.Handle(new RefundOrderCommand(Guid.NewGuid(), 10m, "reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task RefundOrderCommandHandler_ReturnsInvalidStatusWhenOrderCannotBeRefunded()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new RefundOrderCommandHandler(repository.Object, Mock.Of<IEntitlementService>());

        var result = await handler.Handle(new RefundOrderCommand(order.Id, 10m, "reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task RefundOrderCommandHandler_PartialRefundDoesNotRevokeEntitlements()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Starter course", 40m);
        order.MarkAsPaid("provider-ref", "card", "ext-457");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entitlementService = new Mock<IEntitlementService>();
        var handler = new RefundOrderCommandHandler(repository.Object, entitlementService.Object);

        var result = await handler.Handle(new RefundOrderCommand(order.Id, 10m, "partial refund"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.PartiallyRefunded);
        order.RefundAmount.Should().Be(10m);
        entitlementService.Verify(
            mock => mock.RevokeEntitlementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundOrderCommandHandler_DefaultsNullAmountToOrderTotalForPartiallyRefundedOrders()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Starter course", 40m);
        order.MarkAsPaid("provider-ref", "card", "ext-458");
        order.ProcessRefund(10m, "partial");

        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(mock => mock.RevokeEntitlementAsync(order.UserId, It.IsAny<Guid>(), "Order refunded", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RefundOrderCommandHandler(repository.Object, entitlementService.Object);

        var result = await handler.Handle(new RefundOrderCommand(order.Id, null, "final refund"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Refunded);
        order.RefundAmount.Should().Be(order.Total);
        entitlementService.Verify(
            mock => mock.RevokeEntitlementAsync(order.UserId, It.IsAny<Guid>(), "Order refunded", It.IsAny<CancellationToken>()),
            Times.Exactly(order.LineItems.Count));
    }

    [Fact]
    public async Task ReleaseOrderCommandHandler_ReleasesHeldOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.PlaceOnHold("manual review");
        var repository = CreateRepositoryWithOrder(order);
        var handler = new ReleaseOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new ReleaseOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task ReleaseOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new ReleaseOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new ReleaseOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task ReleaseOrderCommandHandler_ReturnsInvalidStatusWhenOrderIsNotOnHold()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = CreateRepositoryWithOrder(order);
        var handler = new ReleaseOrderCommandHandler(repository.Object);

        var result = await handler.Handle(new ReleaseOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task UpdateOrderCommandHandler_RejectsPendingOrderCurrencyMutation()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = CreateRepositoryWithOrder(order);
        var handler = new UpdateOrderCommandHandler(repository.Object, OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, "EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.CurrencyImmutable");
        order.Currency.Should().Be("USD");
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOrderCommandHandler_ReturnsNotFoundWhenOrderDoesNotExist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new UpdateOrderCommandHandler(repository.Object, OrderTestFactory.CreateActor());

        var result = await handler.Handle(new UpdateOrderCommand(Guid.NewGuid(), "EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.NotFound");
    }

    [Fact]
    public async Task UpdateOrderCommandHandler_ReturnsInvalidStatusWhenOrderIsNotPending()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaid("provider-ref", "card", "ext-105");
        var repository = CreateRepositoryWithOrder(order);
        var handler = new UpdateOrderCommandHandler(repository.Object, OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, "EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidStatus");
    }

    [Fact]
    public async Task UpdateOrderCommandHandler_LeavesCurrencyUnchangedWhenNull()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = CreateRepositoryWithOrder(order);
        var handler = new UpdateOrderCommandHandler(repository.Object, OrderTestFactory.CreateActor(order));

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Currency.Should().Be("USD");
    }

    private static Mock<IOrderRepository> CreateRepositoryWithOrder(Order order)
    {
        return CreateRepositoryWithOrder(order, includeLineItems: false);
    }

    private static Mock<IOrderRepository> CreateRepositoryWithOrder(Order order, bool includeLineItems)
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(mock => mock.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        if (includeLineItems)
        {
            repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
        }
        repository.Setup(mock => mock.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static Mock<IApplicationDbContext> CreatePricingDbContext(params ProductPricingVersion[] versions)
    {
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(mock => mock.Set<ProductPricingVersion>())
            .Returns(new TestAsyncDbSet<ProductPricingVersion>(versions));
        return dbContext;
    }

    private static void SetOrderStatus(Order order, OrderStatus status)
    {
        typeof(Order).GetProperty(nameof(Order.Status))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(order, new object[] { status });
    }
}

public sealed class OrderRepositoryTests
{
    [Fact]
    public async Task OrderRepository_LoadsOrdersWithLineItemsAndProducts()
    {
        await using var context = CreateRepositoryContext();
        var repository = new OrderRepository(context);
        var product = Product.Create("Stored product", ProductType.Program, tenantId: Guid.NewGuid());
        var order = CreateOrderWithProduct(product, "lookup-key");

        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var byIdempotencyKey = await repository.GetByIdempotencyKeyAsync("lookup-key");
        var withLineItems = await repository.GetWithLineItemsAsync(order.Id);

        byIdempotencyKey.Should().NotBeNull();
        byIdempotencyKey!.LineItems.Should().ContainSingle();
        withLineItems.Should().NotBeNull();
        withLineItems!.LineItems.Should().ContainSingle();
        var persistedLineItem = withLineItems.LineItems.Single();
        persistedLineItem.Product.Name.Should().Be("Stored product");
        persistedLineItem.ProductPricingId.Should().NotBeEmpty();
        persistedLineItem.ProductPricingVersionId.Should().NotBeEmpty();
        persistedLineItem.PriceVersionSnapshot.Should().Be(1);
        persistedLineItem.UnitPriceSnapshot.Should().Be(19m);
        persistedLineItem.CurrencySnapshot.Should().Be("USD");
    }

    [Fact]
    public async Task OrderRepository_GetByUserId_FiltersByOptionalStatusAndOrdersNewestFirst()
    {
        await using var context = CreateRepositoryContext();
        var repository = new OrderRepository(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var olderPending = Order.Create(userId, "user-older", tenantId);
        olderPending.CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var newestCompleted = Order.Create(userId, "user-newest", tenantId);
        newestCompleted.MarkAsPaid("provider-ref", "card", "ext-201");
        newestCompleted.CreatedAt = new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc);

        var otherUser = Order.Create(Guid.NewGuid(), "user-other", tenantId);
        otherUser.CreatedAt = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);

        context.Orders.AddRange(olderPending, newestCompleted, otherUser);
        await context.SaveChangesAsync();

        var allForUser = (await repository.GetByUserIdAsync(userId)).ToList();
        var onlyPending = (await repository.GetByUserIdAsync(userId, OrderStatus.Pending)).ToList();

        allForUser.Select(order => order.Id).Should().Equal(newestCompleted.Id, olderPending.Id);
        onlyPending.Select(order => order.Id).Should().Equal(olderPending.Id);
    }

    [Fact]
    public async Task OrderRepository_GetByTenantId_FiltersByOptionalStatus()
    {
        await using var context = CreateRepositoryContext();
        var repository = new OrderRepository(context);
        var tenantId = Guid.NewGuid();

        var pending = Order.Create(Guid.NewGuid(), "tenant-pending", tenantId);
        pending.CreatedAt = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);

        var completed = Order.Create(Guid.NewGuid(), "tenant-completed", tenantId);
        completed.MarkAsPaid("provider-ref", "card", "ext-202");
        completed.CreatedAt = new DateTime(2026, 2, 2, 8, 0, 0, DateTimeKind.Utc);

        var otherTenant = Order.Create(Guid.NewGuid(), "tenant-other", Guid.NewGuid());
        otherTenant.CreatedAt = new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc);

        context.Orders.AddRange(pending, completed, otherTenant);
        await context.SaveChangesAsync();

        var allForTenant = (await repository.GetByTenantIdAsync(tenantId)).ToList();
        var onlyCompleted = (await repository.GetByTenantIdAsync(tenantId, OrderStatus.Completed)).ToList();

        allForTenant.Select(order => order.Id).Should().Equal(completed.Id, pending.Id);
        onlyCompleted.Select(order => order.Id).Should().Equal(completed.Id);
    }

    [Fact]
    public async Task OrderRepository_GetByDateRange_FiltersByOptionalStatus()
    {
        await using var context = CreateRepositoryContext();
        var repository = new OrderRepository(context);
        var tenantId = Guid.NewGuid();

        var insidePending = Order.Create(Guid.NewGuid(), "range-pending", tenantId);
        insidePending.CreatedAt = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc);

        var insideCompleted = Order.Create(Guid.NewGuid(), "range-completed", tenantId);
        insideCompleted.MarkAsPaid("provider-ref", "card", "ext-203");
        insideCompleted.CreatedAt = new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc);

        var outsideRange = Order.Create(Guid.NewGuid(), "range-outside", tenantId);
        outsideRange.CreatedAt = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);

        context.Orders.AddRange(insidePending, insideCompleted, outsideRange);
        await context.SaveChangesAsync();

        var allInRange = (await repository.GetByDateRangeAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 5))).ToList();
        var onlyCompleted = (await repository.GetByDateRangeAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 5), OrderStatus.Completed)).ToList();

        allInRange.Select(order => order.Id).Should().Equal(insideCompleted.Id, insidePending.Id);
        onlyCompleted.Select(order => order.Id).Should().Equal(insideCompleted.Id);
    }

    [Fact]
    public async Task OrderRepository_MutationMethodsPersistTrackedState()
    {
        await using var context = CreateRepositoryContext();
        var repository = new OrderRepository(context);
        var order = Order.Create(Guid.NewGuid(), "mutation-order", Guid.NewGuid());

        await repository.AddAsync(order);
        await repository.SaveChangesAsync();

        context.Orders.Should().ContainSingle().Which.Should().Be(order);

        order.Metadata = "{\"updated\":true}";
        await repository.UpdateAsync(order);
        await repository.SaveChangesAsync();

        context.Orders.Single().Metadata.Should().Be("{\"updated\":true}");

        await repository.DeleteAsync(order);
        await repository.SaveChangesAsync();

        order.DeletedAt.Should().NotBeNull();
    }

    private static TestOrdersRepositoryDbContext CreateRepositoryContext()
    {
        var options = new DbContextOptionsBuilder<TestOrdersRepositoryDbContext>()
            .UseInMemoryDatabase($"OrdersRepository_{Guid.NewGuid()}")
            .Options;

        return new TestOrdersRepositoryDbContext(options);
    }

    private static Order CreateOrderWithProduct(Product product, string idempotencyKey)
    {
        var order = Order.Create(Guid.NewGuid(), idempotencyKey, product.TenantId!.Value);
        var lineItem = OrderTestFactory.AddLineItem(order, product.Id, product.Name, 19m, quantity: 1);
        lineItem.Product = product;
        return order;
    }
}

public sealed class OrderStateChangedEventHandlerTests
{
    [Fact]
    public async Task OrderStateChangedEventHandler_PersistsAuditLogEntry()
    {
        await using var context = CreateRepositoryContext();
        var handler = new OrderStateChangedEventHandler(context, NullLogger<OrderStateChangedEventHandler>.Instance);
        var evt = new OrderStateChangedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            OrderStatus.Pending,
            OrderStatus.Completed,
            "captured",
            "ext-901");

        await handler.Handle(evt, CancellationToken.None);

        var auditLog = await context.OrderAuditLogs.SingleAsync();
        auditLog.OrderId.Should().Be(evt.OrderId);
        auditLog.TenantId.Should().Be(evt.TenantId);
        auditLog.PreviousStatus.Should().Be(OrderStatus.Pending);
        auditLog.NewStatus.Should().Be(OrderStatus.Completed);
        auditLog.Reason.Should().Be("captured");
        auditLog.ExternalPaymentId.Should().Be("ext-901");
        auditLog.InitiatedBy.Should().Be("System");
    }

    private static TestOrdersRepositoryDbContext CreateRepositoryContext()
    {
        var options = new DbContextOptionsBuilder<TestOrdersRepositoryDbContext>()
            .UseInMemoryDatabase($"OrderEvents_{Guid.NewGuid()}")
            .Options;

        return new TestOrdersRepositoryDbContext(options);
    }
}

internal sealed class TestOrdersDbContext(DbContextOptions<TestOrdersDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(order => order.Id);
            entity.Ignore(order => order.User);
            entity.Ignore(order => order.LineItems);
        });
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbContextTransaction>(new RecordingDbContextTransaction());
}

internal sealed class TestOrdersRepositoryDbContext(DbContextOptions<TestOrdersRepositoryDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderLineItem> OrderLineItems { get; set; } = null!;
    public DbSet<OrderAuditLog> OrderAuditLogs { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(order => order.Id);
            entity.Ignore(order => order.User);
            entity.HasMany(order => order.LineItems)
                .WithOne(lineItem => lineItem.Order)
                .HasForeignKey(lineItem => lineItem.OrderId);
        });

        modelBuilder.Entity<OrderLineItem>(entity =>
        {
            entity.HasKey(lineItem => lineItem.Id);
            entity.Ignore(lineItem => lineItem.UserProduct);
            entity.HasOne(lineItem => lineItem.Product)
                .WithMany()
                .HasForeignKey(lineItem => lineItem.ProductId);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Ignore(product => product.Creator);
            entity.Ignore(product => product.Pricing);
            entity.Ignore(product => product.SubscriptionPlans);
            entity.Ignore(product => product.UserProducts);
            entity.Ignore(product => product.PromoCodes);
            entity.Ignore(product => product.CommissionConfig);
            entity.Ignore(product => product.BundleItems);
            entity.Ignore(product => product.IncludedInBundles);
        });

        modelBuilder.Entity<OrderAuditLog>(entity =>
        {
            entity.HasKey(auditLog => auditLog.Id);
            entity.Ignore(auditLog => auditLog.Order);
        });
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbContextTransaction>(new RecordingDbContextTransaction());
}

internal sealed class RecordingDbContextTransaction : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public bool CommitCalled { get; private set; }
    public bool RollbackCalled { get; private set; }

    public void Commit() => CommitCalled = true;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        CommitCalled = true;
        return Task.CompletedTask;
    }

    public void Rollback() => RollbackCalled = true;

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RollbackCalled = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })!
            .MakeGenericMethod(expectedResultType)
            .Invoke(inner, new object[] { expression });

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal sealed class TestAsyncDbSet<T>(IEnumerable<T> items) : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>, IEnumerable<T>
    where T : class
{
    private readonly IQueryable<T> queryable = new TestAsyncEnumerable<T>(items);

    public override IEntityType EntityType => throw new NotSupportedException();

    public Type ElementType => queryable.ElementType;

    public Expression Expression => queryable.Expression;

    public IQueryProvider Provider => new TestAsyncQueryProvider<T>(queryable.Provider);

    public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(queryable.GetEnumerator());

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => queryable.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => queryable.GetEnumerator();
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    {
    }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
}

internal static class OrderTestFactory
{
    public static Order CreatePendingOrder(Guid? tenantId = null, string? idempotencyKey = null)
    {
        return Order.Create(
            Guid.NewGuid(),
            idempotencyKey ?? $"order-{Guid.NewGuid():N}",
            tenantId ?? Guid.NewGuid());
    }

    public static IActorContextAccessor CreateActor(Order? order = null, Guid? userId = null, Guid? tenantId = null)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(mock => mock.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = (userId ?? order?.UserId ?? Guid.NewGuid()).ToString(),
            TenantId = tenantId ?? order?.TenantId ?? Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return accessor.Object;
    }

    public static OrderLineItemPricingSnapshot CreatePricingSnapshot(
        decimal unitPrice,
        string currency = "USD",
        decimal? basePrice = null,
        decimal? salePrice = null)
    {
        return new OrderLineItemPricingSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            basePrice ?? unitPrice,
            salePrice,
            unitPrice,
            currency);
    }

    public static IOrderPaymentAuthority CreatePaymentAuthority(bool isSettled = true)
    {
        var authority = new Mock<IOrderPaymentAuthority>();
        authority.Setup(mock => mock.IsSettledAsync(
                It.IsAny<OrderPaymentBinding>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(isSettled);
        return authority.Object;
    }

    public static OrderLineItem AddLineItem(
        Order order,
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity = 1,
        decimal discountAmount = 0,
        string? promoCodesApplied = null,
        bool isSubscription = false)
    {
        return order.AddLineItem(
            productId,
            productName,
            CreatePricingSnapshot(unitPrice, order.Currency),
            quantity,
            discountAmount,
            promoCodesApplied,
            isSubscription: isSubscription);
    }
}
