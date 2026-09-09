using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class DurableAdminWithdrawalWorkflowTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Workflow_ReservesApprovesDispatchesAndSettlesWithAnImmutableAuditTrail()
    {
        var run = CreateRun();
        var context = new RecordingContext();
        var operations = new InMemoryAdminWithdrawalStore();
        var audit = new AdminWithdrawalAuditTrail();
        var reservations = new RecordingReservations(run);
        var postings = new RecordingPostings();
        var providerAuthority = new AcceptProviderAuthority();
        var workflow = new PostgreSqlDurableAdminWithdrawalWorkflow(
            context, operations, audit, reservations, new AcceptCapabilityAuthorization(run),
            new AcceptCapabilityResolver(), postings, providerAuthority, new AcceptEvidence(),
            new RecordingDispatchOutbox());

        var reserved = await workflow.ReserveAsync(CreateReservationRequest(run));
        var approved = await workflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
            run.TenantId, run.Id, reserved.Version, Guid.NewGuid(), Time.AddMinutes(1)));
        var dispatching = await workflow.BeginDispatchAsync(CreateDispatchRequest(
            run, approved.Version, occurredAt: Time.AddMinutes(2)));
        var providerEvent = CreateProviderEvent(dispatching, AdminWithdrawalProviderOutcome.Succeeded);
        var terminal = await workflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
            providerEvent));

        terminal.State.Should().Be(AdminWithdrawalRunState.Succeeded);
        terminal.Version.Should().Be(4);
        terminal.ProviderTransferId.Should().Be(providerEvent.ProviderTransferId);
        postings.Requests.Select(request => request.Posting.Template.Kind).Should().Equal(
            PostingTemplateKind.AdminWithdrawalReservation,
            PostingTemplateKind.AdminWithdrawalSuccess);
        reservations.Transitions.Should().Equal(
            new ReservationTransition(
                run.Id,
                PersistedFragmentReservationStatus.Reserved,
                PersistedFragmentReservationStatus.Dispatching,
                Time.AddMinutes(2)),
            new ReservationTransition(
                run.Id,
                PersistedFragmentReservationStatus.Dispatching,
                PersistedFragmentReservationStatus.Consumed,
                providerEvent.ObservedAt));
        audit.Events(run.Id).Select(item => item.Kind).Should().Equal("reserved", "approved", "dispatching", "succeeded");
        audit.Verify(run.Id).Should().BeTrue();
        context.Transactions.Should().HaveCount(4);
        context.Transactions.Should().OnlyContain(transaction => transaction.CommitCalled);
        providerAuthority.Consumptions.Should().ContainSingle()
            .Which.ConsumedAt.Should().Be(providerEvent.ObservedAt);

        var replay = await workflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
            providerEvent));
        replay.Should().BeEquivalentTo(terminal);
        postings.Requests.Should().HaveCount(2);
        reservations.Transitions.Should().HaveCount(2);
        providerAuthority.Consumptions.Should().ContainSingle();
    }

    [Fact]
    public async Task Approval_RejectsRequesterAndProviderEvent_RejectsUnboundOrPrematureEvents()
    {
        var run = CreateRun();
        var context = new RecordingContext();
        var operations = new InMemoryAdminWithdrawalStore();
        operations.Add(run);
        var workflow = new PostgreSqlDurableAdminWithdrawalWorkflow(
            context,
            operations,
            new AdminWithdrawalAuditTrail(),
            new RecordingReservations(run),
            new AcceptCapabilityAuthorization(run),
            new AcceptCapabilityResolver(),
            new RecordingPostings(),
            new AcceptProviderAuthority(),
            new AcceptEvidence(),
            new RecordingDispatchOutbox());

        await FluentActions.Invoking(() => workflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                run.TenantId, run.Id, run.Version, run.RequestedBy, Time.AddMinutes(1))))
            .Should().ThrowAsync<AdminWithdrawalApprovalException>();

        var approved = run with
        {
            ApprovedBy = Guid.NewGuid(),
            State = AdminWithdrawalRunState.Dispatching,
            Version = 2,
            DispatchSnapshotHash = "snapshot",
            UpdatedAt = Time.AddMinutes(1)
        };
        operations.Update(approved, run.Version);
        var badEvent = CreateProviderEvent(approved, AdminWithdrawalProviderOutcome.Failed) with
        {
            DestinationHash = "wrong"
        };
        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                badEvent)))
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public async Task Reserve_ReplaysOrRollsBackWhenConcurrentAndIneligibleRequestsAreDetected()
    {
        var replayRun = CreateRun();
        var replayStore = new InMemoryAdminWithdrawalStore();
        replayStore.Add(replayRun);
        var replayContext = new RecordingContext();
        var replayWorkflow = CreateWorkflow(replayContext, replayStore, replayRun);

        (await replayWorkflow.ReserveAsync(CreateReservationRequest(replayRun))).Should().BeSameAs(replayRun);
        replayContext.Transactions.Should().BeEmpty();

        var concurrentRun = CreateRun();
        var concurrentContext = new RecordingContext();
        var concurrentWorkflow = CreateWorkflow(
            concurrentContext,
            new ReplayOnSecondReservationLookupStore(concurrentRun),
            concurrentRun);
        (await concurrentWorkflow.ReserveAsync(CreateReservationRequest(concurrentRun))).Should().BeSameAs(concurrentRun);
        concurrentContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var occupiedPeriod = CreateRun();
        var overlappingRun = CreateRun() with { TenantId = occupiedPeriod.TenantId };
        var overlapStore = new InMemoryAdminWithdrawalStore();
        overlapStore.Add(occupiedPeriod);
        var overlapContext = new RecordingContext();
        var overlapWorkflow = CreateWorkflow(overlapContext, overlapStore, overlappingRun);
        await FluentActions.Invoking(() => overlapWorkflow.ReserveAsync(CreateReservationRequest(overlappingRun)))
            .Should().ThrowAsync<AdminWithdrawalOverlapException>();
        overlapContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();

        var mismatchedRun = CreateRun();
        var mismatchContext = new RecordingContext();
        var mismatchWorkflow = CreateWorkflow(
            mismatchContext,
            new InMemoryAdminWithdrawalStore(),
            mismatchedRun,
            fragmentUnits: mismatchedRun.Amount.Units - 1);
        await FluentActions.Invoking(() => mismatchWorkflow.ReserveAsync(CreateReservationRequest(mismatchedRun)))
            .Should().ThrowAsync<AdminWithdrawalEligibilityException>();
        mismatchContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Reserve_BindsTheCapabilityReceiptToTheActualFifoRootsAndDurableRunSnapshot()
    {
        var run = CreateRun();
        var context = new RecordingContext();
        var operations = new InMemoryAdminWithdrawalStore();
        var reservations = new RecordingReservations(run);
        var authorization = new AcceptCapabilityAuthorization(run);
        var resolver = new AcceptCapabilityResolver();
        var workflow = new PostgreSqlDurableAdminWithdrawalWorkflow(
            context,
            operations,
            new AdminWithdrawalAuditTrail(),
            reservations,
            authorization,
            resolver,
            new RecordingPostings(),
            new AcceptProviderAuthority(),
            new AcceptEvidence(),
            new RecordingDispatchOutbox());

        await workflow.ReserveAsync(CreateReservationRequest(run));

        var evaluation = authorization.Contexts.Should().ContainSingle().Subject;
        evaluation.TenantId.Should().Be(run.TenantId);
        evaluation.ActorId.Should().Be(run.RequestedBy);
        evaluation.Capability.Should().Be(EconomyValueMovementCapability.AdminWithdrawalExecution);
        evaluation.SourceRootHashes.Should().Equal(Hash(reservations.Fragment.RootSourceStampId.Value.ToString("N")));
        var resolution = resolver.Resolutions.Should().ContainSingle().Subject;
        resolution.CapabilityName.Should().Be("admin-withdrawal-reservation");
        resolution.TemplateKind.Should().Be(PostingTemplateKind.AdminWithdrawalReservation);
        resolution.Receipt.PolicyVersion.Should().Be(run.PolicyVersion.Value);
        resolution.Receipt.ReserveVersion.Should().Be(run.ReserveVersion.Value);
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Reserve_RollsBackWhenTheReceiptOrRegisteredAuthorityDoesNotMatchTheRun()
    {
        var run = CreateRun();
        var mismatchedReceipts = new Func<CapabilityAuthorizationReceipt, CapabilityAuthorizationReceipt>[]
        {
            receipt => receipt with { TenantId = Guid.NewGuid() },
            receipt => receipt with { ActorId = Guid.NewGuid() },
            receipt => receipt with { PolicyVersion = receipt.PolicyVersion + 1 },
            receipt => receipt with { ReserveVersion = receipt.ReserveVersion + 1 }
        };

        foreach (var mutate in mismatchedReceipts)
        {
            var context = new RecordingContext();
            var postings = new RecordingPostings();
            var workflow = CreateWorkflow(
                context,
                new InMemoryAdminWithdrawalStore(),
                run,
                capabilityAuthorization: new AcceptCapabilityAuthorization(
                    run, (evaluation, receipt) => mutate(receipt)),
                postings: postings);

            await FluentActions.Invoking(() => workflow.ReserveAsync(CreateReservationRequest(run)))
                .Should().ThrowAsync<AdminWithdrawalEligibilityException>();
            context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
            postings.Requests.Should().BeEmpty();
        }

        var authorityContext = new RecordingContext();
        var authorityPostings = new RecordingPostings();
        var authorityWorkflow = CreateWorkflow(
            authorityContext,
            new InMemoryAdminWithdrawalStore(),
            run,
            capabilityResolver: new AcceptCapabilityResolver((receipt, _) => new RegisteredPostingAuthority(
                Guid.NewGuid(), receipt.ActorId, Guid.NewGuid(), receipt.RiskDecisionId,
                receipt.OperationFingerprint, receipt.PolicyVersion)),
            postings: authorityPostings);

        await FluentActions.Invoking(() => authorityWorkflow.ReserveAsync(CreateReservationRequest(run)))
            .Should().ThrowAsync<AdminWithdrawalEligibilityException>();
        authorityContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
        authorityPostings.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovalAndDispatch_ReplayExactCommandsAndRejectStaleLifecycleTransitions()
    {
        var approvedRun = CreateRun() with
        {
            State = AdminWithdrawalRunState.Approved,
            Version = 2,
            ApprovedBy = Guid.NewGuid()
        };
        var approvalStore = new InMemoryAdminWithdrawalStore();
        approvalStore.Add(approvedRun);
        var approvalContext = new RecordingContext();
        var approvalWorkflow = CreateWorkflow(approvalContext, approvalStore, approvedRun);
        (await approvalWorkflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
            approvedRun.TenantId, approvedRun.Id, 1, approvedRun.ApprovedBy!.Value, Time.AddMinutes(1)))).Should().BeSameAs(approvedRun);
        approvalContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        await FluentActions.Invoking(() => approvalWorkflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                approvedRun.TenantId, approvedRun.Id, 1, Guid.NewGuid(), Time.AddMinutes(1))))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        var versionStaleRun = CreateRun() with { Version = 2 };
        var versionStaleStore = new InMemoryAdminWithdrawalStore();
        versionStaleStore.Add(versionStaleRun);
        var versionStaleWorkflow = CreateWorkflow(new RecordingContext(), versionStaleStore, versionStaleRun);
        await FluentActions.Invoking(() => versionStaleWorkflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                versionStaleRun.TenantId, versionStaleRun.Id, 1, Guid.NewGuid(), Time.AddMinutes(1))))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();

        var dispatchingRun = approvedRun with
        {
            State = AdminWithdrawalRunState.Dispatching,
            Version = 3,
            DispatchSnapshotHash = "snapshot"
        };
        var dispatchStore = new InMemoryAdminWithdrawalStore();
        dispatchStore.Add(dispatchingRun);
        var dispatchContext = new RecordingContext();
        var dispatchWorkflow = CreateWorkflow(dispatchContext, dispatchStore, dispatchingRun);
        (await dispatchWorkflow.BeginDispatchAsync(CreateDispatchRequest(
            dispatchingRun, 2, occurredAt: Time.AddMinutes(2))))
            .Should().BeSameAs(dispatchingRun);
        dispatchContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var staleRun = CreateRun();
        var staleStore = new InMemoryAdminWithdrawalStore();
        staleStore.Add(staleRun);
        var staleContext = new RecordingContext();
        var staleWorkflow = CreateWorkflow(staleContext, staleStore, staleRun);
        await FluentActions.Invoking(() => staleWorkflow.BeginDispatchAsync(CreateDispatchRequest(
                staleRun, staleRun.Version)))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        staleContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Approval_RejectsAnApprovedReplayWithoutAnApprover()
    {
        var approvedWithoutApprover = CreateRun() with
        {
            State = AdminWithdrawalRunState.Approved,
            Version = 2,
            ApprovedBy = null
        };
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(approvedWithoutApprover);
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, store, approvedWithoutApprover);

        await FluentActions.Invoking(() => workflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                approvedWithoutApprover.TenantId, approvedWithoutApprover.Id, 1, Guid.NewGuid(), Time.AddMinutes(1))))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task BeginDispatch_RejectsEveryStaleOrUnapprovedRunShape()
    {
        var approved = CreateRun() with { State = AdminWithdrawalRunState.Approved, ApprovedBy = Guid.NewGuid() };

        await AssertDispatchStaleAsync(approved with { State = AdminWithdrawalRunState.PendingApproval }, 1, approved.FencingToken, approved.ExecutionEpoch);
        await AssertDispatchStaleAsync(approved with { Version = 2 }, 1, approved.FencingToken, approved.ExecutionEpoch);
        await AssertDispatchStaleAsync(approved with { FencingToken = approved.FencingToken + 1 }, 1, approved.FencingToken, approved.ExecutionEpoch);
        await AssertDispatchStaleAsync(approved with { ExecutionEpoch = approved.ExecutionEpoch + 1 }, 1, approved.FencingToken, approved.ExecutionEpoch);
        await AssertDispatchStaleAsync(approved with { ApprovedBy = null }, 1, approved.FencingToken, approved.ExecutionEpoch);
        await AssertDispatchStaleAsync(approved with { ApprovedBy = approved.RequestedBy }, 1, approved.FencingToken, approved.ExecutionEpoch);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("policy")]
    [InlineData("reserve")]
    [InlineData("provider")]
    [InlineData("destination")]
    [InlineData("roots")]
    public async Task BeginDispatch_RejectsEveryUnboundCapabilityReceiptField(string invalid)
    {
        var approved = CreateRun() with
        {
            State = AdminWithdrawalRunState.Approved,
            ApprovedBy = Guid.NewGuid()
        };
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(approved);
        var context = new RecordingContext();
        var authorization = new AcceptCapabilityAuthorization(approved, (_, receipt) => invalid switch
        {
            "tenant" => receipt with { TenantId = Guid.NewGuid() },
            "actor" => receipt with { ActorId = Guid.NewGuid() },
            "policy" => receipt with { PolicyVersion = receipt.PolicyVersion + 1 },
            "reserve" => receipt with { ReserveVersion = receipt.ReserveVersion + 1 },
            "provider" => receipt with { ProviderHash = "changed-provider" },
            "destination" => receipt with { DestinationHash = "changed-destination" },
            "roots" => receipt with { SourceRootHashes = ["changed-root"] },
            _ => receipt
        });
        var workflow = CreateWorkflow(
            context, store, approved, capabilityAuthorization: authorization);

        await FluentActions.Awaiting(() => workflow.BeginDispatchAsync(
                CreateDispatchRequest(approved)))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task BeginDispatch_RejectsWhenNoReservedFragmentCanTransition()
    {
        var approved = CreateRun() with
        {
            State = AdminWithdrawalRunState.Approved,
            ApprovedBy = Guid.NewGuid()
        };
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(approved);
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, store, approved, transitionCount: 0);

        await FluentActions.Awaiting(() => workflow.BeginDispatchAsync(
                CreateDispatchRequest(approved)))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task BeginDispatch_RejectsEveryInvalidRequestFieldBeforePersistence()
    {
        var run = CreateRun();
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, new InMemoryAdminWithdrawalStore(), run);
        var valid = CreateDispatchRequest(run);
        DurableAdminWithdrawalDispatchRequest?[] invalid =
        [
            null,
            valid with { TenantId = Guid.Empty },
            valid with { RunId = Guid.Empty },
            valid with { DispatchedBy = Guid.Empty },
            valid with { ExpectedVersion = 0 },
            valid with { FencingToken = 0 },
            valid with { ExecutionEpoch = 0 },
            valid with { JurisdictionCode = " " },
            valid with { ReauthenticationEvidenceHash = " " },
            valid with { ReauthenticationEvidenceHash = "short" },
            valid with { ProviderHash = " " },
            valid with { SourceRoots = null! },
            valid with { SourceRoots = [] },
            valid with { SourceRoots = [SourceStampId.New(), SourceStampId.New()] }
        ];
        invalid[^1] = invalid[^1]! with { SourceRoots = [valid.SourceRoots[0], valid.SourceRoots[0]] };

        foreach (var request in invalid)
        {
            await FluentActions.Awaiting(() => workflow.BeginDispatchAsync(request!))
                .Should().ThrowAsync<Exception>();
        }
        context.Transactions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    public async Task ProviderEvent_RejectsPostingAuthorityBoundToAnotherActorOrTenant(string invalid)
    {
        var run = CreateRun() with
        {
            State = AdminWithdrawalRunState.Dispatching,
            Version = 3,
            ApprovedBy = Guid.NewGuid(),
            DispatchSnapshotHash = "snapshot"
        };
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var context = new RecordingContext();
        var providerAuthority = new AcceptProviderAuthority(request => CreateAuthority(
            invalid == "actor" ? Guid.NewGuid() : request.ActorId,
            invalid == "tenant" ? Guid.NewGuid() : request.TenantId));
        var workflow = CreateWorkflow(
            context, store, run, providerAuthority: providerAuthority);

        await FluentActions.Awaiting(() => workflow.ApplyProviderEventAsync(
                new DurableAdminWithdrawalProviderEventRequest(
                    CreateProviderEvent(run, AdminWithdrawalProviderOutcome.Failed))))
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Workflow_RejectsInvalidReservationApprovalDispatchAndProviderEventShapesBeforePersistence()
    {
        var validRun = CreateRun();
        await AssertReserveRejectedAsync(validRun with { Id = Guid.Empty }, typeof(ArgumentException));
        await AssertReserveRejectedAsync(validRun with { TenantId = Guid.Empty }, typeof(ArgumentException));
        await AssertReserveRejectedAsync(validRun with { PeriodStart = validRun.PeriodStart.AddDays(1) }, typeof(ArgumentException));
        await AssertReserveRejectedAsync(validRun with { State = AdminWithdrawalRunState.Approved }, typeof(InvalidOperationException));
        await AssertReserveRejectedAsync(validRun with { Version = 2 }, typeof(InvalidOperationException));
        await AssertReserveRejectedAsync(validRun with { ApprovedBy = Guid.NewGuid() }, typeof(InvalidOperationException));
        await AssertReserveRejectedAsync(validRun with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 500) }, typeof(AdminWithdrawalEligibilityException));
        await AssertReserveRejectedAsync(validRun with { Amount = new CoinAmount(CurrencyCode.HardCoin, 0) }, typeof(AdminWithdrawalEligibilityException));
        await AssertReserveRejectedAsync(validRun with { FencingToken = 0 }, typeof(ArgumentOutOfRangeException));
        await AssertReserveRejectedAsync(validRun with { ExecutionEpoch = 0 }, typeof(ArgumentOutOfRangeException));
        await AssertReserveRejectedAsync(validRun with { ReserveAuthorizationEpoch = 0 }, typeof(ArgumentOutOfRangeException));

        await AssertReservationShapeRejectedAsync(CreateReservationRequest(validRun) with { JurisdictionCode = " " });
        await AssertReservationShapeRejectedAsync(CreateReservationRequest(validRun) with { ReauthenticationEvidenceHash = " " });
        await AssertReservationShapeRejectedAsync(CreateReservationRequest(validRun) with { ReauthenticationEvidenceHash = "short" });
        await AssertReservationShapeRejectedAsync(CreateReservationRequest(validRun) with { ProviderHash = " " });

        var shapeContext = new RecordingContext();
        var shapeWorkflow = CreateWorkflow(shapeContext, new InMemoryAdminWithdrawalStore(), validRun);
        await FluentActions.Invoking(() => shapeWorkflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                Guid.Empty, validRun.Id, 1, Guid.NewGuid(), Time)))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => shapeWorkflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                validRun.TenantId, validRun.Id, 1, Guid.Empty, Time)))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => shapeWorkflow.ApproveAsync(new DurableAdminWithdrawalApprovalRequest(
                validRun.TenantId, validRun.Id, 0, Guid.NewGuid(), Time)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => shapeWorkflow.BeginDispatchAsync(
                CreateDispatchRequest(validRun) with { RunId = Guid.Empty }))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => shapeWorkflow.BeginDispatchAsync(
                CreateDispatchRequest(validRun) with { ExpectedVersion = 0 }))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => shapeWorkflow.BeginDispatchAsync(
                CreateDispatchRequest(validRun) with { FencingToken = 0 }))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => shapeWorkflow.BeginDispatchAsync(
                CreateDispatchRequest(validRun) with { ExecutionEpoch = 0 }))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        var missingRunId = CreateProviderEvent(validRun, AdminWithdrawalProviderOutcome.Failed) with { RunId = Guid.Empty };
        await FluentActions.Invoking(() => shapeWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                missingRunId)))
            .Should().ThrowAsync<ArgumentException>();
        var missingTenant = CreateProviderEvent(validRun, AdminWithdrawalProviderOutcome.Failed) with { TenantId = Guid.Empty };
        await FluentActions.Invoking(() => shapeWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                missingTenant)))
            .Should().ThrowAsync<ArgumentException>();
        var nonTerminalEvent = CreateProviderEvent(validRun, AdminWithdrawalProviderOutcome.Failed) with { Outcome = AdminWithdrawalProviderOutcome.Submitted };
        await FluentActions.Invoking(() => shapeWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                nonTerminalEvent)))
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
        shapeContext.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderEvent_RejectsInvalidEvidenceAndHandlesConcurrentOrStaleTerminalUpdates()
    {
        var dispatchingRun = CreateRun() with
        {
            State = AdminWithdrawalRunState.Dispatching,
            Version = 3,
            ApprovedBy = Guid.NewGuid(),
            DispatchSnapshotHash = "snapshot"
        };
        var rejectContext = new RecordingContext();
        var rejectWorkflow = CreateWorkflow(
            rejectContext,
            new InMemoryAdminWithdrawalStore(),
            dispatchingRun,
            evidence: new RejectEvidence());
        await FluentActions.Invoking(() => rejectWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                CreateProviderEvent(dispatchingRun, AdminWithdrawalProviderOutcome.Failed))))
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
        rejectContext.Transactions.Should().BeEmpty();

        var replayContext = new RecordingContext();
        var replayWorkflow = CreateWorkflow(
            replayContext,
            new ReplayOnSecondProviderEventLookupStore(dispatchingRun),
            dispatchingRun);
        (await replayWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
            CreateProviderEvent(dispatchingRun, AdminWithdrawalProviderOutcome.Failed))))
            .Should().BeSameAs(dispatchingRun);
        replayContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var zeroTransitionStore = new InMemoryAdminWithdrawalStore();
        zeroTransitionStore.Add(dispatchingRun);
        var zeroTransitionContext = new RecordingContext();
        var zeroTransitionWorkflow = CreateWorkflow(
            zeroTransitionContext,
            zeroTransitionStore,
            dispatchingRun,
            transitionCount: 0);
        await FluentActions.Invoking(() => zeroTransitionWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                CreateProviderEvent(dispatchingRun, AdminWithdrawalProviderOutcome.Failed))))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        zeroTransitionContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();

        var unorderedRun = CreateRun() with { State = AdminWithdrawalRunState.Approved, Version = 2, ApprovedBy = Guid.NewGuid() };
        var unorderedStore = new InMemoryAdminWithdrawalStore();
        unorderedStore.Add(unorderedRun);
        var unorderedContext = new RecordingContext();
        var unorderedWorkflow = CreateWorkflow(unorderedContext, unorderedStore, unorderedRun);
        await FluentActions.Invoking(() => unorderedWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                CreateProviderEvent(unorderedRun, AdminWithdrawalProviderOutcome.Failed))))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();

        var predatedStore = new InMemoryAdminWithdrawalStore();
        predatedStore.Add(dispatchingRun);
        var predatedContext = new RecordingContext();
        var predatedWorkflow = CreateWorkflow(predatedContext, predatedStore, dispatchingRun);
        var predated = CreateProviderEvent(dispatchingRun, AdminWithdrawalProviderOutcome.Failed) with { ObservedAt = Time.AddMinutes(-1) };
        await FluentActions.Invoking(() => predatedWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                predated)))
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public async Task ProviderEvent_RejectsEveryBindingMismatchAndPersistsAValidFailure()
    {
        var dispatching = CreateRun() with
        {
            State = AdminWithdrawalRunState.Dispatching,
            Version = 3,
            ApprovedBy = Guid.NewGuid(),
            DispatchSnapshotHash = "snapshot"
        };
        var validFailure = CreateProviderEvent(dispatching, AdminWithdrawalProviderOutcome.Failed);

        await AssertTerminalEvidenceRejectedAsync(dispatching, validFailure with { FencingToken = dispatching.FencingToken + 1 });
        await AssertTerminalEvidenceRejectedAsync(dispatching, validFailure with { ExecutionEpoch = dispatching.ExecutionEpoch + 1 });
        await AssertTerminalEvidenceRejectedAsync(dispatching, validFailure with { Amount = new CoinAmount(CurrencyCode.HardCoin, 1) });
        await AssertTerminalEvidenceRejectedAsync(dispatching, validFailure with { SourceAssetKey = "other-source" });
        await AssertTerminalEvidenceRejectedAsync(dispatching, validFailure with { DestinationHash = "other-destination" });
        await AssertTerminalEvidenceRejectedAsync(
            dispatching with { ProviderTransferId = "expected-transfer" },
            validFailure);

        var failureStore = new InMemoryAdminWithdrawalStore();
        failureStore.Add(dispatching);
        var failureContext = new RecordingContext();
        var failureWorkflow = CreateWorkflow(failureContext, failureStore, dispatching);
        var failed = await failureWorkflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
            validFailure));

        failed.State.Should().Be(AdminWithdrawalRunState.Failed);
        failureContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    private static async Task AssertReserveRejectedAsync(
        AdminWithdrawalRun run,
        Type expectedException)
    {
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, new InMemoryAdminWithdrawalStore(), CreateRun());
        var exception = await FluentActions.Invoking(() => workflow.ReserveAsync(CreateReservationRequest(run)))
            .Should().ThrowAsync<Exception>();
        exception.Which.Should().BeOfType(expectedException);
        context.Transactions.Should().BeEmpty();
    }

    private static async Task AssertReservationShapeRejectedAsync(
        DurableAdminWithdrawalReservationRequest request)
    {
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, new InMemoryAdminWithdrawalStore(), request.Run);
        await FluentActions.Invoking(() => workflow.ReserveAsync(request))
            .Should().ThrowAsync<ArgumentException>();
        context.Transactions.Should().BeEmpty();
    }

    private static async Task AssertDispatchStaleAsync(
        AdminWithdrawalRun run,
        long expectedVersion,
        long fencingToken,
        long executionEpoch)
    {
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, store, run);
        await FluentActions.Invoking(() => workflow.BeginDispatchAsync(CreateDispatchRequest(
                run, expectedVersion, fencingToken, executionEpoch)))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    private static async Task AssertTerminalEvidenceRejectedAsync(
        AdminWithdrawalRun run,
        AdminWithdrawalProviderEvent providerEvent)
    {
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, store, run);
        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurableAdminWithdrawalProviderEventRequest(
                providerEvent)))
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
        context.Transactions.Should().BeEmpty();
    }

    private static PostgreSqlDurableAdminWithdrawalWorkflow CreateWorkflow(
        RecordingContext context,
        IAdminWithdrawalStore operations,
        AdminWithdrawalRun reservationRun,
        long? fragmentUnits = null,
        long transitionCount = 1,
        IAdminWithdrawalProviderEvidenceVerifier? evidence = null,
        IEconomyProtectedOperationOrchestrator? capabilityAuthorization = null,
        IRegisteredPostingCapabilityResolver? capabilityResolver = null,
        IRegisteredPostingGateway? postings = null,
        IProviderEvidencePostingAuthorityIssuer? providerAuthority = null) => new(
        context,
        operations,
        new AdminWithdrawalAuditTrail(),
        new RecordingReservations(reservationRun, fragmentUnits, transitionCount),
        capabilityAuthorization ?? new AcceptCapabilityAuthorization(reservationRun),
        capabilityResolver ?? new AcceptCapabilityResolver(),
        postings ?? new RecordingPostings(),
        providerAuthority ?? new AcceptProviderAuthority(),
        evidence ?? new AcceptEvidence(),
        new RecordingDispatchOutbox());

    private static AdminWithdrawalRun CreateRun() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new IdempotencyKey($"admin-withdrawal-{Guid.NewGuid():N}"),
        "request-hash",
        new DateOnly(2026, 8, 1),
        Guid.NewGuid(),
        null,
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 500),
        "primary-hard-reserve",
        "destination-hash",
        AdminWithdrawalRunState.PendingApproval,
        1,
        7,
        2,
        new ReserveVersion(1),
        1,
        new PolicyVersion(1),
        null,
        null,
        Time,
        Time);

    private static DurableAdminWithdrawalReservationRequest CreateReservationRequest(AdminWithdrawalRun run) => new(
        run,
        "US",
        Hash("reauth-evidence"),
        "stripe-platform");

    private static DurableAdminWithdrawalDispatchRequest CreateDispatchRequest(
        AdminWithdrawalRun run,
        long? expectedVersion = null,
        long? fencingToken = null,
        long? executionEpoch = null,
        DateTimeOffset? occurredAt = null) => new(
        run.TenantId,
        run.Id,
        expectedVersion ?? run.Version,
        fencingToken ?? run.FencingToken,
        executionEpoch ?? run.ExecutionEpoch,
        occurredAt ?? Time,
        run.RequestedBy,
        "US",
        Hash("reauth-evidence"),
        "stripe-platform",
        [SourceStampId.New()]);

    private static AdminWithdrawalProviderEvent CreateProviderEvent(
        AdminWithdrawalRun run,
        AdminWithdrawalProviderOutcome outcome) => new(
        $"evt_{Guid.NewGuid():N}",
        run.Id,
        run.TenantId,
        outcome,
        "transfer-1",
        run.FencingToken,
        run.ExecutionEpoch,
        run.Amount,
        run.SourceAssetKey,
        run.DestinationHash,
        "provider-evidence",
        "signature",
        Time.AddMinutes(3));

    private static RegisteredPostingAuthority CreateAuthority(Guid actorId, Guid tenantId) => new(
        Guid.NewGuid(), actorId, tenantId, Guid.NewGuid(), "economy-admin-withdrawal", 1);

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RecordingContext : IApplicationDbContext
    {
        public List<RecordingTransaction> Transactions { get; } = [];
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = new RecordingTransaction();
            Transactions.Add(transaction);
            return Task.FromResult<IDbContextTransaction>(transaction);
        }
    }

    private sealed class RecordingTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool CommitCalled { get; private set; }
        public bool RollbackCalled { get; private set; }
        public void Commit() => CommitCalled = true;
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalled = true;
            return Task.CompletedTask;
        }
        public void Rollback() => RollbackCalled = true;
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCalled = true;
            return Task.CompletedTask;
        }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingReservations(
        AdminWithdrawalRun run,
        long? fragmentUnits = null,
        long transitionCount = 1) : IFifoFragmentReservationGateway
    {
        public PersistedFragmentReservation Fragment { get; } = new(
            Guid.NewGuid(),
            run.Id,
            CreditLotId.New(),
            SourceStampId.New(),
            0,
            new RootTraceRange(SourceStampId.New(), 0, checked(run.Amount.Units * 1000), 0),
            new CoinAmount(run.Amount.Currency, fragmentUnits ?? run.Amount.Units));
        public List<ReservationTransition> Transitions { get; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request) => [Fragment];
        public long Transition(Guid operationId, PersistedFragmentReservationStatus expected, PersistedFragmentReservationStatus next, DateTimeOffset terminalAt)
        {
            Transitions.Add(new ReservationTransition(operationId, expected, next, terminalAt));
            return transitionCount;
        }
    }

    private sealed class RecordingPostings : IRegisteredPostingGateway
    {
        public List<RegisteredPostingRequest> Requests { get; } = [];
        public RegisteredPostingReceipt Post(RegisteredPostingRequest request)
        {
            Requests.Add(request);
            return new RegisteredPostingReceipt(request.Posting.Id, 1, "journal-hash", false);
        }
    }

    private sealed class AcceptCapabilityAuthorization(
        AdminWithdrawalRun run,
        Func<EconomyCapabilityEvaluationContext, CapabilityAuthorizationReceipt, CapabilityAuthorizationReceipt>? mutate = null)
        : IEconomyProtectedOperationOrchestrator
    {
        public List<EconomyCapabilityEvaluationContext> Contexts { get; } = [];

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actorId = run.RequestedBy;
            var riskDecisionId = Guid.NewGuid();
            var operationFingerprint = Hash($"{intent.TemplateKind}:{intent.IdempotencyKey.Value}");
            var rootHashes = intent.SourceRoots
                .Select(root => Hash(root.Value.ToString("N")))
                .ToArray();
            var context = new EconomyCapabilityEvaluationContext(
                run.TenantId,
                actorId,
                EconomySubjectReference.ForUser(run.TenantId, actorId),
                "US",
                intent.Capability,
                riskDecisionId,
                operationFingerprint,
                intent.ProviderReferenceHash,
                intent.DestinationHash,
                rootHashes,
                intent.RequestedAt);
            Contexts.Add(context);
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(),
                context.TenantId,
                context.ActorId,
                context.SubjectReference,
                context.JurisdictionCode,
                context.Capability,
                context.OperationFingerprint,
                run.PolicyVersion.Value,
                run.ReserveVersion.Value,
                context.RiskDecisionId,
                1,
                context.ProviderHash,
                context.DestinationHash,
                context.SourceRootHashes,
                ["evidence"],
                context.EvaluatedAt,
                context.EvaluatedAt.AddMinutes(5),
                "receipt-hash",
                "test-key",
                "signature");
            receipt = mutate?.Invoke(context, receipt) ?? receipt;
            var authorization = new EconomyProtectedOperationAuthorization(
                receipt.TenantId,
                receipt.ActorId,
                receipt.JurisdictionCode,
                receipt.RiskDecisionId,
                receipt.OperationFingerprint,
                receipt);
            return await operation(authorization, cancellationToken);
        }
    }

    private sealed class AcceptCapabilityResolver(
        Func<CapabilityAuthorizationReceipt, PostingTemplateKind, RegisteredPostingAuthority>? authorityFactory = null)
        : IRegisteredPostingCapabilityResolver
    {
        public List<CapabilityResolution> Resolutions { get; } = [];

        public Task<RegisteredPostingCapability> ResolveAsync(
            string capabilityName,
            PostingTemplateKind templateKind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegisteredPostingCapability(Guid.NewGuid(), capabilityName, templateKind));

        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
            string capabilityName,
            PostingTemplateKind templateKind,
            CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Resolutions.Add(new CapabilityResolution(capabilityName, templateKind, receipt));
            return Task.FromResult(authorityFactory?.Invoke(receipt, templateKind) ?? new RegisteredPostingAuthority(
                Guid.NewGuid(),
                receipt.ActorId,
                receipt.TenantId,
                receipt.RiskDecisionId,
                receipt.OperationFingerprint,
                receipt.PolicyVersion));
        }
    }

    private sealed record CapabilityResolution(
        string CapabilityName,
        PostingTemplateKind TemplateKind,
        CapabilityAuthorizationReceipt Receipt);

    private sealed class RecordingDispatchOutbox : IAdminWithdrawalDispatchOutboxWriter
    {
        public List<AdminWithdrawalDispatchOutboxRow> Rows { get; } = [];

        public Task AddAsync(
            AdminWithdrawalDispatchOutboxRow row,
            CancellationToken cancellationToken = default)
        {
            Rows.Add(row);
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptProviderAuthority(
        Func<ProviderEvidencePostingAuthorityRequest, RegisteredPostingAuthority>? issue = null)
        : IProviderEvidencePostingAuthorityIssuer
    {
        public List<ProviderEvidencePostingAuthorityRequest> Requests { get; } = [];
        public List<(RegisteredPostingAuthority Authority, DateTimeOffset ConsumedAt)> Consumptions { get; } = [];

        public ValueTask<RegisteredPostingAuthority> IssueAsync(
            ProviderEvidencePostingAuthorityRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return ValueTask.FromResult(
                issue?.Invoke(request) ?? CreateAuthority(request.ActorId, request.TenantId));
        }

        public ValueTask ConsumeAsync(
            RegisteredPostingAuthority authority,
            DateTimeOffset consumedAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Consumptions.Add((authority, consumedAt));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AcceptEvidence : IAdminWithdrawalProviderEvidenceVerifier
    {
        public bool Verify(AdminWithdrawalProviderReceipt receipt) => true;
        public bool Verify(AdminWithdrawalProviderEvent providerEvent) => true;
    }

    private sealed class RejectEvidence : IAdminWithdrawalProviderEvidenceVerifier
    {
        public bool Verify(AdminWithdrawalProviderReceipt receipt) => false;
        public bool Verify(AdminWithdrawalProviderEvent providerEvent) => false;
    }

    private sealed class ReplayOnSecondReservationLookupStore(AdminWithdrawalRun run) : IAdminWithdrawalStore
    {
        private int _replayLookups;
        public AdminWithdrawalRun? FindReplay(string key, string requestHash) => ++_replayLookups == 2 ? run : null;
        public AdminWithdrawalRun? FindPeriod(DateOnly periodStart) => throw new NotSupportedException();
        public void Add(AdminWithdrawalRun withdrawalRun) => throw new NotSupportedException();
        public AdminWithdrawalRun Get(Guid runId) => throw new NotSupportedException();
        public AdminWithdrawalRun Update(AdminWithdrawalRun withdrawalRun, long expectedVersion) => throw new NotSupportedException();
        public Guid? FindProviderEvent(string eventId, string eventHash) => throw new NotSupportedException();
        public void RecordProviderEvent(string eventId, string eventHash, AdminWithdrawalRun withdrawalRun, long expectedVersion) => throw new NotSupportedException();
    }

    private sealed class ReplayOnSecondProviderEventLookupStore(AdminWithdrawalRun run) : IAdminWithdrawalStore
    {
        private int _providerEventLookups;
        public AdminWithdrawalRun? FindReplay(string key, string requestHash) => throw new NotSupportedException();
        public AdminWithdrawalRun? FindPeriod(DateOnly periodStart) => throw new NotSupportedException();
        public void Add(AdminWithdrawalRun withdrawalRun) => throw new NotSupportedException();
        public AdminWithdrawalRun Get(Guid runId) => run;
        public AdminWithdrawalRun Update(AdminWithdrawalRun withdrawalRun, long expectedVersion) => throw new NotSupportedException();
        public Guid? FindProviderEvent(string eventId, string eventHash) => ++_providerEventLookups == 2 ? run.Id : null;
        public void RecordProviderEvent(string eventId, string eventHash, AdminWithdrawalRun withdrawalRun, long expectedVersion) => throw new NotSupportedException();
    }

    private sealed record ReservationTransition(
        Guid OperationId,
        PersistedFragmentReservationStatus Expected,
        PersistedFragmentReservationStatus Next,
        DateTimeOffset TerminalAt);
}
