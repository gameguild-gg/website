using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Economy.Risk;
using GameGuild.Economy.Treasury;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyTreasuryStepUpTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TreasuryRequestsExposeOnlyBusinessIntentAndOpaqueStepUpReceipts()
    {
        typeof(ProposeTreasuryWithdrawalRequest).GetProperties().Select(property => property.Name)
            .Should().Equal(
                nameof(ProposeTreasuryWithdrawalRequest.PeriodStart),
                nameof(ProposeTreasuryWithdrawalRequest.AmountUnits),
                nameof(ProposeTreasuryWithdrawalRequest.DestinationHash),
                nameof(ProposeTreasuryWithdrawalRequest.IdempotencyKey),
                nameof(ProposeTreasuryWithdrawalRequest.StepUpReceipt));
        typeof(DispatchTreasuryWithdrawalRequest).GetProperties().Select(property => property.Name)
            .Should().Equal(
                nameof(DispatchTreasuryWithdrawalRequest.ExpectedVersion),
                nameof(DispatchTreasuryWithdrawalRequest.StepUpReceipt));
    }

    [Fact]
    public async Task ProposalDerivesAuthorityAndConsumesAnOperationBoundReceipt()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var period = new DateOnly(2026, 8, 1);
        const long amount = 500;
        const string destination = "destination-hash";
        const string idempotencyKey = "treasury-august";
        var binding = TreasuryProtectedOperationBinding.Proposal(
            period, amount, destination, idempotencyKey);
        var withdrawals = new Mock<IDurableAdminWithdrawalApplicationService>(MockBehavior.Strict);
        withdrawals.Setup(item => item.ProposeAsync(
                It.Is<ProposeAdminWithdrawalCommand>(command =>
                    command.TenantId == tenantId && command.ActorId == actorId &&
                    command.PeriodStart == period && command.AmountUnits == amount &&
                    command.DestinationHash == destination &&
                    command.IdempotencyKey == idempotencyKey &&
                    command.Reauthentication.TransactionBinding == binding &&
                    command.Reauthentication.EvidenceHash ==
                    TestEconomyStepUpExecutor.EvidenceHash("proposal-receipt")), default))
            .Returns(new ValueTask<AdminWithdrawalRun>((AdminWithdrawalRun)null!));
        var stepUp = new TestEconomyStepUpExecutor();
        var controller = new EconomyTreasuryAdministrationController(
            EconomyHandlerSenders.Funds(
                withdrawals: withdrawals.Object, stepUp: stepUp, timeProvider: new FixedTimeProvider(Now)),
            withdrawals.Object,
            Accessor(tenantId, actorId),
            new FixedTimeProvider(Now));

        var result = await controller.Propose(
            new ProposeTreasuryWithdrawalRequest(
                period, amount, destination, idempotencyKey, "proposal-receipt"), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status201Created);
        stepUp.Calls.Should().ContainSingle().Which.Receipt.Should().Be("proposal-receipt");
        withdrawals.VerifyAll();
    }

    [Fact]
    public async Task ApprovalAndDispatchConsumeSeparateOperationBoundReceipts()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var withdrawals = new Mock<IDurableAdminWithdrawalApplicationService>(MockBehavior.Strict);
        withdrawals.Setup(item => item.ApproveAsync(
                new ApproveAdminWithdrawalCommand(tenantId, actorId, runId, 3), default))
            .ReturnsAsync((AdminWithdrawalRun)null!);
        withdrawals.Setup(item => item.DispatchAsync(
                It.Is<DispatchAdminWithdrawalCommand>(command =>
                    command.TenantId == tenantId && command.ActorId == actorId &&
                    command.RunId == runId && command.ExpectedVersion == 4 &&
                    command.Reauthentication.ActorId == actorId &&
                    command.Reauthentication.Operation == ProtectedOperationKind.AdministrativeAdjustment &&
                    command.Reauthentication.TransactionBinding ==
                    TreasuryProtectedOperationBinding.Dispatch(runId, 4) &&
                    command.Reauthentication.Assurance == ReauthenticationAssurance.MultiFactor &&
                    command.Reauthentication.EvidenceHash ==
                    TestEconomyStepUpExecutor.EvidenceHash("dispatch-receipt")), default))
            .ReturnsAsync((AdminWithdrawalRun)null!);
        var stepUp = new TestEconomyStepUpExecutor();
        var controller = new EconomyTreasuryAdministrationController(
            EconomyHandlerSenders.Funds(
                withdrawals: withdrawals.Object, stepUp: stepUp, timeProvider: new FixedTimeProvider(Now)),
            withdrawals.Object,
            Accessor(tenantId, actorId),
            new FixedTimeProvider(Now));

        (await controller.Approve(runId, new(3, "approval-receipt"), default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Dispatch(
                runId,
                new(4, "dispatch-receipt"),
                default))
            .Should().BeOfType<OkObjectResult>();

        stepUp.Calls.Select(call => (call.Operation.OperationType, call.Receipt)).Should().Equal(
            ("economy.treasury.approve", "approval-receipt"),
            ("economy.treasury.dispatch", "dispatch-receipt"));
        withdrawals.VerifyAll();
    }

    [Theory]
    [InlineData(EconomyProtectedOperationState.Denied, StatusCodes.Status403Forbidden)]
    [InlineData(EconomyProtectedOperationState.ReviewRequired, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.Hold, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.Challenge, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.ComplianceUnavailable, StatusCodes.Status503ServiceUnavailable)]
    public async Task ProtectedOperationFailuresReturnStructuredPublicStates(
        EconomyProtectedOperationState state,
        int expectedStatus)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var withdrawals = new Mock<IDurableAdminWithdrawalApplicationService>(MockBehavior.Strict);
        withdrawals.Setup(item => item.DispatchAsync(
                It.IsAny<DispatchAdminWithdrawalCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EconomyProtectedOperationException(
                state, reviewId, ["safe diagnostic"]));
        var stepUp = new TestEconomyStepUpExecutor();
        var controller = new EconomyTreasuryAdministrationController(
            EconomyHandlerSenders.Funds(
                withdrawals: withdrawals.Object, stepUp: stepUp, timeProvider: new FixedTimeProvider(Now)),
            withdrawals.Object,
            Accessor(tenantId, actorId),
            new FixedTimeProvider(Now));

        var result = await controller.Dispatch(
            runId, new DispatchTreasuryWithdrawalRequest(2, "receipt"), default);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(expectedStatus);
        response.Value.Should().BeEquivalentTo(new TreasuryProtectedOperationFailureResponse(
            state, reviewId, ["safe diagnostic"]));
    }

    private static IActorContextAccessor Accessor(Guid tenantId, Guid actorId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string> { EconomyPermission.Keys.OperateTreasury },
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return accessor.Object;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
