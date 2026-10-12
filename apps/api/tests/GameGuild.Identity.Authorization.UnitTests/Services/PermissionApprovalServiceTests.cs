using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
/// Multi-stage permission approval workflows (issue #188): ordered stage decisions with
/// one audit entry per stage, guarded grant execution only after the final approval,
/// and fail-closed ordering/approver rules.
/// </summary>
public class PermissionApprovalServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _subject = Guid.NewGuid();
    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _approver1 = Guid.NewGuid();
    private readonly Guid _approver2 = Guid.NewGuid();

    private static ActorContext ActorFor(Guid subjectId, Guid? tenantId, bool systemAdmin = false) =>
        ActorContextBuilder.Create()
            .WithActorKind(ActorKind.User)
            .WithSubjectId(subjectId.ToString())
            .WithTenantId(tenantId)
            .WithRoles(systemAdmin ? ["SystemAdmin"] : [])
            .AsAuthenticated()
            .Build();

    private (PermissionApprovalService Service, Mock<IPermissionApprovalRepository> Repository, Mock<IPermissionGrantService> Grants, Mock<IPermissionAuditService> Audit, PermissionApprovalWorkflow Workflow)
        CreateSut(PermissionApprovalStageDefinition[] stages, Guid creator)
    {
        var repository = new Mock<IPermissionApprovalRepository>(MockBehavior.Strict);
        var grants = new Mock<IPermissionGrantService>(MockBehavior.Strict);
        var audit = new Mock<IPermissionAuditService>(MockBehavior.Strict);
        audit.Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());

        var service = new PermissionApprovalService(
            repository.Object,
            grants.Object,
            audit.Object,
            AccessorFor(ActorFor(creator, _tenantId)),
            NullLogger<PermissionApprovalService>.Instance);

        // CreateAsync path
        repository.Setup(r => r.Add(It.IsAny<PermissionApprovalWorkflow>()));
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var workflow = service.CreateAsync(
            _tenantId,
            _subject,
            ["content:manage"],
            stages,
            "onboarding").GetAwaiter().GetResult();

        repository.Invocations.Clear();
        audit.Invocations.Clear();

        return (service, repository, grants, audit, workflow);
    }

    private static IActorContextAccessor AccessorFor(ActorContext actor)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        return accessor.Object;
    }

    private static void SetupLoad(Mock<IPermissionApprovalRepository> repository, PermissionApprovalWorkflow workflow)
    {
        repository.Setup(r => r.GetWithStagesAsync(workflow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);
    }

    [Fact]
    public async Task CreateAsync_RequiresAtLeastOneStage()
    {
        var repository = new Mock<IPermissionApprovalRepository>();
        var service = new PermissionApprovalService(
            repository.Object,
            Mock.Of<IPermissionGrantService>(),
            Mock.Of<IPermissionAuditService>(),
            AccessorFor(ActorFor(_requester, _tenantId)),
            NullLogger<PermissionApprovalService>.Instance);

        var act = () => service.CreateAsync(_tenantId, _subject, ["content:manage"], [], "no stages");
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*At least one approval stage*");
    }

    [Fact]
    public async Task DecideAsync_RejectsOutOfOrderDecisions_FailClosed()
    {
        var (service, repository, grants, audit, workflow) = CreateSut(
        [
            new PermissionApprovalStageDefinition("Line manager"),
            new PermissionApprovalStageDefinition("Security")
        ], _requester);
        SetupLoad(repository, workflow);
        grants.Setup(g => g.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission());

        // Stage 0 approves and advances; a later call decides stage 1 (the current one) —
        // decisions are only accepted for the current stage, in order.
        await service.DecideAsync(workflow.Id, approve: true, "ok");
        repository.Invocations.Clear();

        workflow.CurrentStageIndex.Should().Be(1);

        await service.DecideAsync(workflow.Id, approve: true, "decide stage 1");
        workflow.CurrentStageIndex.Should().Be(2); // final approved => index == count

        grants.Verify(g => g.GrantTenantPermissionAsync(
            It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_EachStageWritesItsOwnAuditEntry()
    {
        var (service, repository, grants, audit, workflow) = CreateSut(
        [
            new PermissionApprovalStageDefinition("Line manager"),
            new PermissionApprovalStageDefinition("Security")
        ], _requester);
        SetupLoad(repository, workflow);
        grants.Setup(g => g.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission());

        await service.DecideAsync(workflow.Id, approve: true, "first");
        await service.DecideAsync(workflow.Id, approve: true, "second");

        audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.ApprovalStage,
            _subject,
            It.IsAny<Guid>(),
            _tenantId,
            It.Is<string>(permissions => permissions.Contains("content:manage", StringComparison.Ordinal)),
            workflow.Id,
            "PermissionApproval",
            It.IsAny<string?>(),
            It.Is<string>(payload => payload.Contains("\"Stage\":\"Line manager\"", StringComparison.Ordinal)),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.ApprovalStage,
            _subject,
            It.IsAny<Guid>(),
            _tenantId,
            It.IsAny<string?>(),
            workflow.Id,
            "PermissionApproval",
            It.IsAny<string?>(),
            It.Is<string>(payload => payload.Contains("\"Stage\":\"Security\"", StringComparison.Ordinal)),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_Rejection_TerminatesWorkflowWithoutGrant()
    {
        var (service, repository, grants, audit, workflow) = CreateSut(
        [
            new PermissionApprovalStageDefinition("Line manager"),
            new PermissionApprovalStageDefinition("Security")
        ], _requester);
        SetupLoad(repository, workflow);

        var result = await service.DecideAsync(workflow.Id, approve: false, "not justified");

        result.WorkflowStatus.Should().Be(PermissionApprovalStatus.Rejected);
        result.GrantApplied.Should().BeFalse();
        workflow.Status.Should().Be(PermissionApprovalStatus.Rejected);
        workflow.Stages.Single(stage => stage.StageIndex == 1).Status.Should().Be(PermissionApprovalStageStatus.Skipped);
        grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecideAsync_FinalApproval_ExecutesGuardedGrantExactlyOnce()
    {
        var (service, repository, grants, audit, workflow) = CreateSut(
        [
            new PermissionApprovalStageDefinition("Line manager"),
            new PermissionApprovalStageDefinition("Security")
        ], _requester);
        SetupLoad(repository, workflow);
        grants.Setup(g => g.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission());

        var first = await service.DecideAsync(workflow.Id, approve: true);
        var final = await service.DecideAsync(workflow.Id, approve: true);

        first.GrantApplied.Should().BeFalse();
        final.GrantApplied.Should().BeTrue();
        final.WorkflowStatus.Should().Be(PermissionApprovalStatus.Approved);

        var expectedPermissions = new[] { "content:manage" };
        grants.Verify(g => g.GrantTenantPermissionAsync(
            _subject,
            _tenantId,
            It.Is<string[]>(permissions => permissions.SequenceEqual(expectedPermissions)),
            It.IsAny<Guid?>(),
            null,
            It.Is<string?>(reason => reason != null && reason.Contains(workflow.Id.ToString(), StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_RequiredApproverIsEnforced_FailClosed()
    {
        var bystander = Guid.NewGuid();
        var (service, repository, grants, audit, workflow) = CreateSut(
        [
            new PermissionApprovalStageDefinition("Security gate", RequiredApproverUserId: null)
        ], _requester);
        SetupLoad(repository, workflow);
        // Pin the current stage to a specific approver after creation.
        workflow.Stages.Single(stage => stage.StageIndex == 0).RequiredApproverUserId = _approver2;

        // The creator (current actor) is not the required approver.
        var act = () => service.DecideAsync(workflow.Id, approve: true);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DecideAsync_CompletedWorkflow_CannotBeDecidedAgain()
    {
        var (service, repository, grants, audit, workflow) = CreateSut(
        [
            new PermissionApprovalStageDefinition("Solo")
        ], _requester);
        SetupLoad(repository, workflow);
        grants.Setup(g => g.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission());

        await service.DecideAsync(workflow.Id, approve: true);

        var replay = () => service.DecideAsync(workflow.Id, approve: true);
        await replay.Should().ThrowAsync<InvalidOperationException>().WithMessage("*pending*");
    }
}
