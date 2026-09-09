using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Projections;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyControlPlaneStepUpTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CriticalApprovalsConsumeOperationBoundReceiptsAndPersistOnlyEvidenceHashes()
    {
        var actorId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var killSwitchId = Guid.NewGuid();
        var reserveId = Guid.NewGuid();
        const long generation = 17;
        var policies = new Mock<IEconomyCapabilityPolicyStore>(MockBehavior.Strict);
        policies.Setup(item => item.ApproveAsync(
                policyId, actorId, TestEconomyStepUpExecutor.EvidenceHash("policy-receipt"), Now, default))
            .ReturnsAsync((EconomyCapabilityPolicy)null!);
        var killSwitches = new Mock<IEconomyKillSwitchStore>(MockBehavior.Strict);
        killSwitches.Setup(item => item.ProposeReleaseAsync(
                killSwitchId, actorId, TestEconomyStepUpExecutor.EvidenceHash("proposal-receipt"), Now, default))
            .ReturnsAsync((EconomyKillSwitchState)null!);
        killSwitches.Setup(item => item.ApproveReleaseAsync(
                killSwitchId, actorId, TestEconomyStepUpExecutor.EvidenceHash("approval-receipt"), Now, default))
            .ReturnsAsync((EconomyKillSwitchState)null!);
        var projections = new Mock<IEconomyProjectionGenerationService>(MockBehavior.Strict);
        projections.Setup(item => item.ApproveAndTryActivateAsync(
                generation, actorId, TestEconomyStepUpExecutor.EvidenceHash("projection-receipt"), Now, default))
            .ReturnsAsync((ProjectionGenerationState)null!);
        var reserves = new Mock<IEconomyReserveCustodyControlPlane>(MockBehavior.Strict);
        reserves.Setup(item => item.ApproveAndActivateAsync(
                reserveId, actorId, TestEconomyStepUpExecutor.EvidenceHash("reserve-receipt"), Now, default))
            .ReturnsAsync((ReserveHead)null!);
        var stepUp = new TestEconomyStepUpExecutor();
        var controller = CreateController(
            actorId, policies.Object, killSwitches.Object, projections.Object, reserves.Object, stepUp);

        (await controller.ApprovePolicy(policyId, new("policy-receipt"), default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.ProposeKillSwitchRelease(killSwitchId, new("proposal-receipt"), default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.ApproveKillSwitchRelease(killSwitchId, new("approval-receipt"), default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.ApproveProjection(generation, new("projection-receipt"), default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.ApproveReserve(reserveId, new("reserve-receipt"), default))
            .Should().BeOfType<OkObjectResult>();

        stepUp.Calls.Select(call => (call.Operation.OperationType, call.Receipt)).Should().Equal(
            ("economy.policy.approve", "policy-receipt"),
            ("economy.kill-switch.release.propose", "proposal-receipt"),
            ("economy.kill-switch.release.approve", "approval-receipt"),
            ("economy.projection.approve", "projection-receipt"),
            ("economy.reserve.approve", "reserve-receipt"));
        policies.VerifyAll();
        killSwitches.VerifyAll();
        projections.VerifyAll();
        reserves.VerifyAll();
    }

    private static EconomyControlPlaneAdministrationController CreateController(
        Guid actorId,
        IEconomyCapabilityPolicyStore policies,
        IEconomyKillSwitchStore killSwitches,
        IEconomyProjectionGenerationService projections,
        IEconomyReserveCustodyControlPlane reserves,
        TestEconomyStepUpExecutor stepUp)
    {
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>
            {
                EconomyPermission.Keys.ManagePolicies,
                EconomyPermission.Keys.ManageKillSwitches,
                EconomyPermission.Keys.OperateLedger,
                EconomyPermission.Keys.ManageReserves
            },
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyControlPlaneAdministrationController(
            EconomyHandlerSenders.ControlPlane(
                policies: policies,
                killSwitches: killSwitches,
                projections: projections,
                reserves: reserves,
                stepUp: stepUp,
                timeProvider: new FixedTimeProvider()),
            Mock.Of<IEconomyCapabilityReadinessInspector>(),
            Mock.Of<IEconomyOperationsReader>(),
            reserves,
            actor.Object,
            new FixedTimeProvider());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
