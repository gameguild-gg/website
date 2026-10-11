using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Commerce.Payments;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

/// <summary>
///     Authorization contract for the payment-methods controller (issue #414): anonymous
/// requests never reach the controller ([Authorize]), non-owners get 403/404 and owners
/// succeed. Mutations go through ISender commands.
/// </summary>
public class PaymentMethodsControllerTests
{
    private readonly Mock<IPaymentMethodService> _paymentMethodService = new();

    private static ActorContext Actor(Guid tenantId, Guid userId, bool systemAdmin = false) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = userId.ToString(),
        TenantId = tenantId,
        Roles = new HashSet<string>(systemAdmin ? ["SystemAdmin"] : []),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Bearer",
        IsAuthenticated = true
    };

    private static ActorContext TenantlessActor(Guid userId) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = userId.ToString(),
        TenantId = null,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Bearer",
        IsAuthenticated = true
    };

    private PaymentMethodsController CreateController(ActorContext actorContext, ISender? sender = null)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actorContext);

        return new PaymentMethodsController(sender ?? Mock.Of<ISender>(), _paymentMethodService.Object, accessor.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static PaymentMethodDto Dto(Guid tenantId, Guid userId, Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        tenantId,
        userId,
        "Card",
        "stripe",
        "pm_owner",
        IsDefault: true,
        "Active",
        "visa",
        "4242",
        12,
        2031,
        Nickname: null,
        BillingAddress: null,
        LastUsedAt: null,
        ExpiresAt: null,
        ExpiresInDays: null,
        DateTime.UtcNow,
        DateTime.UtcNow);

    [Fact]
    public void Controller_AndAllActions_ShouldRequireAuthorization()
    {
        // Anonymous requests are rejected with 401 by the authentication pipeline before
        // any action executes; the contract is the attribute on the controller.
        typeof(PaymentMethodsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(PaymentMethodsController).GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();

        foreach (var action in typeof(PaymentMethodsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!action.Name.EndsWith("Async", StringComparison.Ordinal)
                && action.Name is not ("List" or "GetById" or "Create" or "Update" or "SetDefault" or "Delete"))
            {
                continue;
            }

            action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull(
                $"payment-method action {action.Name} must not allow anonymous access");
        }
    }

    [Fact]
    public async Task List_WithoutTenantContext_ShouldReturn403()
    {
        var controller = CreateController(TenantlessActor(Guid.NewGuid()));

        var result = await controller.List(tenantId: null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task List_NonAdminCrossTenant_ShouldReturn403()
    {
        var actorTenant = Guid.NewGuid();
        var controller = CreateController(Actor(actorTenant, Guid.NewGuid()));

        var result = await controller.List(tenantId: Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task List_Owner_ShouldReturn200WithOwnMethods()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var controller = CreateController(Actor(tenantId, userId));
        _paymentMethodService
            .Setup(service => service.ListAsync(It.IsAny<PaymentMethodActor>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PaymentMethodDto>>([Dto(tenantId, userId)]));

        var result = await controller.List(tenantId: null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task GetById_NonOwnerSameTenant_ShouldReturn403()
    {
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()));
        _paymentMethodService
            .Setup(service => service.GetByIdAsync(It.IsAny<PaymentMethodActor>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentMethodDto>(Error.Forbidden("PaymentMethods.NotOwner", "not owner")));

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        objectResult.Value.Should().BeOfType<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PaymentMethods.NotOwner");
    }

    [Fact]
    public async Task GetById_ForeignTenant_ShouldReturn404()
    {
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()));
        _paymentMethodService
            .Setup(service => service.GetByIdAsync(It.IsAny<PaymentMethodActor>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentMethodDto>(Error.NotFound("PaymentMethods.NotFound", "not found")));

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_Owner_ShouldReturn200()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var controller = CreateController(Actor(tenantId, userId));
        var dto = Dto(tenantId, userId);
        _paymentMethodService
            .Setup(service => service.GetByIdAsync(It.IsAny<PaymentMethodActor>(), dto.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var result = await controller.GetById(dto.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Create_ShouldRejectRawCardData()
    {
        var tenantId = Guid.NewGuid();
        var controller = CreateController(Actor(tenantId, Guid.NewGuid()));

        var result = await controller.Create(
            new PaymentMethodsController.CreatePaymentMethodRequestBody(tenantId, "4242424242424242"),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_CrossTenant_ShouldReturn403()
    {
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()));

        var result = await controller.Create(
            new PaymentMethodsController.CreatePaymentMethodRequestBody(Guid.NewGuid(), "pm_cross_tenant"),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Create_Owner_ShouldSendCommandAndReturn201()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dto = Dto(tenantId, userId);
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.IsAny<RegisterPaymentMethodCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));
        var controller = CreateController(Actor(tenantId, userId), sender.Object);

        var result = await controller.Create(
            new PaymentMethodsController.CreatePaymentMethodRequestBody(tenantId, "pm_new_card", SetAsDefault: true),
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtRouteResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.RouteName.Should().Be("GetPaymentMethodById");
        sender.Verify(
            service => service.Send(
                It.Is<RegisterPaymentMethodCommand>(command =>
                    command.TenantId == tenantId
                    && command.GatewayPaymentMethodId == "pm_new_card"
                    && command.SetAsDefault),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetDefault_ShouldSendCommand()
    {
        var paymentMethodId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.IsAny<SetDefaultPaymentMethodCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Dto(Guid.NewGuid(), Guid.NewGuid(), paymentMethodId)));
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()), sender.Object);

        var result = await controller.SetDefault(paymentMethodId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(
                It.Is<SetDefaultPaymentMethodCommand>(command => command.PaymentMethodId == paymentMethodId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_WhileInUse_ShouldReturn409ProblemDetails()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.IsAny<RemovePaymentMethodCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Conflict(
                "PaymentMethods.InUseBySubscriptions",
                "Payment method is the billing method for 2 active subscription(s).")));
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()), sender.Object);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PaymentMethods.InUseBySubscriptions");
    }

    [Fact]
    public async Task Delete_Success_ShouldReturn204()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.IsAny<RemovePaymentMethodCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()), sender.Object);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Update_ShouldSendCommandWithDisplayFieldsOnly()
    {
        var paymentMethodId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.IsAny<UpdatePaymentMethodCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Dto(Guid.NewGuid(), Guid.NewGuid(), paymentMethodId)));
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()), sender.Object);

        var result = await controller.Update(
            paymentMethodId,
            new PaymentMethodsController.UpdatePaymentMethodRequestBody(Nickname: "Corp"),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(
                It.Is<UpdatePaymentMethodCommand>(command =>
                    command.PaymentMethodId == paymentMethodId && command.Nickname == "Corp"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MutationHandlers_ShouldResolveActorsFromRequestContext()
    {
        // Command handlers must take the acting identity from IActorContextAccessor, never
        // from the command payload.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(Actor(tenantId, userId));

        var service = new Mock<IPaymentMethodService>();
        service
            .Setup(s => s.SetDefaultAsync(It.IsAny<PaymentMethodActor>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Dto(tenantId, userId)));

        var handler = new SetDefaultPaymentMethodCommandHandler(service.Object, accessor.Object);

        var result = await handler.Handle(new SetDefaultPaymentMethodCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        service.Verify(
            s => s.SetDefaultAsync(
                It.Is<PaymentMethodActor>(actor => actor.UserId == userId && actor.TenantId == tenantId && !actor.IsSystemAdmin),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
