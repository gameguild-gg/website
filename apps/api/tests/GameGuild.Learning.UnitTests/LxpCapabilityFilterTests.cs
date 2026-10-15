using System.Security.Claims;
using FluentAssertions;
using GameGuild.Features;
using GameGuild.Learning.Attributes;
using GameGuild.Learning.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.UnitTests;

public sealed class LxpCapabilityFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_ActionWithoutCapability_Continues()
    {
        var context = CreateContext(CreateUser("User"), null, endpointMetadata: []);
        var nextCalled = false;

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_AuthenticatedSystemAdmin_BypassesTenantCapability()
    {
        var capabilityService = new Mock<ICapabilityService>(MockBehavior.Strict);
        var context = CreateContext(
            CreateUser("SystemAdmin"),
            capabilityService.Object);
        var nextCalled = false;

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
        capabilityService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionExecutionAsync_RegularUserWithoutCapability_RemainsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Social, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var context = CreateContext(
            CreateUser("User", tenantId),
            capabilityService.Object);
        var nextCalled = false;

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => nextCalled = true));

        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ResolvedRequestTenant_UsesValidatedTenantContext()
    {
        var tenantId = Guid.NewGuid();
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Social, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var requestContext = new Mock<IRequestContextAccessor>();
        requestContext.SetupGet(accessor => accessor.CurrentTenantId).Returns(tenantId);
        var context = CreateContext(CreateUser("User"), capabilityService.Object, requestContext.Object);
        var nextCalled = false;

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingTenant_ReturnsBadRequest()
    {
        var context = CreateContext(CreateUser("User"), new Mock<ICapabilityService>().Object);

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => { }));

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Be("Missing Tenant ID");
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingCapabilityService_FailsClosed()
    {
        var context = CreateContext(CreateUser("User", Guid.NewGuid()), null);

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => { }));

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Be("Service Unavailable");
    }

    [Theory]
    [InlineData("route")]
    [InlineData("header")]
    [InlineData("query")]
    public async Task OnActionExecutionAsync_ResolvesTenantFromHttpRequest(string source)
    {
        var tenantId = Guid.NewGuid();
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Social, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var context = CreateContext(CreateUser("User"), capabilityService.Object);
        switch (source)
        {
            case "route":
                context.HttpContext.Request.RouteValues["tenantId"] = tenantId.ToString();
                break;
            case "header":
                context.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
                break;
            case "query":
                context.HttpContext.Request.QueryString = QueryString.Create("tenantId", tenantId.ToString());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
        var nextCalled = false;

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        capabilityService.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task OnActionExecutionAsync_InvalidRouteTenant_FallsBackToCanonicalTenantClaim(string? routeTenant)
    {
        var tenantId = Guid.NewGuid();
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Social, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var user = CreateUser("User");
        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim("tenantId", tenantId.ToString()));
        var context = CreateContext(user, capabilityService.Object);
        context.HttpContext.Request.RouteValues["tenantId"] = routeTenant;
        var nextCalled = false;

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        capabilityService.VerifyAll();
    }

    [Fact]
    public async Task OnActionExecutionAsync_CustomCapabilityMessage_IsReturned()
    {
        var tenantId = Guid.NewGuid();
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Social, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Discovery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var context = CreateContext(
            CreateUser("User", tenantId),
            capabilityService.Object,
            endpointMetadata:
            [
                new LxpCapabilityAttribute(LxpCapabilities.Social),
                new LxpCapabilityAttribute(LxpCapabilities.Discovery) { ErrorMessage = "Upgrade required" }
            ]);

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => { }));

        var problem = context.Result.Should().BeOfType<ObjectResult>().Which.Value
            .Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Upgrade required");
        problem.Extensions["capability"].Should().Be(LxpCapabilities.Discovery);
        problem.Extensions["upgradeUrl"].Should().Be("/settings/subscription");
    }

    [Fact]
    public async Task OnActionExecutionAsync_CapabilityFailure_FailsClosed()
    {
        var tenantId = Guid.NewGuid();
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService
            .Setup(service => service.IsCapabilityEnabledAsync(tenantId, LxpCapabilities.Social, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("feature store unavailable"));
        var context = CreateContext(CreateUser("User", tenantId), capabilityService.Object);

        await CreateFilter().OnActionExecutionAsync(context, CreateNext(context, () => { }));

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Unable to verify feature access. Please try again later.");
    }

    [Fact]
    public void Attribute_CreatesReusableFilterFromRegisteredLogger()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var attribute = new LxpCapabilityFilterAttribute();

        attribute.IsReusable.Should().BeTrue();
        attribute.CreateInstance(services).Should().BeOfType<LxpCapabilityFilter>();
    }

    private static LxpCapabilityFilter CreateFilter()
        => new(NullLogger<LxpCapabilityFilter>.Instance);

    private static ActionExecutingContext CreateContext(
        ClaimsPrincipal user,
        ICapabilityService? capabilityService,
        IRequestContextAccessor? requestContextAccessor = null,
        IReadOnlyList<object>? endpointMetadata = null)
    {
        var serviceCollection = new ServiceCollection();
        if (capabilityService is not null)
        {
            serviceCollection.AddSingleton(capabilityService);
        }
        if (requestContextAccessor is not null)
        {
            serviceCollection.AddSingleton(requestContextAccessor);
        }
        var services = serviceCollection.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = services
        };
        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = endpointMetadata is null
                ? new List<object> { new LxpCapabilityAttribute(LxpCapabilities.Social) }
                : endpointMetadata.ToList()
        };

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), actionDescriptor),
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ClaimsPrincipal CreateUser(string role, Guid? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, ClaimTypes.Role));
    }

    private static ActionExecutionDelegate CreateNext(ActionExecutingContext context, Action callback)
        => () =>
        {
            callback();
            return Task.FromResult(new ActionExecutedContext(
                context,
                context.Filters,
                context.Controller));
        };
}
