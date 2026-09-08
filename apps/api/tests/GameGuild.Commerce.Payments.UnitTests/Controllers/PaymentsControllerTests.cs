using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

public class PaymentsControllerTests
{
    [Fact]
    public async Task GetAll_ShouldScopeMissingTenantFilterToAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetAllPaymentsQuery>(query => query.TenantId == tenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var controller = CreateController(
            sender.Object,
            CreateAuthenticatedActorContext(tenantId),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.GetAll(null, null, null, null, ct: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(
                It.Is<GetAllPaymentsQuery>(query => query.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAll_ShouldRejectTenantFilterFromAnotherTenant()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(
            sender.Object,
            CreateAuthenticatedActorContext(Guid.NewGuid()),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.GetAll(Guid.NewGuid(), null, null, null, ct: CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(It.IsAny<GetAllPaymentsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_ShouldRejectPaymentFromAnotherTenant()
    {
        var actorTenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, Guid.NewGuid());
        var controller = CreateController(
            sender.Object,
            CreateAuthenticatedActorContext(actorTenantId),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.GetById(paymentId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Cancel_ShouldRejectPaymentFromAnotherTenant_BeforeDispatchingMutation()
    {
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, Guid.NewGuid());
        var controller = CreateController(
            sender.Object,
            CreateAuthenticatedActorContext(Guid.NewGuid()),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.Cancel(
            paymentId,
            new PaymentsController.CancelPaymentRequest("duplicate"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(It.IsAny<CancelPaymentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Refund_ShouldRejectPaymentFromAnotherTenant_BeforeDispatchingMutation()
    {
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, Guid.NewGuid());
        var controller = CreateController(
            sender.Object,
            CreateAuthenticatedActorContext(Guid.NewGuid()),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.Refund(
            paymentId,
            new PaymentsController.RefundRequest(10m, "duplicate"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(It.IsAny<ProcessRefundCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Retry_ShouldRejectPaymentFromAnotherTenant_BeforeDispatchingMutation()
    {
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, Guid.NewGuid());
        var controller = CreateController(
            sender.Object,
            CreateAuthenticatedActorContext(Guid.NewGuid()),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.Retry(paymentId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(It.IsAny<RetryPaymentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSetupIntent_ShouldCreateCustomerPersistExternalIdAndReturnClientSecret()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var stripeCustomerService = new Mock<IStripeCustomerService>();
        var paymentContextService = new Mock<ISubscriptionPaymentContextService>();

        paymentContextService.Setup(service => service.GetPaymentContextAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(subscriptionId, tenantId, 25m, "USD", null));

        stripeCustomerService
            .Setup(service => service.CreateCustomerAsync(
                It.Is<GatewayCustomerRequest>(request => request.Email == "owner@example.com" && request.Name == "Owner Name"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayCustomerResult(true, "cus_123", null, null));

        paymentContextService
            .Setup(service => service.SetExternalCustomerIdAsync(subscriptionId, "cus_123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        stripeCustomerService
            .Setup(service => service.CreateSetupIntentAsync(
                It.Is<GatewaySetupIntentRequest>(request => request.CustomerId == "cus_123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySetupIntentResult(true, "seti_123", "seti_123_secret_456", "cus_123", null, null));

        var handler = new CreateSetupIntentCommandHandler(stripeCustomerService.Object, paymentContextService.Object);
        sender.Setup(service => service.Send(It.IsAny<CreateSetupIntentCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateSetupIntentCommand command, CancellationToken token) => handler.Handle(command, token));
        var controller = CreateController(sender.Object, CreateAuthenticatedActorContext(tenantId), stripeCustomerService.Object, paymentContextService.Object);

        var result = await controller.CreateSetupIntent(
            new PaymentsController.CreateSetupIntentRequest(tenantId, subscriptionId, "owner@example.com", "Owner Name"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new PaymentsController.CreateSetupIntentResponse(
            subscriptionId,
            "cus_123",
            "seti_123",
            "seti_123_secret_456"));
    }

    [Fact]
    public async Task CompleteSubscriptionCheckout_ShouldRejectMalformedPaymentMethodIds()
    {
        var controller = CreateController(
            Mock.Of<ISender>(),
            CreateAuthenticatedActorContext(Guid.NewGuid()),
            Mock.Of<IStripeCustomerService>(),
            Mock.Of<ISubscriptionPaymentContextService>());

        var result = await controller.CompleteSubscriptionCheckout(
            new PaymentsController.CompleteSubscriptionCheckoutRequest(Guid.NewGuid(), Guid.NewGuid(), "4242 4242 4242 4242"),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteSubscriptionCheckout_ShouldSetDefaultPaymentMethodAndProcessPayment()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        const decimal amount = 25m;
        const string currency = "USD";

        var sender = new Mock<ISender>();
        var stripeCustomerService = new Mock<IStripeCustomerService>();
        var paymentContextService = new Mock<ISubscriptionPaymentContextService>();

        paymentContextService.Setup(service => service.GetPaymentContextAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(subscriptionId, tenantId, amount, currency, "cus_123"));
        sender.Setup(service => service.Send(
                It.Is<ProcessPaymentCommand>(command =>
                    command.TenantId == tenantId &&
                    command.SubscriptionId == subscriptionId &&
                    command.Amount == amount &&
                    command.PaymentMethodId == "pm_from_setup"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = true,
                PaymentId = "pay_123",
                Status = PaymentStatus.Succeeded,
                Amount = new Money(amount, currency)
            });

        stripeCustomerService
            .Setup(service => service.SetDefaultPaymentMethodAsync(
                It.Is<GatewayDefaultPaymentMethodRequest>(request =>
                    request.CustomerId == "cus_123" && request.PaymentMethodId == "pm_from_setup"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayDefaultPaymentMethodResult(true, null, null));

        var controller = CreateController(sender.Object, CreateAuthenticatedActorContext(tenantId), stripeCustomerService.Object, paymentContextService.Object);

        var result = await controller.CompleteSubscriptionCheckout(
            new PaymentsController.CompleteSubscriptionCheckoutRequest(tenantId, subscriptionId, "pm_from_setup"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new PaymentResult
        {
            Success = true,
            PaymentId = "pay_123",
            Status = PaymentStatus.Succeeded,
            Amount = new Money(amount, currency)
        });
    }

    private static PaymentsController CreateController(
        ISender sender,
        ActorContext actorContext,
        IStripeCustomerService stripeCustomerService,
        ISubscriptionPaymentContextService paymentContextService)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actorContext);

        return new PaymentsController(sender, accessor.Object, stripeCustomerService, paymentContextService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static Mock<ISender> CreateSenderWithPayment(Guid paymentId, Guid tenantId)
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetPaymentByIdQuery>(query => query.PaymentId == paymentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult
            {
                PaymentId = paymentId.ToString(),
                TenantId = tenantId,
                Amount = new Money(25m, "USD"),
                Status = PaymentStatus.Succeeded,
                Success = true
            });

        return sender;
    }

    private static ActorContext CreateAuthenticatedActorContext(Guid? tenantId)
    {
        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
    }

}
