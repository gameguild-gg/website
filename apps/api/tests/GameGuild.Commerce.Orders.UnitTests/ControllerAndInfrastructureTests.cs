using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Orders.UnitTests;

public class OrdersControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IActorContextAccessor> _actorMock = new();
    private readonly OrdersController _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public OrdersControllerTests()
    {
        _actorMock.Setup(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            SubjectId = _userId.ToString(),
            TenantId = _tenantId,
            ActorKind = ActorKind.User,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });

        _sut = new OrdersController(_senderMock.Object, _actorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        _sut.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        _sut.HttpContext.Request.Headers.UserAgent = "OrdersUnitTests/1.0";
    }

    [Fact]
    public async Task CreateOrder_ReturnsCreatedAtAction_OnSuccess()
    {
        var order = CreateTestOrder();
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<CreateOrderCommand>(command =>
                    command.IdempotencyKey == "idem-key-123" &&
                    command.IpAddress == "127.0.0.1" &&
                    command.UserAgent == "OrdersUnitTests/1.0"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var result = await _sut.CreateOrder(new CreateOrderRequest("idem-key-123"));

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<OrderDto>().Subject;
        dto.Id.Should().Be(order.Id);
        dto.LineItems.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateOrder_ReturnsOk_WhenDuplicate()
    {
        var order = CreateTestOrder();
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order, wasDuplicate: true)));

        var result = await _sut.CreateOrder(new CreateOrderRequest("dup-key-123"));

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_ReturnsBadRequest_OnFailure()
    {
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OrderOperationResult>(Error.Failure("Orders.CreateFailed", "failed")));

        var result = await _sut.CreateOrder(new CreateOrderRequest("bad-key-123"));

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_AllowsMissingIpAddress()
    {
        var order = CreateTestOrder();
        _sut.HttpContext.Connection.RemoteIpAddress = null;
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<CreateOrderCommand>(command => command.IpAddress == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var result = await _sut.CreateOrder(new CreateOrderRequest("idem-null-ip"));

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task AddProductToOrder_ReturnsOk_OnSuccess()
    {
        var order = CreateTestOrder();
        var productId = Guid.NewGuid();
        var pricingId = Guid.NewGuid();
        var pricingVersionId = Guid.NewGuid();
        _senderMock.Setup(sender => sender.Send<Result<Order>>(
                It.Is<AddProductToOrderCommand>(command =>
                    command.OrderId == order.Id &&
                    command.ProductId == productId &&
                    command.ProductPricingId == pricingId &&
                    command.ProductPricingVersionId == pricingVersionId &&
                    command.Quantity == 2 &&
                    command.PromoCode == "PROMO10"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(order));

        var result = await _sut.AddProductToOrder(
            order.Id,
            new AddOrderItemRequest(productId, pricingId, pricingVersionId, 2, "PROMO10"));

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddProductToOrder_ReturnsBadRequest_OnFailure()
    {
        _senderMock.Setup(sender => sender.Send<Result<Order>>(It.IsAny<AddProductToOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Order>(Error.Failure("Orders.AddItemFailed", "failed")));

        var result = await _sut.AddProductToOrder(
            Guid.NewGuid(),
            new AddOrderItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteOrder_ReturnsOk_WithNullRequest()
    {
        var order = CreateTestOrder();
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<CompleteOrderCommand>(command =>
                    command.OrderId == order.Id &&
                    command.PaymentId == null &&
                    command.PaymentProviderReference == null &&
                    command.PaymentMethod == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var result = await _sut.CompleteOrder(order.Id, null);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteOrder_MapsEconomyMarketplaceIntent()
    {
        var order = CreateTestOrder();
        var evidence = new CompleteOrderMarketplaceSettlement(
            OrderMarketplaceCurrencyChoice.FixedMix,
            "idempotency");
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<CompleteOrderCommand>(command =>
                    command.OrderId == order.Id &&
                    command.MarketplaceSettlement == evidence),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var result = await _sut.CompleteOrder(
            order.Id,
            new CompleteOrderRequest(MarketplaceSettlement: evidence));

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CancelOrder_ReturnsExpectedResults()
    {
        _senderMock.SetupSequence(sender => sender.Send<Result>(It.IsAny<CancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success())
            .ReturnsAsync(Result.Failure(Error.Failure("Orders.CancelFailed", "failed")));

        var noContent = await _sut.CancelOrder(Guid.NewGuid(), new CancelOrderRequest("reason"));
        var badRequest = await _sut.CancelOrder(Guid.NewGuid(), new CancelOrderRequest("reason"));

        noContent.Should().BeOfType<NoContentResult>();
        badRequest.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefundOrder_ReturnsOk_OnSuccess()
    {
        var order = CreateTestOrder();
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<RefundOrderCommand>(command =>
                    command.OrderId == order.Id &&
                    command.Amount == 12.5m &&
                    command.Reason == "refund reason"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var result = await _sut.RefundOrder(order.Id, new RefundOrderRequest(12.5m, "refund reason"));

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrder_ReturnsNotFound_OrOk()
    {
        var order = CreateTestOrder();
        _senderMock.SetupSequence(sender => sender.Send<Order?>(It.IsAny<GetOrderQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null)
            .ReturnsAsync(order);

        var notFound = await _sut.GetOrder(Guid.NewGuid());
        var ok = await _sut.GetOrder(order.Id);

        notFound.Result.Should().BeOfType<NotFoundResult>();
        ok.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ListOrders_UsesExpectedQueryBranch()
    {
        var order = CreateTestOrder();
        _actorMock.Setup(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            SubjectId = _userId.ToString(), TenantId = _tenantId, ActorKind = ActorKind.User,
            Roles = new HashSet<string>(), Permissions = new HashSet<string> { OrdersPermission.Keys.ReadAll }, IsAuthenticated = true
        });
        _senderMock.Setup(sender => sender.Send<IEnumerable<Order>>(
                It.Is<GetAllOrdersQuery>(query => query.Status == OrderStatus.Pending),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);
        _senderMock.Setup(sender => sender.Send<IEnumerable<Order>>(
                It.Is<GetUserOrdersQuery>(query => query.UserId == _userId && query.Status == OrderStatus.Completed),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([order]);

        var allOrders = await _sut.ListOrders(owner: null, status: OrderStatus.Pending);
        var myOrders = await _sut.ListOrders(owner: "me", status: OrderStatus.Completed);

        allOrders.Result.Should().BeOfType<OkObjectResult>();
        myOrders.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ListOrders_ForbidsWhenActorSubjectIsInvalid()
    {
        _actorMock.Setup(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            SubjectId = "not-a-guid",
            TenantId = _tenantId,
            ActorKind = ActorKind.User,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });

        var result = await _sut.ListOrders(owner: "me", status: OrderStatus.Pending);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OrderExists_ReturnsOk_OrNotFound()
    {
        _senderMock.SetupSequence(sender => sender.Send<bool>(It.IsAny<OrderExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        var ok = await _sut.OrderExists(Guid.NewGuid());
        var notFound = await _sut.OrderExists(Guid.NewGuid());

        ok.Should().BeOfType<OkResult>();
        notFound.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateOrder_ReturnsOk_OnSuccess()
    {
        var order = CreateTestOrder();
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<UpdateOrderCommand>(command =>
                    command.OrderId == order.Id &&
                    command.Currency == "EUR" &&
                    command.Notes == "notes" &&
                    command.Metadata == metadata),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var result = await _sut.UpdateOrder(order.Id, new PatchOrderRequest("EUR", "notes", metadata));

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteOrder_ReturnsExpectedResults()
    {
        _senderMock.SetupSequence(sender => sender.Send<Result>(It.IsAny<DeleteOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success())
            .ReturnsAsync(Result.Failure(Error.Failure("Orders.DeleteFailed", "failed")));

        var noContent = await _sut.DeleteOrder(Guid.NewGuid(), "cleanup");
        var badRequest = await _sut.DeleteOrder(Guid.NewGuid(), "cleanup");

        noContent.Should().BeOfType<NoContentResult>();
        badRequest.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CaptureHoldAndRelease_ReturnOk_OnSuccess()
    {
        var order = CreateTestOrder();
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<CaptureOrderCommand>(command => command.OrderId == order.Id && command.PaymentMethodId == "pm_test"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<HoldOrderCommand>(command => command.OrderId == order.Id && command.Reason == "manual review"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.Is<ReleaseOrderCommand>(command => command.OrderId == order.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order)));

        var capture = await _sut.CaptureOrder(order.Id, new CaptureOrderRequest("pm_test"));
        var hold = await _sut.HoldOrder(order.Id, new HoldOrderRequest("manual review"));
        var release = await _sut.ReleaseOrder(order.Id);

        capture.Result.Should().BeOfType<OkObjectResult>();
        hold.Result.Should().BeOfType<OkObjectResult>();
        release.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CaptureOrder_ReturnsPaymentActionContract()
    {
        var order = CreateTestOrder();
        var paymentId = Guid.NewGuid();
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.IsAny<CaptureOrderCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new OrderOperationResult(
                order,
                PaymentState: OrderChargeState.RequiresAction,
                PaymentId: paymentId,
                ClientActionToken: "pi_secret_test",
                PaymentMessage: "Additional authentication is required.")));

        var result = await _sut.CaptureOrder(order.Id, new CaptureOrderRequest("pm_test"));

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<OrderCaptureDto>().Subject;
        response.PaymentState.Should().Be(OrderChargeState.RequiresAction);
        response.PaymentId.Should().Be(paymentId);
        response.ClientActionToken.Should().Be("pi_secret_test");
    }

    [Fact]
    public async Task CaptureOrder_ReturnsSucceededStateForSettledOrder()
    {
        var order = CreateTestOrder();
        var paymentId = Guid.NewGuid();
        order.StartPaymentProcessing();
        order.MarkAsPaidPendingFulfillment(paymentId, "pi_settled");
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(
                It.IsAny<CaptureOrderCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(OrderOperationResult.FromOrder(order, wasDuplicate: true)));

        var result = await _sut.CaptureOrder(order.Id, new CaptureOrderRequest("pm_test"));

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<OrderCaptureDto>().Subject;
        response.PaymentState.Should().Be(OrderChargeState.Succeeded);
        response.PaymentId.Should().Be(paymentId);
    }

    [Fact]
    public async Task PreparePaymentIntent_ReturnsOkOrBadRequest()
    {
        var preparation = new OrderPaymentIntentPreparation(
            true, Guid.NewGuid(), "pi_secret", null, OrderChargeState.RequiresAction);
        _senderMock.SetupSequence(sender => sender.Send<Result<OrderPaymentIntentPreparation>>(
                It.IsAny<PrepareOrderPaymentIntentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(preparation))
            .ReturnsAsync(Result.Failure<OrderPaymentIntentPreparation>(
                Error.Failure("Orders.PaymentIntentUnavailable", "Provider unavailable.")));

        var ok = await _sut.PreparePaymentIntent(Guid.NewGuid());
        var badRequest = await _sut.PreparePaymentIntent(Guid.NewGuid());

        ok.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(preparation);
        badRequest.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ReleaseOrder_ReturnsBadRequestWithDefaultProblemDetailsWhenDescriptionIsNull()
    {
        _senderMock.Setup(sender => sender.Send<Result<OrderOperationResult>>(It.IsAny<ReleaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OrderOperationResult>(Error.Failure("Orders.ReleaseFailed", null!)));

        var result = await _sut.ReleaseOrder(Guid.NewGuid());

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("An unexpected error occurred.");
    }

    private Order CreateTestOrder()
    {
        var order = Order.Create(_userId, "idem-" + Guid.NewGuid().ToString("N")[..12], _tenantId);
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Product", 10m, quantity: 2, discountAmount: 1m, promoCodesApplied: "PROMO10");
        return order;
    }
}

public class OrdersInfrastructureTests
{
    [Fact]
    public void OrdersController_ExposesOnlyVerifiedCheckoutActionsToMinimumComposition()
    {
        var markedActions = typeof(OrdersController)
            .GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(MinimumOrderRouteAttribute), inherit: true).Length > 0)
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();

        markedActions.Should().Equal(
            nameof(OrdersController.AddProductToOrder),
            nameof(OrdersController.CaptureOrder),
            nameof(OrdersController.CompleteOrder),
            nameof(OrdersController.CreateOrder),
            nameof(OrdersController.GetOrder),
            nameof(OrdersController.ListOrders),
            nameof(OrdersController.PreparePaymentIntent));
    }

    [Fact]
    public void CommandHandlers_CanBeInstantiated()
    {
        var orderRepo = Mock.Of<IOrderRepository>();
        var productRepo = Mock.Of<IProductRepository>();
        var pricingRepo = Mock.Of<IProductPricingRepository>();
        var promoService = Mock.Of<IPromoCodeService>();
        var entitlementService = Mock.Of<IEntitlementService>();
        var dbContext = Mock.Of<IApplicationDbContext>();
        var actor = OrderTestFactory.CreateActor();

        var handlers = new object[]
        {
            new CreateOrderCommandHandler(orderRepo, actor),
            new AddProductToOrderCommandHandler(orderRepo, productRepo, pricingRepo, promoService, Mock.Of<IPromoCodeUsageRecorder>(), dbContext, actor),
            new CancelOrderCommandHandler(orderRepo),
            new CaptureOrderCommandHandler(orderRepo, Mock.Of<IOrderPaymentProcessor>(), actor),
            new CompleteOrderCommandHandler(orderRepo, entitlementService, dbContext, OrderTestFactory.CreatePaymentAuthority(), actor),
            new DeleteOrderCommandHandler(orderRepo),
            new HoldOrderCommandHandler(orderRepo),
            new RefundOrderCommandHandler(orderRepo, entitlementService),
            new ReleaseOrderCommandHandler(orderRepo),
            new UpdateOrderCommandHandler(orderRepo, actor)
        };

        handlers.Should().AllSatisfy(handler => handler.Should().NotBeNull());
    }

    [Fact]
    public void QueryHandlers_CanBeInstantiated()
    {
        var orderRepo = Mock.Of<IOrderRepository>();
        var dbContext = Mock.Of<IApplicationDbContext>();

        var handlers = new object[]
        {
            new GetOrderQueryHandler(orderRepo),
            new GetAllOrdersQueryHandler(dbContext),
            new GetUserOrdersQueryHandler(orderRepo),
            new OrderExistsQueryHandler(orderRepo)
        };

        handlers.Should().AllSatisfy(handler => handler.Should().NotBeNull());
    }

    [Fact]
    public void OrderStateChangedEventHandler_CanBeInstantiated()
    {
        var handler = new OrderStateChangedEventHandler(Mock.Of<IApplicationDbContext>(), NullLogger<OrderStateChangedEventHandler>.Instance);

        handler.Should().NotBeNull();
    }

    [Fact]
    public void OrderRepository_CanBeInstantiated()
    {
        var dbContext = new Mock<IApplicationDbContext>();
        dbContext.Setup(context => context.Set<Order>()).Returns(new Mock<DbSet<Order>>().Object);

        var repository = new OrderRepository(dbContext.Object);

        repository.Should().NotBeNull();
        repository.Should().BeAssignableTo<IOrderRepository>();
    }

    [Fact]
    public void AddOrdersModule_RegistersOrderRepository()
    {
        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => Mock.Of<IApplicationDbContext>());

        services.AddOrdersModule();

        var provider = services.BuildServiceProvider();
        provider.GetService<IOrderRepository>().Should().BeOfType<OrderRepository>();
    }

    [Fact]
    public async Task AddOrdersModule_RegistersFailClosedPaymentAuthority()
    {
        var services = new ServiceCollection();
        services.AddOrdersModule();
        var provider = services.BuildServiceProvider();
        var authority = provider.GetRequiredService<IOrderPaymentAuthority>();
        var binding = new OrderPaymentBinding(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, "USD");

        var isSettled = await authority.IsSettledAsync(binding);

        isSettled.Should().BeFalse();
    }

    [Fact]
    public async Task AddOrdersModule_RegistersFailClosedEconomyMarketplaceAuthority()
    {
        var services = new ServiceCollection();
        services.AddOrdersModule();
        var provider = services.BuildServiceProvider();
        var authority = provider.GetRequiredService<IOrderMarketplaceSettlementAuthority>();

        var result = await authority.SettleAsync(new OrderMarketplaceSettlementRequest(
            Guid.NewGuid(),
            OrderMarketplaceCurrencyChoice.Hard,
            "idempotency"));

        result.IsAccepted.Should().BeFalse();
        result.ErrorCode.Should().Be("Orders.EconomyMarketplaceDisabled");
    }

    [Fact]
    public void AddOrdersModule_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        services.AddOrdersModule().Should().BeSameAs(services);
    }

    [Fact]
    public void ConfigureOrdersModel_ConfiguresEntities()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());

        OrdersModule.ConfigureOrdersModel(modelBuilder);

        modelBuilder.Model.FindEntityType(typeof(Order)).Should().NotBeNull();
        modelBuilder.Model.FindEntityType(typeof(OrderLineItem)).Should().NotBeNull();
        modelBuilder.Model.FindEntityType(typeof(OrderAuditLog)).Should().NotBeNull();
    }

    [Fact]
    public void OrdersModelConfiguration_DelegatesToModule()
    {
        var configuration = new OrdersModelConfiguration();
        var modelBuilder = new ModelBuilder(new ConventionSet());

        configuration.Configure(modelBuilder);

        modelBuilder.Model.FindEntityType(typeof(Order)).Should().NotBeNull();
    }

    [Fact]
    public void OrderAndRequestDtos_CanBeCreated()
    {
        var orderDto = new OrderDto(
            Guid.NewGuid(), Guid.NewGuid(), "key", OrderStatus.Pending,
            100m, 10m, 5m, 95m, "USD", null, null, null, null, null, null,
            DateTime.UtcNow, DateTime.UtcNow, []);
        var lineItemDto = new OrderLineItemDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            "Product", 10m, 12m, 10m, "USD", 2, 4m, "PROMO10", 16m, false);
        var create = new CreateOrderRequest("key-12345678");
        var addItem = new AddOrderItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, "PROMO");
        var complete = new CompleteOrderRequest(Guid.NewGuid(), "ref-123", "card");
        var cancel = new CancelOrderRequest("reason");
        var refund = new RefundOrderRequest(10m, "refund");
        var patch = new PatchOrderRequest("EUR", "notes", new Dictionary<string, string> { ["k"] = "v" });
        var capture = new CaptureOrderRequest("pm_test");
        var hold = new HoldOrderRequest("hold reason");

        orderDto.Status.Should().Be(OrderStatus.Pending);
        lineItemDto.Quantity.Should().Be(2);
        create.IdempotencyKey.Should().Be("key-12345678");
        addItem.Quantity.Should().Be(2);
        complete.PaymentMethod.Should().Be("card");
        cancel.Reason.Should().Be("reason");
        refund.Amount.Should().Be(10m);
        patch.Currency.Should().Be("EUR");
        capture.PaymentMethodId.Should().Be("pm_test");
        hold.Reason.Should().Be("hold reason");
    }

    [Fact]
    public void OrderLineItem_PartialConstructor_IsNotExposed()
    {
        typeof(OrderLineItem).GetConstructor([typeof(object)]).Should().BeNull();
    }
}
