using FluentAssertions;
using GameGuild.Commerce.Products;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Orders.UnitTests;

public sealed class AuthoritativeOrderCommandTests
{
    [Fact]
    public async Task CreateOrder_DerivesUserAndTenantFromActorContext()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<IOrderRepository>();
        Order? addedOrder = null;
        repository.Setup(mock => mock.GetByIdempotencyKeyAsync("actor-owned-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repository.Setup(mock => mock.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => addedOrder = order);

        var handler = new CreateOrderCommandHandler(repository.Object, CreateActor(userId, tenantId));

        var result = await handler.Handle(
            new CreateOrderCommand("actor-owned-key", "127.0.0.1", "unit-test"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedOrder.Should().NotBeNull();
        addedOrder!.UserId.Should().Be(userId);
        addedOrder.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void CreateOrderRequest_DoesNotAcceptIdentityOrCurrencyFromTheBody()
    {
        typeof(CreateOrderRequest).GetProperty("UserId").Should().BeNull();
        typeof(CreateOrderRequest).GetProperty("TenantId").Should().BeNull();
        typeof(CreateOrderRequest).GetProperty("Currency").Should().BeNull();
    }

    [Fact]
    public async Task CreateOrder_RejectsAuthenticatedNonUserActor()
    {
        var repository = new Mock<IOrderRepository>();
        var handler = new CreateOrderCommandHandler(
            repository.Object,
            CreateActor(Guid.NewGuid(), Guid.NewGuid(), ActorKind.Service));

        var result = await handler.Handle(new CreateOrderCommand("service-actor-key"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.UserActorRequired");
        repository.Verify(mock => mock.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOrder_RejectsCrossUserMutation()
    {
        var tenantId = Guid.NewGuid();
        var order = Order.Create(Guid.NewGuid(), "cross-user-key", tenantId);
        var repository = RepositoryWith(order);
        var handler = new UpdateOrderCommandHandler(
            repository.Object,
            CreateActor(Guid.NewGuid(), tenantId));

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, Notes: "spoofed"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.Forbidden");
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOrder_RejectsCrossTenantMutation()
    {
        var userId = Guid.NewGuid();
        var order = Order.Create(userId, "cross-tenant-key", Guid.NewGuid());
        var repository = RepositoryWith(order);
        var handler = new UpdateOrderCommandHandler(
            repository.Object,
            CreateActor(userId, Guid.NewGuid()));

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, Notes: "spoofed"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.Forbidden");
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProduct_RejectsUnpublishedProduct()
    {
        var fixture = CreatePricingFixture(isPublished: false);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.Unavailable");
        fixture.Order.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProduct_RejectsCrossTenantProductAndPricing()
    {
        var fixture = CreatePricingFixture(productTenantId: Guid.NewGuid());

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PricingTenantMismatch");
        fixture.Order.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProduct_RejectsStalePricingVersion()
    {
        var fixture = CreatePricingFixture(staleVersion: true);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.StalePricing");
        fixture.Order.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProduct_RejectsZeroPrice()
    {
        var fixture = CreatePricingFixture(basePrice: 0m);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidPrice");
        fixture.Order.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProduct_RejectsMixedCurrencies()
    {
        var fixture = CreatePricingFixture(currency: "EUR");
        fixture.Order.AddLineItem(
            Guid.NewGuid(),
            "Existing item",
            new OrderLineItemPricingSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, 10m, null, 10m, "USD"));

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.MixedCurrency");
        fixture.Order.LineItems.Should().ContainSingle();
    }

    [Fact]
    public async Task AddProduct_RejectsFixedDiscountInAnotherCurrency()
    {
        var fixture = CreatePricingFixture(currency: "BRL", fixedPromoCurrency: "USD");

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.MixedCurrency");
        fixture.Order.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProduct_SnapshotsPriceCurrencyVersionAndComputesTotalsServerSide()
    {
        var fixture = CreatePricingFixture(basePrice: 100m, salePrice: 80m, currency: "BRL", quantity: 2);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var lineItem = fixture.Order.LineItems.Should().ContainSingle().Subject;
        lineItem.ProductPricingId.Should().Be(fixture.Pricing.Id);
        lineItem.ProductPricingVersionId.Should().Be(fixture.Version.Id);
        lineItem.PriceVersionSnapshot.Should().Be(fixture.Version.PriceVersion);
        lineItem.BasePriceSnapshot.Should().Be(100m);
        lineItem.SalePriceSnapshot.Should().Be(80m);
        lineItem.UnitPriceSnapshot.Should().Be(80m);
        lineItem.CurrencySnapshot.Should().Be("BRL");
        lineItem.LineTotal.Should().Be(160m);
        fixture.Order.Currency.Should().Be("BRL");
        fixture.Order.Subtotal.Should().Be(160m);
        fixture.Order.Total.Should().Be(160m);

        fixture.Pricing.Versions.Add(fixture.Version);
        fixture.Pricing.UpdatePrices(250m, 200m, "post-checkout mutation");

        lineItem.BasePriceSnapshot.Should().Be(100m);
        lineItem.SalePriceSnapshot.Should().Be(80m);
        lineItem.UnitPriceSnapshot.Should().Be(80m);
        lineItem.CurrencySnapshot.Should().Be("BRL");
        fixture.Order.Total.Should().Be(160m);
    }

    [Fact]
    public void OrderLineItem_RejectsReflectionBasedSnapshotMutation()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var lineItem = OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Immutable product", 45m);

        var mutate = () => lineItem.SetProperties(new Dictionary<string, object?>
        {
            [nameof(OrderLineItem.UnitPriceSnapshot)] = 1m,
            [nameof(OrderLineItem.CurrencySnapshot)] = "EUR",
            [nameof(OrderLineItem.PriceVersionSnapshot)] = 99
        });

        mutate.Should().Throw<InvalidOperationException>();
        lineItem.UnitPriceSnapshot.Should().Be(45m);
        lineItem.CurrencySnapshot.Should().Be("USD");
        lineItem.PriceVersionSnapshot.Should().Be(1);
    }

    [Fact]
    public void OrderLineItem_DoesNotExposePublicConstructionBypass()
    {
        typeof(OrderLineItem).GetConstructors().Should().BeEmpty();
    }

    [Fact]
    public async Task CaptureOrder_RejectsOrderWithoutAuthoritativeLineItems()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidPayableOrder");
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaidAt.Should().BeNull();
        paymentProcessor.VerifyNoOtherCalls();
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CaptureOrder_UsesAuthoritativeOrderTotalAndMarksSuccessfulPayment()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m, quantity: 2);
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentId = Guid.NewGuid();
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        paymentProcessor
            .Setup(processor => processor.ProcessAsync(
                It.Is<AuthoritativeOrderCharge>(charge =>
                    charge.OrderId == order.Id &&
                    charge.TenantId == order.TenantId &&
                    charge.Amount == 90m &&
                    charge.Currency == "USD" &&
                    charge.PaymentMethodId == "pm_test"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderChargeResult.Succeeded(paymentId, "pi_order"));
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
        order.PaymentId.Should().Be(paymentId);
        order.ExternalPaymentId.Should().Be("pi_order");
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CaptureOrder_ShouldReleaseReservationWhenPaymentFails()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        paymentProcessor
            .Setup(processor => processor.ProcessAsync(
                It.IsAny<AuthoritativeOrderCharge>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderChargeResult.Failed(Guid.NewGuid(), "Card declined"));
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentFailed");
        result.Error.Description.Should().Be("Card declined");
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentId.Should().BeNull();
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CaptureOrder_RejectsMalformedPaymentMethodBeforeReservingOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("attacker-controlled"))
            .Returns("Payment method identifier is invalid.");
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(
            new CaptureOrderCommand(order.Id, "attacker-controlled"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidPaymentMethod");
        order.Status.Should().Be(OrderStatus.Pending);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        paymentProcessor.Verify(
            processor => processor.ProcessAsync(It.IsAny<AuthoritativeOrderCharge>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CaptureOrder_RejectsConcurrentOrderMutationBeforeCharging()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        var repository = RepositoryWith(order, withLineItems: true);
        repository
            .Setup(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrent order mutation"));
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ConcurrentModification");
        paymentProcessor.Verify(
            processor => processor.ProcessAsync(It.IsAny<AuthoritativeOrderCharge>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CaptureOrder_ReplaysAlreadyPaidOrderWithoutChargingOrSaving()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        var paymentId = Guid.NewGuid();
        order.MarkAsPaidPendingFulfillment(paymentId, "pi_existing");
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WasDuplicate.Should().BeTrue();
        order.PaymentId.Should().Be(paymentId);
        paymentProcessor.VerifyNoOtherCalls();
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CaptureOrder_ResumesReservedOrderWithoutSavingReservationAgain()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        order.StartPaymentProcessing();
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        paymentProcessor
            .Setup(processor => processor.ProcessAsync(
                It.IsAny<AuthoritativeOrderCharge>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderChargeResult.Processing(Guid.NewGuid(), "Payment is processing."));
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentState.Should().Be(OrderChargeState.Processing);
        order.Status.Should().Be(OrderStatus.Processing);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        paymentProcessor.Verify(
            processor => processor.ProcessAsync(It.IsAny<AuthoritativeOrderCharge>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CaptureOrder_ReturnsClientActionForReservedPayment()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        var paymentId = Guid.NewGuid();
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        paymentProcessor
            .Setup(processor => processor.ProcessAsync(
                It.IsAny<AuthoritativeOrderCharge>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderChargeResult.RequiresAction(
                paymentId,
                "Additional authentication is required.",
                "pi_secret_test"));
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentState.Should().Be(OrderChargeState.RequiresAction);
        result.Value.PaymentId.Should().Be(paymentId);
        result.Value.ClientActionToken.Should().Be("pi_secret_test");
        order.Status.Should().Be(OrderStatus.Processing);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CaptureOrder_ReleasesReservationAfterDefinitivePaymentFailure()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        paymentProcessor
            .Setup(processor => processor.ProcessAsync(
                It.IsAny<AuthoritativeOrderCharge>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderChargeResult.Failed(Guid.NewGuid(), "Card declined"));
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentFailed");
        order.Status.Should().Be(OrderStatus.Pending);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CaptureOrder_ReturnsConflictWhenPaymentRetryLosesConcurrencyRace()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Authoritative product", 45m);
        order.StartPaymentProcessing();
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentProcessor = new Mock<IOrderPaymentProcessor>();
        paymentProcessor
            .Setup(processor => processor.GetPaymentMethodValidationError("pm_test"))
            .Returns((string?)null);
        paymentProcessor
            .Setup(processor => processor.ProcessAsync(
                It.IsAny<AuthoritativeOrderCharge>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrent payment retry"));
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            paymentProcessor.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentConcurrentModification");
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CaptureOrder_RejectsCrossTenantActor()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = RepositoryWith(order, withLineItems: true);
        var handler = new CaptureOrderCommandHandler(
            repository.Object,
            Mock.Of<IOrderPaymentProcessor>(),
            CreateActor(order.UserId, Guid.NewGuid()));

        var result = await handler.Handle(new CaptureOrderCommand(order.Id, "pm_test"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.Forbidden");
        order.Status.Should().Be(OrderStatus.Pending);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_RejectsArbitraryPaymentReferencesOnPendingOrder()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        var repository = RepositoryWith(order, withLineItems: true);
        var entitlementService = new Mock<IEntitlementService>();
        var dbContext = new Mock<IApplicationDbContext>();
        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            entitlementService.Object,
            dbContext.Object,
            Mock.Of<IOrderPaymentAuthority>(),
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(
            new CompleteOrderCommand(order.Id, Guid.NewGuid(), "attacker-reference", "card"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentAuthorityRequired");
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentId.Should().BeNull();
        entitlementService.VerifyNoOtherCalls();
        dbContext.Verify(mock => mock.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_RejectsCrossUserEvenWhenOrderIsPaidAndBound()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        order.MarkAsPaidPendingFulfillment(Guid.NewGuid(), "authoritative-reference");
        var repository = RepositoryWith(order, withLineItems: true);
        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IOrderPaymentAuthority>(),
            CreateActor(Guid.NewGuid(), order.TenantId!.Value));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.Forbidden");
        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task CompleteOrder_RejectsUnconfirmedPaymentBinding()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(order, Guid.NewGuid(), "Bound product", 45m);
        var paymentId = Guid.NewGuid();
        order.MarkAsPaidPendingFulfillment(paymentId, "provider-object");
        var repository = RepositoryWith(order, withLineItems: true);
        var paymentAuthority = new Mock<IOrderPaymentAuthority>();
        paymentAuthority.Setup(mock => mock.IsSettledAsync(
                It.Is<OrderPaymentBinding>(binding =>
                    binding.OrderId == order.Id &&
                    binding.PaymentId == paymentId &&
                    binding.TenantId == order.TenantId &&
                    binding.Amount == order.Total &&
                    binding.Currency == order.Currency),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            Mock.Of<IApplicationDbContext>(),
            paymentAuthority.Object,
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PaymentAuthorityRequired");
        order.Status.Should().Be(OrderStatus.Paid);
        repository.Verify(mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_RejectsSubscriptionWithoutSubscriptionAuthority()
    {
        var order = OrderTestFactory.CreatePendingOrder();
        OrderTestFactory.AddLineItem(
            order,
            Guid.NewGuid(),
            "Subscription product",
            30m,
            isSubscription: true);
        order.MarkAsPaidPendingFulfillment(Guid.NewGuid(), "provider-subscription");
        var repository = RepositoryWith(order, withLineItems: true);
        var dbContext = new Mock<IApplicationDbContext>();
        var handler = new CompleteOrderCommandHandler(
            repository.Object,
            Mock.Of<IEntitlementService>(),
            dbContext.Object,
            OrderTestFactory.CreatePaymentAuthority(),
            CreateActor(order.UserId, order.TenantId!.Value));

        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.SubscriptionAuthorityRequired");
        order.Status.Should().Be(OrderStatus.Paid);
        dbContext.Verify(mock => mock.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProduct_RecordsPricingDecisionLedgerEntryWithFullBreakdown()
    {
        var fixture = CreatePricingFixture(basePrice: 100m, salePrice: 80m, currency: "BRL", quantity: 2);

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.PricingLedgerWriter.Verify(
            mock => mock.RecordAsync(
                It.Is<GameGuild.Commerce.PricingDecisionRecord>(decision =>
                    decision.DecisionType == GameGuild.Commerce.PricingDecisionType.CheckoutLineItem &&
                    decision.TenantId == fixture.Order.TenantId &&
                    decision.ActorUserId == fixture.Order.UserId &&
                    decision.Order == fixture.Order.Id &&
                    decision.Product == fixture.Command.ProductId &&
                    decision.Breakdown.Quantity == 2 &&
                    decision.Breakdown.BaseUnitPrice == 100m &&
                    decision.Breakdown.SaleUnitPrice == 80m &&
                    decision.Breakdown.SaleActive &&
                    decision.Breakdown.BaseAmount == 160m &&
                    decision.Breakdown.PromoApplications.Count == 0 &&
                    decision.Breakdown.FinalAmount == 160m),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "every committed checkout price must append one durable pricing decision");
    }

    [Fact]
    public async Task AddProduct_LedgerReplayKey_IsStablePerOrderLine()
    {
        var fixture = CreatePricingFixture();

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        GameGuild.Commerce.PricingDecisionRecord? recorded = null;
        fixture.PricingLedgerWriter
            .Setup(mock => mock.RecordAsync(It.IsAny<GameGuild.Commerce.PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .Callback<GameGuild.Commerce.PricingDecisionRecord, CancellationToken>((decision, _) => recorded = decision)
            .ReturnsAsync((GameGuild.Commerce.PricingDecisionRecordingStatus.Recorded, new GameGuild.Commerce.PricingDecisionLedgerEntry()));

        fixture.PricingLedgerWriter.Verify(
            mock => mock.RecordAsync(
                It.Is<GameGuild.Commerce.PricingDecisionRecord>(decision =>
                    !string.IsNullOrWhiteSpace(decision.IdempotencyKey) &&
                    decision.IdempotencyKey!.StartsWith($"order:{fixture.Order.Id}:line:", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        recorded.Should().NotBeNull();
    }

    [Fact]
    public async Task AddProduct_FailsClosedWhenLedgerRejectsTheDecision()
    {
        var fixture = CreatePricingFixture();
        fixture.PricingLedgerWriter
            .Setup(mock => mock.RecordAsync(It.IsAny<GameGuild.Commerce.PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameGuild.Commerce.PricingDecisionRecordingStatus.Rejected, (GameGuild.Commerce.PricingDecisionLedgerEntry?)null));

        var result = await fixture.Handler.Handle(fixture.Command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PricingLedgerRejected");
    }

    private static PricingFixture CreatePricingFixture(
        bool isPublished = true,
        Guid? productTenantId = null,
        bool staleVersion = false,
        decimal basePrice = 100m,
        decimal? salePrice = null,
        string currency = "USD",
        int quantity = 1,
        string? fixedPromoCurrency = null)
    {
        var userId = Guid.NewGuid();
        var orderTenantId = Guid.NewGuid();
        var resolvedProductTenantId = productTenantId ?? orderTenantId;
        var order = Order.Create(userId, $"pricing-{Guid.NewGuid():N}", orderTenantId);
        var product = Product.Create("Authoritative product", tenantId: resolvedProductTenantId);
        product.IsPublished = isPublished;
        var (pricing, initialVersion) = ProductPricing.CreateWithVersion(
            product.Id,
            "Explicit tier",
            basePrice,
            salePrice,
            currency,
            isDefault: false,
            tenantId: resolvedProductTenantId);

        var requestedVersion = initialVersion;
        if (staleVersion)
        {
            pricing.Versions.Add(initialVersion);
            pricing.UpdateBasePrice(basePrice + 10m, "new current price");
        }

        var orderRepository = RepositoryWith(order, withLineItems: true);
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
            .Returns(new TestAsyncDbSet<ProductPricingVersion>([requestedVersion]));
        var promoCodeService = new Mock<IPromoCodeService>();
        if (fixedPromoCurrency is not null)
        {
            var promoCode = new PromoCode
            {
                Code = "FIXED",
                Type = PromoCodeType.FixedAmountOff,
                DiscountAmount = 10m,
                Currency = fixedPromoCurrency,
                TenantId = orderTenantId
            };
            promoCodeService.Setup(mock => mock.GetPromoCodeByCodeAsync(promoCode.Code))
                .ReturnsAsync(promoCode);
            promoCodeService.Setup(mock => mock.ValidatePromoCodeAsync(promoCode.Code, userId, product.Id))
                .ReturnsAsync(true);
        }

        var pricingLedgerWriter = new Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter>();
        pricingLedgerWriter
            .Setup(mock => mock.RecordAsync(It.IsAny<GameGuild.Commerce.PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameGuild.Commerce.PricingDecisionRecordingStatus.Recorded, new GameGuild.Commerce.PricingDecisionLedgerEntry()));

        var handler = new AddProductToOrderCommandHandler(
            orderRepository.Object,
            productRepository.Object,
            pricingRepository.Object,
            promoCodeService.Object,
            Mock.Of<IPromoCodeUsageRecorder>(),
            pricingLedgerWriter.Object,
            dbContext.Object,
            CreateActor(userId, orderTenantId));
        var command = new AddProductToOrderCommand(
            order.Id,
            product.Id,
            pricing.Id,
            requestedVersion.Id,
            quantity,
            fixedPromoCurrency is null ? null : "FIXED");

        return new PricingFixture(order, pricing, requestedVersion, command, handler, pricingLedgerWriter);
    }

    private static Mock<IOrderRepository> RepositoryWith(Order order, bool withLineItems = false)
    {
        var repository = new Mock<IOrderRepository>();
        if (withLineItems)
        {
            repository.Setup(mock => mock.GetWithLineItemsAsync(order.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
        }
        else
        {
            repository.Setup(mock => mock.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
        }

        return repository;
    }

    private static IActorContextAccessor CreateActor(
        Guid userId,
        Guid tenantId,
        ActorKind actorKind = ActorKind.User)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(mock => mock.ActorContext).Returns(new ActorContext
        {
            ActorKind = actorKind,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return accessor.Object;
    }

    private sealed record PricingFixture(
        Order Order,
        ProductPricing Pricing,
        ProductPricingVersion Version,
        AddProductToOrderCommand Command,
        AddProductToOrderCommandHandler Handler,
        Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter> PricingLedgerWriter);
}
