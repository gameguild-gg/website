using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.API.Core.Security;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ISiemIntegrationService = GameGuild.Identity.Authentication.ISiemIntegrationService;

namespace GameGuild.API.UnitTests.Security;

public sealed class AuditingAuthorizationMiddlewareResultHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("fd50f29c-c3ad-47ca-8ef0-7b9e3c4073cb");
    private static readonly Guid TenantId = Guid.Parse("254b12b2-7e43-4c3d-9cb0-b03977d50e82");
    private static readonly Guid SessionId = Guid.Parse("17414753-a9d9-4bd9-a77f-c09cc516e4c8");

    [Fact]
    public void SetupAuthorization_UsesAuditingMiddlewareResultHandler()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new Mock<IAuditService>().Object);
        services.AddSingleton(new Mock<ISiemIntegrationService>().Object);
        services.AddScoped<IPermissionQueryService>(_ => new Mock<IPermissionQueryService>().Object);
        services.AddScoped(_ => new Mock<IRbacPermissionResolver>().Object);
        services.AddScoped<IAuthorizationPermissionService, AuthorizationPermissionServiceAdapter>();
        services.SetupAuthorization(configuration, AuthorizationOptionsBuilder.Build(configuration));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuthorizationMiddlewareResultHandler>()
            .Should().BeOfType<AuditingAuthorizationMiddlewareResultHandler>();
        provider.GetRequiredService<IAuthorizationPermissionService>()
            .Should().BeOfType<AuditingAuthorizationPermissionService>();
    }

    [Fact]
    public async Task HandleAsync_RecordsGrantedDecisionContextAndDelegatesOriginalResult()
    {
        var auditService = new Mock<IAuditService>();
        CreateAuditLogRequest? capturedRequest = null;
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => capturedRequest = request)
            .Returns(Task.CompletedTask);
        var inner = new CapturingResultHandler();
        var handler = CreateHandler(auditService.Object, inner);
        var context = CreateContext();
        var policy = CreatePolicy();
        var result = PolicyAuthorizationResult.Success();

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, result);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        capturedRequest.Category.Should().Be(AuditCategory.Permission);
        capturedRequest.ResourceType.Should().Be("teams/{teamId:guid}");
        capturedRequest.ResourceId.Should().Be("846876be-39d9-4497-9733-a66f33692c38");
        capturedRequest.UserId.Should().Be(UserId);
        capturedRequest.TenantId.Should().Be(TenantId);
        capturedRequest.SessionId.Should().Be(SessionId);
        capturedRequest.IpAddress.Should().Be("203.0.113.10");
        capturedRequest.UserAgent.Should().Be("GameGuild.Tests/1.0");
        capturedRequest.Success.Should().BeTrue();
        capturedRequest.CorrelationId.Should().Be("request-correlation-42");

        var metadata = JsonSerializer.Serialize(capturedRequest.Metadata);
        metadata.Should().Contain("\"HttpMethod\":\"GET\"");
        metadata.Should().Contain("\"Policies\":[\"CanReadTeam\"]");
        metadata.Should().Contain("\"Roles\":[\"Administrator\"]");
        metadata.Should().Contain("\"AuthenticationSchemes\":[\"Bearer\"]");
        metadata.Should().Contain("\"Result\":\"Granted\"");

        inner.CallCount.Should().Be(1);
        inner.LastNext.Should().NotBeNull();
        inner.LastContext.Should().BeSameAs(context);
        inner.LastPolicy.Should().BeSameAs(policy);
        inner.LastResult.Should().BeSameAs(result);
    }

    [Fact]
    public async Task HandleAsync_RecordsDeniedRequirementsAndDoesNotChangeDenial()
    {
        var auditService = new Mock<IAuditService>();
        CreateAuditLogRequest? capturedRequest = null;
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => capturedRequest = request)
            .Returns(Task.CompletedTask);
        var inner = new CapturingResultHandler();
        var handler = CreateHandler(auditService.Object, inner);
        var context = CreateContext();
        var policy = CreatePolicy();
        var result = PolicyAuthorizationResult.Forbid(
            AuthorizationFailure.Failed(policy.Requirements));

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, result);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.ActionType.Should().Be(AuditActionTypes.PermissionDenied);
        capturedRequest.Success.Should().BeFalse();
        capturedRequest.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        capturedRequest.RiskLevel.Should().Be(AuditRiskLevel.High);
        capturedRequest.CorrelationId.Should().Be("request-correlation-42");

        var metadata = JsonSerializer.Serialize(capturedRequest.Metadata);
        metadata.Should().Contain("\"FailedRequirements\":[\"RolesAuthorizationRequirement\"]");
        metadata.Should().Contain("\"Result\":\"Forbidden\"");

        inner.CallCount.Should().Be(1);
        inner.LastResult.Should().BeSameAs(result);
    }

    [Fact]
    public async Task HandleAsync_ContinuesAuthorizationWhenAuditPersistenceFails()
    {
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .ThrowsAsync(new InvalidOperationException("Audit storage unavailable"));
        var inner = new CapturingResultHandler();
        var handler = CreateHandler(auditService.Object, inner);
        var context = CreateContext();
        var policy = CreatePolicy();
        var result = PolicyAuthorizationResult.Forbid();

        var act = () => handler.HandleAsync(_ => Task.CompletedTask, context, policy, result);

        await act.Should().NotThrowAsync();
        inner.CallCount.Should().Be(1);
        inner.LastResult.Should().BeSameAs(result);
    }

    [Fact]
    public async Task HandleAsync_UsesEffectiveRemotePeerInsteadOfForwardedHeader()
    {
        var auditService = new Mock<IAuditService>();
        CreateAuditLogRequest? capturedRequest = null;
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => capturedRequest = request)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler(auditService.Object, new CapturingResultHandler());
        var context = CreateContext();
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.200";

        await handler.HandleAsync(_ => Task.CompletedTask, context, CreatePolicy(), PolicyAuthorizationResult.Success());

        capturedRequest.Should().NotBeNull();
        capturedRequest!.IpAddress.Should().Be("203.0.113.10");
    }

    private static AuditingAuthorizationMiddlewareResultHandler CreateHandler(
        IAuditService auditService,
        IAuthorizationMiddlewareResultHandler inner)
        => new(auditService, NullLogger<AuditingAuthorizationMiddlewareResultHandler>.Instance, inner);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "request-correlation-42";
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "GameGuild.Tests/1.0";
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Request.RouteValues["teamId"] = Guid.Parse("846876be-39d9-4497-9733-a66f33692c38");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString("D")),
            new Claim(JwtClaimTypes.TenantId, TenantId.ToString("D")),
            new Claim(JwtClaimTypes.SessionId, SessionId.ToString("D")),
        ],
        "Bearer"));
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("teams/{teamId:guid}"),
            order: 0,
            new EndpointMetadataCollection(new AuthorizeAttribute("CanReadTeam")
            {
                Roles = "Administrator",
                AuthenticationSchemes = "Bearer"
            }),
            displayName: "Teams.Read"));

        return context;
    }

    private static AuthorizationPolicy CreatePolicy()
        => new(
        [new RolesAuthorizationRequirement(["Administrator"])],
        ["Bearer"]);

    private sealed class CapturingResultHandler : IAuthorizationMiddlewareResultHandler
    {
        public int CallCount { get; private set; }

        public RequestDelegate? LastNext { get; private set; }

        public HttpContext? LastContext { get; private set; }

        public AuthorizationPolicy? LastPolicy { get; private set; }

        public PolicyAuthorizationResult? LastResult { get; private set; }

        public Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            CallCount++;
            LastNext = next;
            LastContext = context;
            LastPolicy = policy;
            LastResult = authorizeResult;
            return Task.CompletedTask;
        }
    }
}
