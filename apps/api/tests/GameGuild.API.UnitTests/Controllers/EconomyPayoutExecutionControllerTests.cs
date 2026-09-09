using System.Text;
using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyPayoutExecutionControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProtectedExecutionRequestsExposeOnlyServerVerifiableBusinessIntent()
    {
        typeof(ReserveApprovedPayoutExecutionRequest).GetProperties().Select(property => property.Name)
            .Should().Equal(nameof(ReserveApprovedPayoutExecutionRequest.StepUpReceipt));
        typeof(DispatchPayoutExecutionRequest).GetProperties().Select(property => property.Name)
            .Should().Equal(
                nameof(DispatchPayoutExecutionRequest.ExpectedVersion),
                nameof(DispatchPayoutExecutionRequest.StepUpReceipt));
    }

    [Fact]
    public async Task AccountEndpointsUseOnlyTheAuthenticatedTenantAndPayee()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var actor = Actor(tenantId, actorId);
        var account = Account(actorId);
        var onboarding = new ConnectOnboardingResult(account, new Uri("https://connect.example/onboard"));
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        payouts.Setup(service => service.CreateOrRefreshAccountAsync(tenantId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(onboarding);
        payouts.Setup(service => service.GetAccountAsync(tenantId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var controller = new EconomyPayoutAccountController(
            EconomyHandlerSenders.Funds(payouts: payouts.Object), payouts.Object, Accessor(actor));

        var onboardingResult = await controller.CreateOrRefreshOnboarding(CancellationToken.None);
        var accountResult = await controller.GetAccount(CancellationToken.None);

        onboardingResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(onboarding);
        accountResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(account);
        payouts.VerifyAll();
    }

    [Fact]
    public async Task AccountEndpointsFailClosedForMissingContextProviderOrConfiguration()
    {
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        var anonymous = new EconomyPayoutAccountController(
            EconomyHandlerSenders.Funds(payouts: payouts.Object), payouts.Object, Accessor(ActorContext.Anonymous));
        (await anonymous.CreateOrRefreshOnboarding(CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await anonymous.GetAccount(CancellationToken.None)).Should().BeOfType<ForbidResult>();

        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        payouts.Setup(service => service.CreateOrRefreshAccountAsync(tenantId, actorId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutExecutionDisabledException("provider disabled"));
        payouts.Setup(service => service.GetAccountAsync(tenantId, actorId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutEligibilityException("account missing"));
        var controller = new EconomyPayoutAccountController(
            EconomyHandlerSenders.Funds(payouts: payouts.Object),
            payouts.Object,
            Accessor(Actor(tenantId, actorId)));

        (await controller.CreateOrRefreshOnboarding(CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        (await controller.GetAccount(CancellationToken.None)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ReserveDerivesTenantActorAndConsumesAnOperationBoundStepUpReceipt()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var operation = Operation(tenantId, actorId);
        var transactionBinding = PayoutProtectedOperationBinding.Reservation(requestId);
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        payouts.Setup(service => service.ReserveApprovedAsync(
                It.Is<ReserveApprovedPayoutCommand>(command =>
                    command.TenantId == tenantId && command.ActorId == actorId &&
                    command.RequestId == requestId &&
                    command.Reauthentication.ActorId == actorId &&
                    command.Reauthentication.TransactionBinding == transactionBinding &&
                    command.Reauthentication.Assurance == GameGuild.Finance.Economy.Risk.ReauthenticationAssurance.MultiFactor &&
                    command.Reauthentication.EvidenceHash ==
                    TestEconomyStepUpExecutor.EvidenceHash("reserve-receipt")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);
        var stepUp = new TestEconomyStepUpExecutor();
        var controller = AdminController(payouts.Object, Actor(tenantId, actorId), stepUp);

        var result = await controller.Reserve(
            requestId,
            new ReserveApprovedPayoutExecutionRequest("reserve-receipt"),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().Be(EconomyPayoutExecutionOperationDto.From(operation));
        stepUp.Calls.Should().ContainSingle().Which.Should().Match<(GameGuild.API.Authorization.EconomyStepUpOperation Operation, string Receipt)>(
            call => call.Operation.OperationType == "economy.payout.reserve" &&
                    call.Receipt == "reserve-receipt");
        payouts.VerifyAll();
    }

    [Fact]
    public async Task DispatchReconcileAndReadsRemainTenantScoped()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var operation = Operation(tenantId, actorId);
        var dispatching = operation.Transition(
            PayoutOperationState.Dispatching, Now.AddMinutes(1), "snapshot");
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        payouts.Setup(service => service.DispatchAsync(
                It.Is<DispatchPayoutOperationCommand>(command =>
                    command.TenantId == tenantId && command.ActorId == actorId &&
                    command.OperationId == operation.Id && command.ExpectedVersion == operation.Version &&
                    command.Reauthentication.TransactionBinding ==
                    PayoutProtectedOperationBinding.Dispatch(operation.Id, operation.Version)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispatching);
        payouts.Setup(service => service.ReconcileAsync(
                new ReconcilePayoutOperationCommand(tenantId, actorId, operation.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispatching);
        payouts.Setup(service => service.List(tenantId, 25)).Returns([operation]);
        payouts.Setup(service => service.Get(tenantId, operation.Id)).Returns(operation);
        var controller = AdminController(payouts.Object, Actor(tenantId, actorId));

        (await controller.Dispatch(
                operation.Id,
                new DispatchPayoutExecutionRequest(operation.Version, "dispatch-receipt"),
                CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Reconcile(operation.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        controller.List(25).Should().BeOfType<OkObjectResult>();
        controller.Get(operation.Id).Should().BeOfType<OkObjectResult>();
        payouts.VerifyAll();
    }

    [Fact]
    public async Task AdministrativeMovementRequiresPermissionAndAValidStepUpReceipt()
    {
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var request = new ReserveApprovedPayoutExecutionRequest("receipt");
        var noPermission = AdminController(payouts.Object, Actor(tenantId, actorId, permission: false));
        (await noPermission.Reserve(Guid.NewGuid(), request, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();

        var rejectingStepUp = new TestEconomyStepUpExecutor
        {
            Failure = new GameGuild.Identity.Authentication.StepUpReceiptInvalidException("invalid receipt")
        };
        var controller = AdminController(payouts.Object, Actor(tenantId, actorId), rejectingStepUp);
        (await controller.Reserve(Guid.NewGuid(), request, CancellationToken.None))
            .Should().BeOfType<ConflictObjectResult>();
        payouts.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(EconomyProtectedOperationState.Denied, StatusCodes.Status403Forbidden)]
    [InlineData(EconomyProtectedOperationState.ReviewRequired, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.Hold, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.ComplianceUnavailable, StatusCodes.Status503ServiceUnavailable)]
    public async Task AdministrativeMovementMapsProtectedOperationStateWithoutLeakingEvidence(
        EconomyProtectedOperationState state,
        int expectedStatus)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        payouts.Setup(service => service.ReserveApprovedAsync(
                It.IsAny<ReserveApprovedPayoutCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EconomyProtectedOperationException(
                state, reviewId, ["safe diagnostic"]));
        var controller = AdminController(payouts.Object, Actor(tenantId, actorId));

        var result = await controller.Reserve(
            Guid.NewGuid(), new ReserveApprovedPayoutExecutionRequest("receipt"), default);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(expectedStatus);
        response.Value.Should().BeEquivalentTo(new PayoutProtectedOperationFailureResponse(
            state, reviewId, ["safe diagnostic"]));
    }

    [Fact]
    public async Task AdministrationMapsStaleMissingAndInvalidReadRequestsWithoutLeakingAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        payouts.Setup(service => service.ReconcileAsync(
                It.IsAny<ReconcilePayoutOperationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutStaleCommandException("stale"));
        payouts.Setup(service => service.Get(tenantId, operationId)).Throws<KeyNotFoundException>();
        var controller = AdminController(payouts.Object, Actor(tenantId, actorId));

        (await controller.Reconcile(operationId, CancellationToken.None))
            .Should().BeOfType<ConflictObjectResult>();
        controller.Get(operationId).Should().BeOfType<NotFoundResult>();
        controller.List(0).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task StripeWebhookRequiresTheSignatureAndPersistsTheNormalizedEventBeforeAcknowledging()
    {
        var operation = Operation(Guid.NewGuid(), Guid.NewGuid())
            .Transition(PayoutOperationState.Dispatching, Now, "snapshot");
        var providerEvent = new PayoutProviderEvent(
            "evt_1", operation.Id, PayoutProviderOutcome.Succeeded, "po_1",
            operation.ProviderAccountId, operation.DestinationHash, "evidence", "signature", Now);
        var normalizer = new Mock<IStripeConnectWebhookNormalizer>(MockBehavior.Strict);
        normalizer.Setup(service => service.NormalizeAsync(
                It.Is<ReadOnlyMemory<byte>>(payload => Encoding.UTF8.GetString(payload.ToArray()) == "payload"),
                "t=1,v1=signature", Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerEvent);
        var payouts = new Mock<IDurablePayoutApplicationService>(MockBehavior.Strict);
        payouts.Setup(service => service.ApplyProviderEventAsync(providerEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);
        var controller = WebhookController(normalizer.Object, payouts.Object, signature: "t=1,v1=signature");

        var result = await controller.Ingest(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        normalizer.VerifyAll();
        payouts.VerifyAll();

        controller = WebhookController(
            Mock.Of<IStripeConnectWebhookNormalizer>(), Mock.Of<IDurablePayoutApplicationService>(), signature: null);
        (await controller.Ingest(CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }

    private static EconomyPayoutExecutionAdministrationController AdminController(
        IDurablePayoutApplicationService payouts,
        ActorContext actor,
        TestEconomyStepUpExecutor? stepUp = null)
    {
        var resolvedStepUp = stepUp ?? new TestEconomyStepUpExecutor();
        return new EconomyPayoutExecutionAdministrationController(
            EconomyHandlerSenders.Funds(
                payouts: payouts, stepUp: resolvedStepUp, timeProvider: new FixedTimeProvider(Now)),
            payouts,
            Accessor(actor),
            new FixedTimeProvider(Now));
    }

    private static EconomyStripeConnectWebhookController WebhookController(
        IStripeConnectWebhookNormalizer normalizer,
        IDurablePayoutApplicationService payouts,
        string? signature)
    {
        var controller = new EconomyStripeConnectWebhookController(
            EconomyHandlerSenders.Funds(payouts: payouts, timeProvider: new FixedTimeProvider(Now)),
            normalizer,
            new FixedTimeProvider(Now));
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("payload"));
        if (signature is not null) context.Request.Headers["Stripe-Signature"] = signature;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static IActorContextAccessor Accessor(ActorContext actor)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actor);
        return accessor.Object;
    }

    private static ActorContext Actor(
        Guid tenantId,
        Guid actorId,
        bool permission = true,
        ActorAttributes? attributes = null) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = actorId.ToString(),
        TenantId = tenantId,
        Roles = new HashSet<string>(),
        Permissions = permission ? new HashSet<string> { EconomyPermission.Keys.OperatePayouts } : [],
        TypedAttributes = attributes ?? Attributes(),
        IsAuthenticated = true
    };

    private static ActorAttributes Attributes() => new()
    {
        MfaVerified = true,
        SessionId = "session-1",
        TokenId = "token-1",
        AuthenticatedAt = Now.AddMinutes(-1),
        TokenExpiresAt = Now.AddMinutes(10)
    };

    private static ConnectAccountSnapshot Account(Guid payeeId) => new(
        payeeId, "acct_1", "destination-hash", ConnectAccountState.Ready,
        true, true, 1, Now.AddMinutes(-1), Now.AddMinutes(10), "evidence-hash");

    private static PayoutOperation Operation(Guid tenantId, Guid actorId) => new(
        Guid.NewGuid(),
        new IdempotencyKey("payout-execution"),
        "request-hash",
        actorId,
        Guid.NewGuid(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 1_000),
        "acct_1",
        "destination-hash",
        "provider-binding-hash",
        "eligibility-hash",
        null,
        null,
        PayoutOperationState.Reserved,
        1,
        1,
        0,
        new ReserveVersion(1),
        1,
        new PolicyVersion(1),
        Guid.NewGuid(),
        Now.AddMinutes(-2),
        Now.AddMinutes(-2),
        tenantId);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
