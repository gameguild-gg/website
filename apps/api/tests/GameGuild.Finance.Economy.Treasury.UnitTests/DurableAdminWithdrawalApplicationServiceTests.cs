using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class DurableAdminWithdrawalApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProtectedOperationBindingRejectsInvalidProposalAndDispatchCoordinates()
    {
        FluentActions.Invoking(() => TreasuryProtectedOperationBinding.Proposal(
                new DateOnly(2026, 8, 2), 1, "destination", "key"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => TreasuryProtectedOperationBinding.Proposal(
                new DateOnly(2026, 8, 1), 0, "destination", "key"))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => TreasuryProtectedOperationBinding.Dispatch(Guid.Empty, 1))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => TreasuryProtectedOperationBinding.Dispatch(Guid.NewGuid(), 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ProposeAsync_DerivesEveryAuthoritativeFieldFromSignedControlPlaneState()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var walletId = WalletId.New();
        var policy = CreatePolicy(tenantId, walletId, 3);
        var workflow = new RecordingWorkflow();
        var service = CreateService(
            new InMemoryAdminWithdrawalStore(),
            new AdminWithdrawalAuditTrail(),
            policy,
            CreateReserve(4),
            walletId,
            workflow: workflow,
            fencingToken: 91);
        var command = CreateProposalCommand(
            tenantId, actorId, new DateOnly(2026, 8, 1), 500,
            "DESTINATION-HASH", "treasury-august");

        var run = await service.ProposeAsync(command);

        run.TenantId.Should().Be(tenantId);
        run.RequestedBy.Should().Be(actorId);
        run.PlatformFeeWalletId.Should().Be(walletId);
        run.SourceAssetKey.Should().Be("stripe:platform:cash");
        run.DestinationHash.Should().Be("destination-hash");
        run.PolicyVersion.Value.Should().Be(3);
        run.ReserveVersion.Value.Should().Be(4);
        run.ReserveAuthorizationEpoch.Should().Be(7);
        run.FencingToken.Should().Be(91);
        run.ExecutionEpoch.Should().Be(91);
        workflow.Reservations.Should().ContainSingle();
        workflow.Reservations.Single().ProviderHash.Should().Be("stripe-platform-provider");
        workflow.Reservations.Single().JurisdictionCode.Should().Be("US");
        workflow.Reservations.Single().ReauthenticationEvidenceHash.Should().Be(Hash("reauth-evidence"));
    }

    [Fact]
    public async Task ProposeAsync_ReturnsAnExactReplayBeforeReadingMutableControlPlaneState()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var command = CreateProposalCommand(
            tenantId, actorId, new DateOnly(2026, 8, 1), 500,
            "destination-hash", "replay-key");
        var walletId = WalletId.New();
        var policy = CreatePolicy(tenantId, walletId, 3);
        var store = new InMemoryAdminWithdrawalStore();
        var recording = new RecordingWorkflow();
        var initial = CreateService(
            store, new AdminWithdrawalAuditTrail(), policy, CreateReserve(4), walletId,
            workflow: recording);
        var existing = await initial.ProposeAsync(command);
        store.Add(existing);
        var unavailablePolicies = new RecordingPolicyStore(null);
        var replayService = CreateService(
            store, new AdminWithdrawalAuditTrail(), policy, CreateReserve(4), walletId,
            policyStore: unavailablePolicies);

        var replay = await replayService.ProposeAsync(command);

        replay.Should().BeSameAs(existing);
        unavailablePolicies.CurrentCalls.Should().Be(0);
    }

    [Fact]
    public async Task DispatchAsync_RevalidatesPolicyReserveAndExactPostgreSqlReservationRoots()
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var run = CreateRun(tenantId, walletId) with
        {
            State = AdminWithdrawalRunState.Approved,
            ApprovedBy = Guid.NewGuid(),
            Version = 2
        };
        var root = SourceStampId.New();
        var rootHash = Hash(root.Value.ToString("N"));
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var audit = new AdminWithdrawalAuditTrail();
        audit.Append(run.Id, "reserved", run.RequestedBy, JsonSerializer.Serialize(
            CreateAuthorizationSnapshot(run, rootHash)), run.CreatedAt);
        var workflow = new RecordingWorkflow();
        var service = CreateService(
            store,
            audit,
            CreatePolicy(tenantId, walletId, run.PolicyVersion.Value),
            CreateReserve(run.ReserveVersion.Value),
            walletId,
            workflow,
            reservations: [CreateFragment(run, root)]);
        var dispatchActor = Guid.NewGuid();

        var dispatching = await service.DispatchAsync(CreateDispatchCommand(
            tenantId, dispatchActor, run.Id, run.Version));

        dispatching.State.Should().Be(AdminWithdrawalRunState.Dispatching);
        var request = workflow.Dispatches.Should().ContainSingle().Subject;
        request.DispatchedBy.Should().Be(dispatchActor);
        request.ProviderHash.Should().Be("stripe-platform-provider");
        request.SourceRoots.Should().Equal(root);
        request.ReauthenticationEvidenceHash.Should().Be(Hash("reauth-evidence"));
    }

    [Fact]
    public async Task DispatchAsync_FailsClosedWhenReserveOrSourceProvenanceChanged()
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var run = CreateRun(tenantId, walletId) with
        {
            State = AdminWithdrawalRunState.Approved,
            ApprovedBy = Guid.NewGuid(),
            Version = 2
        };
        var root = SourceStampId.New();
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var audit = new AdminWithdrawalAuditTrail();
        audit.Append(run.Id, "reserved", run.RequestedBy, JsonSerializer.Serialize(
            CreateAuthorizationSnapshot(run, "different-root")), run.CreatedAt);
        var service = CreateService(
            store,
            audit,
            CreatePolicy(tenantId, walletId, run.PolicyVersion.Value),
            CreateReserve(run.ReserveVersion.Value),
            walletId,
            reservations: [CreateFragment(run, root)]);
        var dispatchActor = Guid.NewGuid();
        var command = CreateDispatchCommand(tenantId, dispatchActor, run.Id, run.Version);

        await FluentActions.Invoking(() => service.DispatchAsync(command))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>()
            .WithMessage("*provenance*");

        var staleReserve = CreateService(
            store,
            audit,
            CreatePolicy(tenantId, walletId, run.PolicyVersion.Value),
            CreateReserve(run.ReserveVersion.Value + 1),
            walletId,
            reservations: [CreateFragment(run, root)]);
        await FluentActions.Invoking(() => staleReserve.DispatchAsync(command))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>()
            .WithMessage("*reserve snapshot changed*");
    }

    [Fact]
    public async Task ApproveReconcileReadAndAudit_AreTenantScoped()
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var run = CreateRun(tenantId, walletId);
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var audit = new AdminWithdrawalAuditTrail();
        audit.Append(run.Id, "reserved", run.RequestedBy, "evidence", run.CreatedAt);
        var workflow = new RecordingWorkflow();
        var provider = new RecordingProvider(CreateProviderEvent(run));
        var service = CreateService(
            store, audit, CreatePolicy(tenantId, walletId, 3), CreateReserve(4), walletId,
            workflow, provider: provider);

        var approved = await service.ApproveAsync(new ApproveAdminWithdrawalCommand(
            tenantId, Guid.NewGuid(), run.Id, run.Version));
        approved.State.Should().Be(AdminWithdrawalRunState.Approved);
        service.Get(tenantId, run.Id).Should().Be(run);
        service.List(tenantId).Should().ContainSingle();
        var auditView = service.Audit(tenantId, run.Id);
        auditView.IntegrityValid.Should().BeTrue();
        auditView.Events.Should().ContainSingle();
        FluentActions.Invoking(() => service.Get(Guid.NewGuid(), run.Id))
            .Should().Throw<KeyNotFoundException>();

        var dispatching = run with
        {
            State = AdminWithdrawalRunState.Dispatching,
            ApprovedBy = Guid.NewGuid(),
            Version = 3,
            DispatchSnapshotHash = "snapshot"
        };
        var terminalStore = new InMemoryAdminWithdrawalStore();
        terminalStore.Add(dispatching);
        var terminalWorkflow = new RecordingWorkflow();
        var terminalService = CreateService(
            terminalStore, new AdminWithdrawalAuditTrail(), CreatePolicy(tenantId, walletId, 3),
            CreateReserve(4), walletId, terminalWorkflow,
            provider: new RecordingProvider(CreateProviderEvent(dispatching)));
        var reconciled = await terminalService.ReconcileAsync(new ReconcileAdminWithdrawalCommand(
            tenantId, Guid.NewGuid(), dispatching.Id));
        reconciled.State.Should().Be(AdminWithdrawalRunState.Succeeded);
        terminalWorkflow.ProviderEvents.Should().ContainSingle();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("period")]
    [InlineData("amount")]
    [InlineData("destination")]
    [InlineData("idempotency")]
    [InlineData("long-destination")]
    [InlineData("reauth-null")]
    [InlineData("reauth-binding")]
    [InlineData("reauth-assurance")]
    public async Task ProposeAsync_RejectsEveryInvalidCommandShape(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var service = CreateService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            CreatePolicy(tenantId, walletId, 3), CreateReserve(4), walletId);
        var actorId = Guid.NewGuid();
        ProposeAdminWithdrawalCommand? command = CreateProposalCommand(
            tenantId, actorId, new DateOnly(2026, 8, 1), 500, "destination-hash", "key");
        command = invalid switch
        {
            "null" => null,
            "tenant" => command with { TenantId = Guid.Empty },
            "actor" => command with { ActorId = Guid.Empty },
            "period" => command with { PeriodStart = new DateOnly(2026, 8, 2) },
            "amount" => command with { AmountUnits = 0 },
            "destination" => command with { DestinationHash = " " },
            "idempotency" => command with { IdempotencyKey = " " },
            "long-destination" => command with { DestinationHash = new string('a', 129) },
            "reauth-null" => command with { Reauthentication = null! },
            "reauth-binding" => command with
            {
                Reauthentication = command.Reauthentication with { TransactionBinding = "wrong" }
            },
            "reauth-assurance" => command with
            {
                Reauthentication = command.Reauthentication with
                {
                    Assurance = ReauthenticationAssurance.Password
                }
            },
            _ => command
        };

        await FluentActions.Awaiting(async () => await service.ProposeAsync(command!).AsTask())
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("below-minimum")]
    [InlineData("above-maximum")]
    [InlineData("destination")]
    public async Task ProposeAsync_EnforcesEverySignedExecutionLimit(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var service = CreateService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            CreatePolicy(tenantId, walletId, 3), CreateReserve(4), walletId);
        var command = CreateProposalCommand(
            tenantId, Guid.NewGuid(), new DateOnly(2026, 8, 1),
            invalid == "below-minimum" ? 99 : invalid == "above-maximum" ? 10_001 : 500,
            invalid == "destination" ? "not-allowed" : "destination-hash",
            "key-" + invalid);

        await FluentActions.Awaiting(async () => await service.ProposeAsync(command).AsTask())
            .Should().ThrowAsync<AdminWithdrawalEligibilityException>();
    }

    [Fact]
    public async Task ProposeAsync_FallsBackToAnExplicitGlobalSignedPolicy()
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var global = CreatePolicy(tenantId, walletId, 3) with
        {
            TenantId = null,
            ScopeKey = "global"
        };
        var workflow = new RecordingWorkflow();
        var service = CreateAdvancedService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            new SelectivePolicyStore(null, global), new ConfigurableSignatureVerifier(true),
            CreateReserve(4), walletId, tenantId, workflow: workflow);

        var result = await service.ProposeAsync(CreateProposalCommand(
            tenantId, Guid.NewGuid(), new DateOnly(2026, 8, 1), 500,
            "destination-hash", "global-key"));

        result.PolicyVersion.Value.Should().Be(3);
        workflow.Reservations.Should().ContainSingle();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("proposed")]
    [InlineData("future")]
    [InlineData("expired")]
    [InlineData("provider")]
    [InlineData("hash")]
    [InlineData("signature")]
    public async Task ProposeAsync_FailsClosedForEveryPolicyReadinessFailure(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        EconomyCapabilityPolicy? policy = CreatePolicy(tenantId, walletId, 3);
        var signatureValid = true;
        policy = invalid switch
        {
            "missing" => null,
            "proposed" => policy with { State = EconomyCapabilityPolicyState.PendingApproval },
            "future" => policy with { EffectiveAt = Now.AddMinutes(1) },
            "expired" => policy with { ExpiresAt = Now },
            "provider" => policy with { ProviderReady = false },
            "hash" => policy with { PayloadHash = "wrong" },
            "signature" => policy,
            _ => policy
        };
        if (invalid == "signature") signatureValid = false;
        var service = CreateAdvancedService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            new SelectivePolicyStore(policy, null), new ConfigurableSignatureVerifier(signatureValid),
            CreateReserve(4), walletId, tenantId);

        await FluentActions.Awaiting(async () => await service.ProposeAsync(CreateProposalCommand(
                tenantId, Guid.NewGuid(), new DateOnly(2026, 8, 1), 500,
                "destination-hash", "policy-" + invalid)).AsTask())
            .Should().ThrowAsync<AdminWithdrawalExecutionDisabledException>();
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("missing")]
    [InlineData("wallet-type")]
    [InlineData("wallet-format")]
    [InlineData("wallet-empty")]
    [InlineData("source")]
    [InlineData("provider")]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("destinations")]
    [InlineData("destination-null")]
    public async Task ProposeAsync_FailsClosedForEveryMalformedPolicyValue(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var payload = invalid switch
        {
            "malformed" => "{",
            "missing" => "{}",
            "wallet-type" => PolicyPayload(123, "asset", "provider", 1, 2, ["destination-hash"]),
            "wallet-format" => PolicyPayload("not-a-guid", "asset", "provider", 1, 2, ["destination-hash"]),
            "wallet-empty" => PolicyPayload(Guid.Empty, "asset", "provider", 1, 2, ["destination-hash"]),
            "source" => PolicyPayload(walletId.Value, " ", "provider", 1, 2, ["destination-hash"]),
            "provider" => PolicyPayload(walletId.Value, "asset", " ", 1, 2, ["destination-hash"]),
            "minimum" => PolicyPayload(walletId.Value, "asset", "provider", 0, 2, ["destination-hash"]),
            "maximum" => PolicyPayload(walletId.Value, "asset", "provider", 2, 1, ["destination-hash"]),
            "destinations" => PolicyPayload(walletId.Value, "asset", "provider", 1, 2, []),
            "destination-null" => PolicyPayload(walletId.Value, "asset", "provider", 1, 2, [null!]),
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var policy = CreatePolicy(tenantId, walletId, 3) with
        {
            CanonicalPayload = payload,
            PayloadHash = Hash(payload)
        };
        var service = CreateAdvancedService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            new SelectivePolicyStore(policy, null), new ConfigurableSignatureVerifier(true),
            CreateReserve(4), walletId, tenantId);

        await FluentActions.Awaiting(async () => await service.ProposeAsync(CreateProposalCommand(
                tenantId, Guid.NewGuid(), new DateOnly(2026, 8, 1), 1,
                "destination-hash", "payload-" + invalid)).AsTask())
            .Should().ThrowAsync<AdminWithdrawalExecutionDisabledException>();
    }

    [Theory]
    [InlineData("purpose")]
    [InlineData("asset")]
    [InlineData("shortfall")]
    [InlineData("overflow")]
    public async Task ProposeAsync_FailsClosedForEveryReserveCoverageFailure(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var amount = invalid == "overflow" ? long.MaxValue : 500;
        var maximum = invalid == "overflow" ? long.MaxValue : 10_000;
        var payload = PolicyPayload(
            walletId.Value, "stripe:platform:cash", "provider", 1, maximum, ["destination-hash"]);
        var policy = CreatePolicy(tenantId, walletId, 3) with
        {
            CanonicalPayload = payload,
            PayloadHash = Hash(payload)
        };
        var allocation = invalid switch
        {
            "purpose" => new ExternalReserveAsset(
                "stripe:platform:cash", ReserveBackingPurpose.SoftCoin, 10_000_000_000),
            "asset" => new ExternalReserveAsset(
                "different", ReserveBackingPurpose.HardCoin, 10_000_000_000),
            "shortfall" => new ExternalReserveAsset(
                "stripe:platform:cash", ReserveBackingPurpose.HardCoin, 1),
            _ => new ExternalReserveAsset(
                "stripe:platform:cash", ReserveBackingPurpose.HardCoin, long.MaxValue)
        };
        var reserve = CreateReserve(4) with { AssetAllocations = [allocation] };
        var service = CreateAdvancedService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            new SelectivePolicyStore(policy, null), new ConfigurableSignatureVerifier(true),
            reserve, walletId, tenantId);

        await FluentActions.Awaiting(async () => await service.ProposeAsync(CreateProposalCommand(
                tenantId, Guid.NewGuid(), new DateOnly(2026, 8, 1), amount,
                "destination-hash", "reserve-" + invalid)).AsTask())
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("run")]
    [InlineData("version")]
    public async Task ApproveAsync_RejectsEveryInvalidCommandShape(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var service = CreateService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            CreatePolicy(tenantId, walletId, 3), CreateReserve(4), walletId);
        ApproveAdminWithdrawalCommand? command = new(tenantId, Guid.NewGuid(), Guid.NewGuid(), 1);
        command = invalid switch
        {
            "null" => null,
            "tenant" => command with { TenantId = Guid.Empty },
            "actor" => command with { ActorId = Guid.Empty },
            "run" => command with { RunId = Guid.Empty },
            "version" => command with { ExpectedVersion = 0 },
            _ => command
        };

        await FluentActions.Awaiting(() => service.ApproveAsync(command!))
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("run")]
    [InlineData("version")]
    [InlineData("reauth-null")]
    [InlineData("reauth-binding")]
    public async Task DispatchAsync_RejectsEveryInvalidCommandShape(string invalid)
    {
        var fixture = CreateDispatchFixture("valid");
        var actorId = Guid.NewGuid();
        DispatchAdminWithdrawalCommand? command = CreateDispatchCommand(
            fixture.Run.TenantId, actorId, fixture.Run.Id, fixture.Run.Version);
        command = invalid switch
        {
            "null" => null,
            "tenant" => command with { TenantId = Guid.Empty },
            "actor" => command with { ActorId = Guid.Empty },
            "run" => command with { RunId = Guid.Empty },
            "version" => command with { ExpectedVersion = 0 },
            "reauth-null" => command with { Reauthentication = null! },
            "reauth-binding" => command with
            {
                Reauthentication = command.Reauthentication with { TransactionBinding = "wrong" }
            },
            _ => command
        };

        await FluentActions.Awaiting(() => fixture.Service.DispatchAsync(command!))
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("state")]
    [InlineData("version")]
    [InlineData("audit")]
    [InlineData("snapshot-missing")]
    [InlineData("snapshot-null")]
    [InlineData("snapshot-json")]
    [InlineData("request")]
    [InlineData("policy-binding")]
    [InlineData("reserve-binding")]
    [InlineData("destination-binding")]
    [InlineData("subject")]
    [InlineData("jurisdiction")]
    [InlineData("provider-binding")]
    [InlineData("receipt")]
    [InlineData("roots-empty")]
    [InlineData("policy-version")]
    [InlineData("policy-provider")]
    [InlineData("policy-destination")]
    [InlineData("reserve-version")]
    [InlineData("reserve-epoch")]
    [InlineData("fragments-empty")]
    [InlineData("fragments-incomplete")]
    [InlineData("roots-changed")]
    [InlineData("reauth-binding")]
    public async Task DispatchAsync_FailsClosedForEveryStaleAuthorityOrEvidenceBinding(string invalid)
    {
        var fixture = CreateDispatchFixture(invalid);
        var actorId = Guid.NewGuid();
        var expectedVersion = invalid == "version" ? fixture.Run.Version + 1 : fixture.Run.Version;
        var command = CreateDispatchCommand(
            fixture.Run.TenantId, actorId, fixture.Run.Id, expectedVersion);
        if (invalid == "reauth-binding")
            command = command with
            {
                Reauthentication = command.Reauthentication with { TransactionBinding = "wrong" }
            };

        await FluentActions.Awaiting(() => fixture.Service.DispatchAsync(command))
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("run")]
    public async Task ReconcileAsync_RejectsEveryInvalidCommandShape(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var service = CreateService(
            new InMemoryAdminWithdrawalStore(), new AdminWithdrawalAuditTrail(),
            CreatePolicy(tenantId, walletId, 3), CreateReserve(4), walletId);
        ReconcileAdminWithdrawalCommand? command = new(tenantId, Guid.NewGuid(), Guid.NewGuid());
        command = invalid switch
        {
            "null" => null,
            "tenant" => command with { TenantId = Guid.Empty },
            "actor" => command with { ActorId = Guid.Empty },
            "run" => command with { RunId = Guid.Empty },
            _ => command
        };

        await FluentActions.Awaiting(() => service.ReconcileAsync(command!))
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(AdminWithdrawalRunState.Dispatching, AdminWithdrawalProviderOutcome.Submitted, false)]
    [InlineData(AdminWithdrawalRunState.Ambiguous, AdminWithdrawalProviderOutcome.Ambiguous, false)]
    [InlineData(AdminWithdrawalRunState.Dispatching, AdminWithdrawalProviderOutcome.Failed, true)]
    [InlineData(AdminWithdrawalRunState.Ambiguous, AdminWithdrawalProviderOutcome.Succeeded, true)]
    public async Task ReconcileAsync_HandlesEveryDispatchStateAndProviderOutcome(
        AdminWithdrawalRunState state,
        AdminWithdrawalProviderOutcome outcome,
        bool terminal)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var run = CreateRun(tenantId, walletId) with
        {
            State = state,
            ApprovedBy = Guid.NewGuid(),
            Version = state == AdminWithdrawalRunState.Dispatching ? 3 : 4,
            DispatchSnapshotHash = "snapshot",
            ProviderTransferId = "provider-transfer"
        };
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var workflow = new RecordingWorkflow();
        var providerEvent = CreateProviderEvent(run) with { Outcome = outcome };
        var service = CreateService(
            store, new AdminWithdrawalAuditTrail(), CreatePolicy(tenantId, walletId, 3),
            CreateReserve(4), walletId, workflow,
            provider: new RecordingProvider(providerEvent));

        var result = await service.ReconcileAsync(new ReconcileAdminWithdrawalCommand(
            tenantId, Guid.NewGuid(), run.Id));

        if (terminal) workflow.ProviderEvents.Should().ContainSingle();
        else
        {
            result.Should().BeSameAs(run);
            workflow.ProviderEvents.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ReconcileAsync_RejectsRunsThatWereNeverDispatched()
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var run = CreateRun(tenantId, walletId);
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var service = CreateService(
            store, new AdminWithdrawalAuditTrail(), CreatePolicy(tenantId, walletId, 3),
            CreateReserve(4), walletId);

        await FluentActions.Awaiting(() => service.ReconcileAsync(
                new ReconcileAdminWithdrawalCommand(tenantId, Guid.NewGuid(), run.Id)))
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
    }

    private static DispatchFixture CreateDispatchFixture(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var walletId = WalletId.New();
        var root = SourceStampId.New();
        var rootHash = Hash(root.Value.ToString("N"));
        var run = CreateRun(tenantId, walletId) with
        {
            State = invalid == "state"
                ? AdminWithdrawalRunState.PendingApproval
                : AdminWithdrawalRunState.Approved,
            ApprovedBy = invalid == "state" ? null : Guid.NewGuid(),
            Version = invalid == "state" ? 1 : 2
        };
        var store = new InMemoryAdminWithdrawalStore();
        store.Add(run);
        var audit = new AdminWithdrawalAuditTrail();
        var snapshot = CreateAuthorizationSnapshot(run, rootHash);
        snapshot = invalid switch
        {
            "request" => snapshot with { RequestHash = "changed" },
            "policy-binding" => snapshot with { PolicyVersion = run.PolicyVersion.Value + 1 },
            "reserve-binding" => snapshot with { ReserveVersion = run.ReserveVersion.Value + 1 },
            "destination-binding" => snapshot with { DestinationHash = "changed" },
            "subject" => snapshot with { SubjectReference = " " },
            "jurisdiction" => snapshot with { JurisdictionCode = " " },
            "provider-binding" => snapshot with { ProviderHash = " " },
            "receipt" => snapshot with { ReceiptHash = " " },
            "roots-empty" => snapshot with { SourceRootHashes = [] },
            "roots-changed" => snapshot with { SourceRootHashes = ["changed-root"] },
            _ => snapshot
        };
        if (invalid != "audit")
        {
            var kind = invalid == "snapshot-missing" ? "requested" : "reserved";
            var evidence = invalid switch
            {
                "snapshot-null" => "null",
                "snapshot-json" => "{",
                _ => JsonSerializer.Serialize(snapshot)
            };
            audit.Append(run.Id, kind, run.RequestedBy, evidence, run.CreatedAt);
        }

        var policy = CreatePolicy(tenantId, walletId, run.PolicyVersion.Value);
        if (invalid == "policy-version") policy = policy with { Version = policy.Version + 1 };
        if (invalid is "policy-provider" or "policy-destination")
        {
            var payload = PolicyPayload(
                walletId.Value,
                "stripe:platform:cash",
                invalid == "policy-provider" ? "changed-provider" : "stripe-platform-provider",
                100,
                10_000,
                invalid == "policy-destination" ? ["changed-destination"] : ["destination-hash"]);
            policy = policy with { CanonicalPayload = payload, PayloadHash = Hash(payload) };
        }

        var reserve = CreateReserve(run.ReserveVersion.Value);
        if (invalid == "reserve-version")
            reserve = reserve with { Version = new ReserveVersion(run.ReserveVersion.Value + 1) };
        if (invalid == "reserve-epoch")
            reserve = reserve with { AuthorizationEpoch = run.ReserveAuthorizationEpoch + 1 };
        IReadOnlyList<PersistedFragmentReservation> reservations = invalid == "fragments-empty"
            ? []
            : [CreateFragment(run, root)];
        if (invalid == "fragments-incomplete")
            reservations = [CreateFragment(run, root) with
            {
                Amount = new CoinAmount(CurrencyCode.HardCoin, run.Amount.Units - 1)
            }];
        var service = CreateAdvancedService(
            store,
            audit,
            new SelectivePolicyStore(policy, null),
            new ConfigurableSignatureVerifier(true),
            reserve,
            walletId,
            tenantId,
            reservations: reservations);
        return new DispatchFixture(service, run);
    }

    private sealed record DispatchFixture(
        DurableAdminWithdrawalApplicationService Service,
        AdminWithdrawalRun Run);

    private static string PolicyPayload(
        object platformFeeWalletId,
        string sourceAssetKey,
        string providerHash,
        long minimumAmountUnits,
        long maximumAmountUnits,
        string[] destinationHashes) =>
        EconomyCanonicalJson.Serialize(JsonSerializer.SerializeToElement(new
        {
            platformFeeWalletId,
            sourceAssetKey,
            providerHash,
            minimumAmountUnits,
            maximumAmountUnits,
            destinationHashes
        }));

    private static DurableAdminWithdrawalApplicationService CreateAdvancedService(
        IAdminWithdrawalStore store,
        IAdminWithdrawalAuditTrail audit,
        IEconomyCapabilityPolicyStore policyStore,
        ICapabilityPolicySignatureVerifier signatureVerifier,
        ReserveHead reserve,
        WalletId walletId,
        Guid walletTenantId,
        RecordingWorkflow? workflow = null,
        IReadOnlyList<PersistedFragmentReservation>? reservations = null,
        IAdminWithdrawalProvider? provider = null) => new(
        store,
        audit,
        policyStore,
        signatureVerifier,
        new StubReserveControlPlane(reserve),
        new StubWalletDirectory(walletId, walletTenantId),
        new StubFencingAllocator(11),
        new StubReservationReader(reservations ?? []),
        workflow ?? new RecordingWorkflow(),
        provider ?? new RecordingProvider(null),
        new FixedJurisdictionResolver(),
        new FixedTimeProvider(Now));

    private static DurableAdminWithdrawalApplicationService CreateService(
        IAdminWithdrawalStore store,
        IAdminWithdrawalAuditTrail audit,
        EconomyCapabilityPolicy policy,
        ReserveHead reserve,
        WalletId walletId,
        RecordingWorkflow? workflow = null,
        long fencingToken = 11,
        IReadOnlyList<PersistedFragmentReservation>? reservations = null,
        RecordingPolicyStore? policyStore = null,
        IAdminWithdrawalProvider? provider = null) => new(
        store,
        audit,
        policyStore ?? new RecordingPolicyStore(policy),
        new AcceptPolicySignatureVerifier(),
        new StubReserveControlPlane(reserve),
        new StubWalletDirectory(walletId, policy.TenantId!.Value),
        new StubFencingAllocator(fencingToken),
        new StubReservationReader(reservations ?? []),
        workflow ?? new RecordingWorkflow(),
        provider ?? new RecordingProvider(null),
        new FixedJurisdictionResolver(),
        new FixedTimeProvider(Now));

    private static EconomyCapabilityPolicy CreatePolicy(Guid tenantId, WalletId walletId, long version)
    {
        var canonical = EconomyCanonicalJson.Serialize(JsonSerializer.SerializeToElement(new
        {
            platformFeeWalletId = walletId.Value,
            sourceAssetKey = "stripe:platform:cash",
            providerHash = "stripe-platform-provider",
            minimumAmountUnits = 100,
            maximumAmountUnits = 10_000,
            destinationHashes = new[] { "destination-hash" }
        }));
        return new EconomyCapabilityPolicy(
            Guid.NewGuid(), $"tenant:{tenantId:N}", tenantId,
            EconomyValueMovementCapability.AdminWithdrawalExecution, "US", version,
            canonical, Hash(canonical), "key", "signature", Guid.NewGuid(), Guid.NewGuid(),
            Now.AddDays(-1), Now.AddHours(-2), Now.AddHours(-1), Now.AddHours(1), true,
            EconomyCapabilityPolicyState.Active);
    }

    private static ReserveHead CreateReserve(long version) => new(
        new ReserveVersion(version), new PolicyVersion(1), 7, Now.AddMinutes(-1), Now.AddHours(1),
        new ReserveRequirementSnapshot(0, 0, 0, 0, 0),
        10_000_000_000, 0, ReserveCoverageState.Covered,
        [new ExternalReserveAsset("stripe:platform:cash", ReserveBackingPurpose.HardCoin, 10_000_000_000)],
        "reserve-evidence-hash");

    private static AdminWithdrawalRun CreateRun(Guid tenantId, WalletId walletId) => new(
        Guid.NewGuid(), tenantId, new IdempotencyKey("admin-withdrawal-test"), "request-hash",
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, walletId,
        new CoinAmount(CurrencyCode.HardCoin, 500), "stripe:platform:cash", "destination-hash",
        AdminWithdrawalRunState.PendingApproval, 1, 11, 11, new ReserveVersion(4), 7,
        new PolicyVersion(3), null, null, Now.AddMinutes(-5), Now.AddMinutes(-5));

    private static AdminWithdrawalAuthorizationSnapshot CreateAuthorizationSnapshot(
        AdminWithdrawalRun run,
        string rootHash) => new(
        run.RequestHash,
        $"treasury:{run.TenantId:N}",
        "US",
        "stripe-platform-provider",
        run.DestinationHash,
        run.PolicyVersion.Value,
        run.ReserveVersion.Value,
        0,
        Guid.NewGuid(),
        Hash("reservation-operation-fingerprint"),
        Hash("reauth-evidence"),
        "reservation-receipt-hash",
        [rootHash],
        ["evidence-hash"]);

    private static ProposeAdminWithdrawalCommand CreateProposalCommand(
        Guid tenantId,
        Guid actorId,
        DateOnly periodStart,
        long amountUnits,
        string destinationHash,
        string idempotencyKey)
    {
        var normalizedDestination = destinationHash.Trim().ToLowerInvariant();
        var normalizedKey = idempotencyKey.Trim();
        var binding = TreasuryProtectedOperationBinding.Proposal(
            periodStart, amountUnits, normalizedDestination, normalizedKey);
        return new ProposeAdminWithdrawalCommand(
            tenantId, actorId, periodStart, amountUnits, destinationHash, idempotencyKey,
            Reauthentication(actorId, binding));
    }

    private static DispatchAdminWithdrawalCommand CreateDispatchCommand(
        Guid tenantId,
        Guid actorId,
        Guid runId,
        long expectedVersion) => new(
        tenantId,
        actorId,
        runId,
        expectedVersion,
        Reauthentication(actorId, TreasuryProtectedOperationBinding.Dispatch(runId, expectedVersion)));

    private static ReauthenticationEvidence Reauthentication(Guid actorId, string binding) => new(
        actorId,
        ProtectedOperationKind.AdministrativeAdjustment,
        binding,
        ReauthenticationAssurance.MultiFactor,
        Now.AddMinutes(-1),
        Now.AddMinutes(5),
        Hash("reauth-evidence"));

    private static PersistedFragmentReservation CreateFragment(
        AdminWithdrawalRun run,
        SourceStampId root) => new(
        Guid.NewGuid(), run.Id, CreditLotId.New(), root, 0,
        new RootTraceRange(root, 0, run.Amount.Units * 1000, 0), run.Amount);

    private static AdminWithdrawalProviderEvent CreateProviderEvent(AdminWithdrawalRun run) => new(
        "evt-terminal", run.Id, run.TenantId, AdminWithdrawalProviderOutcome.Succeeded,
        "provider-transfer", run.FencingToken, run.ExecutionEpoch, run.Amount,
        run.SourceAssetKey, run.DestinationHash, "evidence", "signature", Now);

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RecordingWorkflow : IDurableAdminWithdrawalWorkflow
    {
        public List<DurableAdminWithdrawalReservationRequest> Reservations { get; } = [];
        public List<DurableAdminWithdrawalDispatchRequest> Dispatches { get; } = [];
        public List<DurableAdminWithdrawalProviderEventRequest> ProviderEvents { get; } = [];

        public Task<AdminWithdrawalRun> ReserveAsync(
            DurableAdminWithdrawalReservationRequest request,
            CancellationToken cancellationToken = default)
        {
            Reservations.Add(request);
            return Task.FromResult(request.Run);
        }

        public Task<AdminWithdrawalRun> ApproveAsync(
            DurableAdminWithdrawalApprovalRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateRun(request.TenantId, WalletId.New()) with
            {
                Id = request.RunId, State = AdminWithdrawalRunState.Approved,
                ApprovedBy = request.ApprovedBy, Version = request.ExpectedVersion + 1
            });

        public Task<AdminWithdrawalRun> BeginDispatchAsync(
            DurableAdminWithdrawalDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            Dispatches.Add(request);
            return Task.FromResult(CreateRun(request.TenantId, WalletId.New()) with
            {
                Id = request.RunId, State = AdminWithdrawalRunState.Dispatching,
                ApprovedBy = Guid.NewGuid(), Version = request.ExpectedVersion + 1,
                DispatchSnapshotHash = Hash(request.ReauthenticationEvidenceHash + request.ProviderHash)
            });
        }

        public Task<AdminWithdrawalRun> ApplyProviderEventAsync(
            DurableAdminWithdrawalProviderEventRequest request,
            CancellationToken cancellationToken = default)
        {
            ProviderEvents.Add(request);
            var value = request.ProviderEvent;
            return Task.FromResult(CreateRun(value.TenantId, WalletId.New()) with
            {
                Id = value.RunId, State = value.Outcome == AdminWithdrawalProviderOutcome.Succeeded
                    ? AdminWithdrawalRunState.Succeeded : AdminWithdrawalRunState.Failed,
                ApprovedBy = Guid.NewGuid(), Version = 4, DispatchSnapshotHash = "snapshot",
                ProviderTransferId = value.ProviderTransferId
            });
        }
    }

    private sealed class FixedJurisdictionResolver : IEconomyJurisdictionResolver
    {
        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId,
            Guid actorId,
            string? providerJurisdiction,
            string? destinationJurisdiction,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new EconomyJurisdictionResolution(
                "US", 1, 1, Hash("jurisdiction-evidence")));
    }

    private sealed class RecordingPolicyStore(EconomyCapabilityPolicy? policy) : IEconomyCapabilityPolicyStore
    {
        public int CurrentCalls { get; private set; }
        public ValueTask<EconomyCapabilityPolicy> ProposeAsync(EconomyCapabilityPolicyProposal proposal, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy> ApproveAsync(Guid policyId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy?> CurrentAsync(Guid? tenantId, EconomyValueMovementCapability capability, string jurisdictionCode, CancellationToken cancellationToken)
        {
            CurrentCalls++;
            return ValueTask.FromResult(policy?.TenantId == tenantId ? policy : null);
        }
    }

    private sealed class SelectivePolicyStore(
        EconomyCapabilityPolicy? tenantPolicy,
        EconomyCapabilityPolicy? globalPolicy) : IEconomyCapabilityPolicyStore
    {
        public ValueTask<EconomyCapabilityPolicy> ProposeAsync(
            EconomyCapabilityPolicyProposal proposal,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<EconomyCapabilityPolicy> ApproveAsync(
            Guid policyId,
            Guid actorId,
            string reauthenticationHash,
            DateTimeOffset approvedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<int> ActivateDueAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<EconomyCapabilityPolicy?> CurrentAsync(
            Guid? tenantId,
            EconomyValueMovementCapability capability,
            string jurisdictionCode,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(tenantId.HasValue ? tenantPolicy : globalPolicy);
    }

    private sealed class ConfigurableSignatureVerifier(bool valid) : ICapabilityPolicySignatureVerifier
    {
        public ValueTask<bool> VerifyAsync(
            string canonicalPayload,
            string keyId,
            string signature,
            CancellationToken cancellationToken) => ValueTask.FromResult(valid);
    }

    private sealed class AcceptPolicySignatureVerifier : ICapabilityPolicySignatureVerifier
    {
        public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    private sealed class StubReserveControlPlane(ReserveHead head) : IEconomyReserveCustodyControlPlane
    {
        public ValueTask<ReserveHead> CurrentHeadAsync(DateTimeOffset now, CancellationToken cancellationToken) => ValueTask.FromResult(head);
        public ValueTask<DurableCustodyObservation> IngestObservationAsync(CustodyObservationCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyLiabilitySnapshot> CalculateLiabilitiesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<DurableReserveProposalState> ProposeAsync(DurableReserveProposalCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ReserveHead> ApproveAndActivateAsync(Guid proposalId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ReservePostingAuthorization> AuthorizeAsync(ReserveVersion version, long authorizationEpoch, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubWalletDirectory(WalletId walletId, Guid tenantId) : IEconomyWalletDirectory
    {
        public ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(Guid tenant, Guid ownerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<EconomyWalletIdentity> GetWalletAsync(Guid tenant, WalletId requested, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(tenant == tenantId && requested == walletId
                ? new EconomyWalletIdentity(walletId, tenantId, Guid.NewGuid(), WalletLifecycleState.Active)
                : throw new EconomyWalletUnavailableException("wallet unavailable"));
    }

    private sealed class StubFencingAllocator(long value) : IAdminWithdrawalFencingTokenAllocator
    {
        public ValueTask<long> AllocateAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(value);
    }

    private sealed class StubReservationReader(IReadOnlyList<PersistedFragmentReservation> rows) : IFifoFragmentReservationReader
    {
        public IReadOnlyList<PersistedFragmentReservation> Read(Guid operationId, PersistedFragmentReservationStatus status) => rows;
    }

    private sealed class RecordingProvider(AdminWithdrawalProviderEvent? providerEvent) : IAdminWithdrawalProvider
    {
        public ValueTask<AdminWithdrawalProviderReceipt> DispatchAsync(AdminWithdrawalDispatchCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<AdminWithdrawalProviderEvent> ReconcileAsync(Guid tenantId, Guid runId, string idempotencyKey, string? providerTransferId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(providerEvent ?? throw new NotSupportedException());
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
