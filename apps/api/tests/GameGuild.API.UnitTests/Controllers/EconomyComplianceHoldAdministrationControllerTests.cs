using FluentAssertions;
using GameGuild.API.Authorization;
using GameGuild.API.Controllers;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyComplianceHoldAdministrationControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("98000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("98000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListUsesActorTenantAndCurrentTime()
    {
        var page = new ComplianceHoldPage([State()], "next");
        var store = new Mock<IComplianceHoldAdministrationStore>(MockBehavior.Strict);
        store.Setup(value => value.ListAsync(
                TenantId,
                true,
                EconomyValueMovementCapability.PayoutExecution,
                25,
                "cursor",
                Now,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        var controller = Controller(store.Object);

        var result = await controller.List(
            true,
            EconomyValueMovementCapability.PayoutExecution,
            25,
            "cursor",
            default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
        store.VerifyAll();
    }

    [Fact]
    public async Task GetAndAuditDoNotLeakAnotherTenant()
    {
        var holdId = Guid.NewGuid();
        var store = new Mock<IComplianceHoldAdministrationStore>(MockBehavior.Strict);
        store.Setup(value => value.CurrentAsync(TenantId, holdId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        store.Setup(value => value.EventsAsync(TenantId, holdId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        var controller = Controller(store.Object);

        (await controller.Get(holdId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.Audit(holdId, default)).Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseMutationConsumesOperationBoundStepUp(bool approve)
    {
        var state = State();
        var store = new Mock<IComplianceHoldAdministrationStore>(MockBehavior.Strict);
        var setup = approve
            ? store.Setup(value => value.ApproveReleaseAsync(
                TenantId,
                state.Hold.Id,
                ActorId,
                "step-up-evidence",
                Now,
                It.IsAny<CancellationToken>()))
            : store.Setup(value => value.ProposeReleaseAsync(
                TenantId,
                state.Hold.Id,
                ActorId,
                "step-up-evidence",
                Now,
                It.IsAny<CancellationToken>()));
        setup.ReturnsAsync(state);
        var executor = new RecordingStepUpExecutor();
        var controller = Controller(store.Object, executor);
        var request = new EconomyStepUpRequest("opaque-receipt");

        var result = approve
            ? await controller.ApproveRelease(state.Hold.Id, request, default)
            : await controller.ProposeRelease(state.Hold.Id, request, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(state);
        executor.Receipt.Should().Be("opaque-receipt");
        executor.Operation.Should().Be(EconomyStepUpOperation.Create(
            $"economy.compliance-hold.release.{(approve ? "approve" : "propose")}",
            $"compliance-hold:{state.Hold.Id:N}",
            state.Hold.Id.ToString("N")));
        store.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReleaseMutationMapsStoreFailures(bool notFound)
    {
        var holdId = Guid.NewGuid();
        var store = new Mock<IComplianceHoldAdministrationStore>(MockBehavior.Strict);
        store.Setup(value => value.ProposeReleaseAsync(
                TenantId,
                holdId,
                ActorId,
                "step-up-evidence",
                Now,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFound
                ? new KeyNotFoundException()
                : new InvalidOperationException("blocked"));
        var controller = Controller(store.Object, new RecordingStepUpExecutor());

        var result = await controller.ProposeRelease(
            holdId,
            new EconomyStepUpRequest("receipt"),
            default);

        if (notFound)
            result.Should().BeOfType<NotFoundResult>();
        else
            result.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be("blocked");
    }

    [Fact]
    public async Task AllOperationsForbidActorsWithoutCompliancePermission()
    {
        var store = new Mock<IComplianceHoldAdministrationStore>(MockBehavior.Strict);
        var accessor = Actor([]);
        var controller = new EconomyComplianceHoldAdministrationController(
            EconomyHandlerSenders.Compliance(holds: store.Object, stepUp: new RecordingStepUpExecutor()),
            store.Object,
            accessor,
            new FixedTimeProvider(Now));
        var holdId = Guid.NewGuid();

        (await controller.List(null, null, 10, null, default)).Should().BeOfType<ForbidResult>();
        (await controller.Get(holdId, default)).Should().BeOfType<ForbidResult>();
        (await controller.Audit(holdId, default)).Should().BeOfType<ForbidResult>();
        (await controller.ProposeRelease(
            holdId,
            new EconomyStepUpRequest("receipt"),
            default)).Should().BeOfType<ForbidResult>();
    }

    private static EconomyComplianceHoldAdministrationController Controller(
        IComplianceHoldAdministrationStore store,
        IEconomyStepUpExecutor? executor = null) =>
        new(
            EconomyHandlerSenders.Compliance(
                holds: store,
                stepUp: executor ?? new RecordingStepUpExecutor()),
            store,
            Actor([EconomyPermission.Keys.OperateCompliance]),
            new FixedTimeProvider(Now));

    private static ActorContextAccessor Actor(IReadOnlyCollection<string> permissions)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = ActorId.ToString(),
            TenantId = TenantId,
            Roles = new HashSet<string>(),
            Permissions = permissions.ToHashSet(),
            IsAuthenticated = true
        });
        return accessor;
    }

    private static ComplianceHoldAdministrationState State()
    {
        var hold = new ComplianceHold(
            Guid.NewGuid(),
            new ComplianceHoldScope(TenantId, "subject-hash", null),
            "case-hash",
            "reason",
            "evidence",
            Guid.NewGuid(),
            Now.AddMinutes(-5),
            Now.AddHours(1),
            null,
            null);
        return new ComplianceHoldAdministrationState(hold, null, null, null, null, []);
    }

    private sealed class RecordingStepUpExecutor : IEconomyStepUpExecutor
    {
        public EconomyStepUpOperation? Operation { get; private set; }
        public string? Receipt { get; private set; }

        public Task<TResult> ExecuteAsync<TResult>(
            EconomyStepUpOperation operation,
            string receipt,
            Func<string, CancellationToken, Task<TResult>> protectedAction,
            CancellationToken cancellationToken)
        {
            Operation = operation;
            Receipt = receipt;
            return protectedAction("step-up-evidence", cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
