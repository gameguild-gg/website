using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

/// <summary>
///     Wiring tests for the orchestrated authentication flow endpoints (#233): the
///     commands exposed by <see cref="OrchestratedAuthController" /> must resolve to
///     handlers that delegate to <see cref="IAuthenticationOrchestrationService" /> —
///     previously the orchestrator had no production consumer.
/// </summary>
public class OrchestratedAuthenticationWiringTests
{
    private readonly Mock<IAuthenticationOrchestrationService> _orchestratorMock = new();
    private readonly Mock<IAuthAttemptService> _authAttemptMock = new();
    private readonly Mock<ISender> _senderMock = new();

    private static OrchestrationAuthenticationFlowState FlowState(Guid? flowId = null, bool isComplete = false) => new()
    {
        FlowId = flowId ?? Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CurrentStep = AuthenticationStep.MfaVerification,
        RequiredSteps = [AuthenticationStep.PrimaryCredential, AuthenticationStep.MfaVerification],
        // A complete flow has every required step satisfied — the domain derives
        // NextStep from RequiredSteps.Except(CompletedSteps), so an "isComplete"
        // fixture with a pending step would contradict itself.
        CompletedSteps = isComplete
            ? [AuthenticationStep.PrimaryCredential, AuthenticationStep.MfaVerification]
            : [AuthenticationStep.PrimaryCredential],
        RiskScore = 0.6,
        InitiatedAt = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc),
        ExpiresAt = new DateTime(2026, 10, 11, 0, 15, 0, DateTimeKind.Utc)
    };

    private InitiateOrchestratedAuthenticationHandler CreateInitiateHandler()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "WiringTestAgent/1.0";
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(x => x.HttpContext).Returns(httpContext);
        _authAttemptMock.Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("203.0.113.7");

        return new InitiateOrchestratedAuthenticationHandler(
            _orchestratorMock.Object,
            _authAttemptMock.Object,
            accessor.Object,
            NullLogger<InitiateOrchestratedAuthenticationHandler>.Instance,
            new InitiateOrchestratedAuthenticationCommandValidator());
    }

    // ── Initiate: builds the orchestration request with server-owned context ─

    [Fact]
    public async Task Initiate_DelegatesToOrchestrator_WithLocalCredentialAndServerContext()
    {
        var tenantId = Guid.NewGuid();
        var state = FlowState();
        InitiateAuthenticationRequest? captured = null;
        _orchestratorMock
            .Setup(x => x.InitiateAuthenticationAsync(It.IsAny<InitiateAuthenticationRequest>()))
            .Callback((InitiateAuthenticationRequest request) => captured = request)
            .ReturnsAsync(state);

        var result = await CreateInitiateHandler().Handle(
            new InitiateOrchestratedAuthenticationCommand
            {
                Identifier = "User@Example.com",
                Password = "correct-horse",
                TenantId = tenantId,
                DeviceFingerprint = "device-1"
            },
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Identifier.Should().Be("User@Example.com");
        captured.AuthMethod.Should().Be("Local");
        captured.CredentialType.Should().Be("Email");
        captured.TenantId.Should().Be(tenantId);
        captured.DeviceFingerprint.Should().Be("device-1");
        captured.IpAddress.Should().Be("203.0.113.7");
        captured.UserAgent.Should().Be("WiringTestAgent/1.0");
        captured.Context.Should().ContainKey("password").WhoseValue.Should().Be("correct-horse");

        result.FlowId.Should().Be(state.FlowId);
        result.NextStep.Should().Be(AuthenticationStep.MfaVerification);
        result.RequiredSteps.Should().BeEquivalentTo([AuthenticationStep.PrimaryCredential, AuthenticationStep.MfaVerification]);
        result.CompletedSteps.Should().BeEquivalentTo([AuthenticationStep.PrimaryCredential]);
        result.RiskScore.Should().Be(0.6);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("user@example.com", "")]
    public async Task Initiate_RejectsMissingIdentifierOrPassword(string identifier, string password)
    {
        var handler = CreateInitiateHandler();

        var act = () => handler.Handle(
            new InitiateOrchestratedAuthenticationCommand { Identifier = identifier, Password = password },
            CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
        _orchestratorMock.Verify(x => x.InitiateAuthenticationAsync(It.IsAny<InitiateAuthenticationRequest>()), Times.Never);
    }

    // ── ProcessStep: forwards exactly one proof to the orchestrator ──────────

    [Fact]
    public async Task ProcessStep_DelegatesToOrchestrator_WithTrimmedProofPayload()
    {
        var flowId = Guid.NewGuid();
        var state = FlowState(flowId, isComplete: true);
        object? capturedPayload = null;
        _orchestratorMock
            .Setup(x => x.ProcessAuthenticationStepAsync(flowId, AuthenticationStep.MfaVerification, It.IsAny<object>()))
            .Callback((Guid _, AuthenticationStep _, object payload) => capturedPayload = payload)
            .ReturnsAsync(state);

        var handler = new ProcessOrchestratedAuthenticationStepHandler(
            _orchestratorMock.Object,
            NullLogger<ProcessOrchestratedAuthenticationStepHandler>.Instance,
            new ProcessOrchestratedAuthenticationStepCommandValidator());

        var result = await handler.Handle(
            new ProcessOrchestratedAuthenticationStepCommand { FlowId = flowId, Step = AuthenticationStep.MfaVerification, TotpCode = " 123456 " },
            CancellationToken.None);

        var payload = capturedPayload.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>().Subject;
        payload.Should().HaveCount(1).And.ContainKey("totpCode").WhoseValue.Should().Be("123456");

        result.FlowId.Should().Be(flowId);
        result.IsComplete.Should().BeTrue();
        result.NextStep.Should().BeNull();
    }

    [Fact]
    public async Task ProcessStep_RejectsPrimaryCredentialStepAndMissingProofs()
    {
        var handler = new ProcessOrchestratedAuthenticationStepHandler(
            _orchestratorMock.Object,
            NullLogger<ProcessOrchestratedAuthenticationStepHandler>.Instance,
            new ProcessOrchestratedAuthenticationStepCommandValidator());

        var primaryCredential = () => handler.Handle(
            new ProcessOrchestratedAuthenticationStepCommand { FlowId = Guid.NewGuid(), Step = AuthenticationStep.PrimaryCredential, TotpCode = "123456" },
            CancellationToken.None);
        await primaryCredential.Should().ThrowAsync<RequestValidationException>();

        var noProof = () => handler.Handle(
            new ProcessOrchestratedAuthenticationStepCommand { FlowId = Guid.NewGuid(), Step = AuthenticationStep.MfaVerification },
            CancellationToken.None);
        await noProof.Should().ThrowAsync<RequestValidationException>();

        _orchestratorMock.Verify(x => x.ProcessAuthenticationStepAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationStep>(), It.IsAny<object>()), Times.Never);
    }

    // ── Complete: maps the orchestration result onto the shared response ─────

    [Fact]
    public async Task Complete_DelegatesToOrchestrator_AndMapsIssuedCredentials()
    {
        var flowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _orchestratorMock
            .Setup(x => x.CompleteAuthenticationAsync(flowId))
            .ReturnsAsync(new OrchestrationAuthenticationResult
            {
                IsSuccess = true,
                UserId = userId,
                AccessToken = "orchestrated-access",
                RefreshToken = "orchestrated-refresh",
                TokenExpiresAt = new DateTime(2026, 10, 11, 1, 0, 0, DateTimeKind.Utc),
                SessionId = sessionId,
                MfaEnabled = true,
                DeviceTrusted = true
            });

        var handler = new CompleteOrchestratedAuthenticationHandler(
            _orchestratorMock.Object,
            NullLogger<CompleteOrchestratedAuthenticationHandler>.Instance,
            new CompleteOrchestratedAuthenticationCommandValidator());

        var result = await handler.Handle(
            new CompleteOrchestratedAuthenticationCommand { FlowId = flowId },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("orchestrated-access");
        result.RefreshToken.Should().Be("orchestrated-refresh");
        result.UserId.Should().Be(userId);
        result.SessionId.Should().Be(sessionId);
        result.AccessTokenExpiresAt.Should().Be(new DateTime(2026, 10, 11, 1, 0, 0, DateTimeKind.Utc));
    }

    // ── Abandon / lookup: delegate with fail-closed passthrough ──────────────

    [Fact]
    public async Task Abandon_DelegatesToOrchestrator()
    {
        var flowId = Guid.NewGuid();
        _orchestratorMock
            .Setup(x => x.AbandonAuthenticationFlowAsync(flowId))
            .Returns(Task.CompletedTask);

        var handler = new AbandonOrchestratedAuthenticationHandler(
            _orchestratorMock.Object,
            NullLogger<AbandonOrchestratedAuthenticationHandler>.Instance,
            new AbandonOrchestratedAuthenticationCommandValidator());

        await handler.Handle(new AbandonOrchestratedAuthenticationCommand { FlowId = flowId }, CancellationToken.None);

        _orchestratorMock.Verify(x => x.AbandonAuthenticationFlowAsync(flowId), Times.Once);
    }

    [Fact]
    public async Task GetFlowState_ReturnsNullForUnobservableFlows_AndMapsObservableOnes()
    {
        var flowId = Guid.NewGuid();
        _orchestratorMock
            .Setup(x => x.GetFlowStateAsync(flowId))
            .ReturnsAsync(FlowState(flowId));
        _orchestratorMock
            .Setup(x => x.GetFlowStateAsync(It.Is<Guid>(id => id != flowId)))
            .ReturnsAsync((AuthenticationFlowState?)null);

        var handler = new GetOrchestratedAuthenticationFlowStateHandler(_orchestratorMock.Object);

        var observable = await handler.Handle(new GetOrchestratedAuthenticationFlowStateQuery { FlowId = flowId }, CancellationToken.None);
        observable.Should().NotBeNull();
        observable!.FlowId.Should().Be(flowId);

        var unobservable = await handler.Handle(new GetOrchestratedAuthenticationFlowStateQuery { FlowId = Guid.NewGuid() }, CancellationToken.None);
        unobservable.Should().BeNull();
    }

    // ── Production wiring: commands dispatched through ISender reach the orchestrator ─

    [Fact]
    public async Task OrchestratedCommands_DispatchToOrchestrator_ViaAddCqrsScan()
    {
        _orchestratorMock
            .Setup(x => x.InitiateAuthenticationAsync(It.IsAny<InitiateAuthenticationRequest>()))
            .ReturnsAsync(FlowState());
        _orchestratorMock
            .Setup(x => x.ProcessAuthenticationStepAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationStep>(), It.IsAny<object>()))
            .ReturnsAsync(FlowState());
        _orchestratorMock
            .Setup(x => x.CompleteAuthenticationAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new OrchestrationAuthenticationResult { IsSuccess = true, UserId = Guid.NewGuid() });
        _orchestratorMock
            .Setup(x => x.AbandonAuthenticationFlowAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _authAttemptMock
            .Setup(x => x.GetClientIpAddress(It.IsAny<HttpContext>()))
            .Returns("127.0.0.1");

        var services = new ServiceCollection();
        services.AddCqrs(typeof(OAuthAuthService).Assembly);
        services.AddSingleton(_orchestratorMock.Object);
        services.AddSingleton(_authAttemptMock.Object);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var initiate = await sender.Send(
            new InitiateOrchestratedAuthenticationCommand { Identifier = "user@example.com", Password = "pw" },
            CancellationToken.None);
        initiate.Should().NotBeNull();

        var step = await sender.Send(
            new ProcessOrchestratedAuthenticationStepCommand { FlowId = initiate.FlowId, Step = AuthenticationStep.MfaVerification, TotpCode = "123456" },
            CancellationToken.None);
        step.Should().NotBeNull();

        var complete = await sender.Send(
            new CompleteOrchestratedAuthenticationCommand { FlowId = initiate.FlowId },
            CancellationToken.None);
        complete.Success.Should().BeTrue();

        await sender.Send(new AbandonOrchestratedAuthenticationCommand { FlowId = Guid.NewGuid() }, CancellationToken.None);

        _orchestratorMock.Verify(x => x.InitiateAuthenticationAsync(It.Is<InitiateAuthenticationRequest>(r => r.AuthMethod == "Local")), Times.Once);
        _orchestratorMock.Verify(x => x.ProcessAuthenticationStepAsync(initiate.FlowId, AuthenticationStep.MfaVerification, It.IsAny<object>()), Times.Once);
        _orchestratorMock.Verify(x => x.CompleteAuthenticationAsync(initiate.FlowId), Times.Once);
        _orchestratorMock.Verify(x => x.AbandonAuthenticationFlowAsync(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task OrchestratedAuthController_RoutesCommandsThroughSender()
    {
        var flowId = Guid.NewGuid();
        var state = FlowState(flowId);
        _senderMock
            .Setup(x => x.Send(It.IsAny<InitiateOrchestratedAuthenticationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state.ToDto());
        _senderMock
            .Setup(x => x.Send(It.IsAny<ProcessOrchestratedAuthenticationStepCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state.ToDto());
        _senderMock
            .Setup(x => x.Send(It.IsAny<CompleteOrchestratedAuthenticationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse { Success = true, AccessToken = "token" });
        _senderMock
            .SetupSequence(x => x.Send(It.IsAny<GetOrchestratedAuthenticationFlowStateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state.ToDto())
            .ReturnsAsync((OrchestratedFlowStateResponse?)null);
        _senderMock
            .Setup(x => x.Send(It.IsAny<AbandonOrchestratedAuthenticationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new OrchestratedAuthController(_senderMock.Object);

        var initiate = await controller.Initiate(
            new InitiateOrchestratedAuthenticationRequestDto { Identifier = "user@example.com", Password = "pw" },
            CancellationToken.None);
        initiate.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        var process = await controller.ProcessStep(
            flowId,
            new ProcessOrchestratedAuthenticationStepRequestDto { Step = AuthenticationStep.MfaVerification, TotpCode = "123456" },
            CancellationToken.None);
        process.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        var complete = await controller.Complete(flowId, CancellationToken.None);
        complete.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        var lookup = await controller.GetFlowState(flowId, CancellationToken.None);
        lookup.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        var missing = await controller.GetFlowState(Guid.NewGuid(), CancellationToken.None);
        missing.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();

        var abandon = await controller.Abandon(flowId, CancellationToken.None);
        abandon.Should().BeOfType<Microsoft.AspNetCore.Mvc.NoContentResult>();

        _senderMock.Verify(x => x.Send(It.Is<InitiateOrchestratedAuthenticationCommand>(c => c.Identifier == "user@example.com" && c.Password == "pw"), It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(x => x.Send(It.Is<ProcessOrchestratedAuthenticationStepCommand>(c => c.FlowId == flowId && c.TotpCode == "123456"), It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(x => x.Send(It.Is<CompleteOrchestratedAuthenticationCommand>(c => c.FlowId == flowId), It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(x => x.Send(It.Is<GetOrchestratedAuthenticationFlowStateQuery>(q => q.FlowId == flowId), It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(x => x.Send(It.Is<AbandonOrchestratedAuthenticationCommand>(c => c.FlowId == flowId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
