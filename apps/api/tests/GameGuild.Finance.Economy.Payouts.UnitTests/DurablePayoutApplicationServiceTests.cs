using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class DurablePayoutApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProtectedOperationBindingsAreCanonicalAndRejectIncompleteTargets()
    {
        var requestId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        PayoutProtectedOperationBinding.Reservation(requestId)
            .Should().Be(PayoutProtectedOperationBinding.Reservation(requestId)).And.HaveLength(64);
        PayoutProtectedOperationBinding.Dispatch(operationId, 1)
            .Should().NotBe(PayoutProtectedOperationBinding.Dispatch(operationId, 2));
        FluentActions.Invoking(() => PayoutProtectedOperationBinding.Reservation(Guid.Empty))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PayoutProtectedOperationBinding.Dispatch(Guid.Empty, 1))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => PayoutProtectedOperationBinding.Dispatch(operationId, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AccountOnboardingAndStatusRequireATenantOwnedWalletAndBoundProviderAccount()
    {
        var fixture = new Fixture();

        var onboarding = await fixture.Service.CreateOrRefreshAccountAsync(fixture.TenantId, fixture.PayeeId);
        var status = await fixture.Service.GetAccountAsync(fixture.TenantId, fixture.PayeeId);

        onboarding.Account.Should().Be(fixture.Provider.Account);
        onboarding.OnboardingUri.Should().Be(new Uri("https://connect.example/onboard"));
        status.Should().Be(fixture.Provider.Account);
        fixture.Wallets.OwnerLookups.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReserveApprovedDerivesTheDurableSnapshotFromReviewPolicyReserveProviderAndReauthentication()
    {
        var fixture = new Fixture();

        var operation = await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand());

        operation.Id.Should().Be(fixture.Request.Id);
        operation.TenantId.Should().Be(fixture.TenantId);
        operation.ActorId.Should().Be(fixture.ActorId);
        operation.PayeeId.Should().Be(fixture.PayeeId);
        operation.PolicyVersion.Value.Should().Be(3);
        operation.ReserveVersion.Value.Should().Be(4);
        operation.ReserveAuthorizationEpoch.Should().Be(7);
        operation.FencingToken.Should().Be(91);
        operation.KillSwitchEpoch.Should().Be(3);
        operation.RiskDecisionId.Should().NotBeEmpty();
        operation.ProviderBindingHash.Should().MatchRegex("^[0-9a-f]{64}$");
        operation.EligibilityHash.Should().MatchRegex("^[0-9a-f]{64}$");
        var durable = fixture.ReservationWorkflow.Requests.Should().ContainSingle().Subject;
        durable.JurisdictionCode.Should().Be("BR");
        durable.ProviderHash.Should().Be("stripe-connect-provider");
        durable.ReauthenticationEvidenceHash.Should().MatchRegex("^[0-9a-f]{64}$");
        fixture.Jurisdictions.Subjects.Should().ContainSingle().Which.Should().Be(fixture.PayeeId);
    }

    [Fact]
    public async Task ReserveApprovedReturnsAnExactReplayBeforeReadingMutableProviderOrControlPlaneState()
    {
        var fixture = new Fixture();
        var existing = await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand());
        fixture.Operations.Add(existing);
        fixture.Provider.ThrowOnRead = true;

        var replay = await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand());

        replay.Should().BeSameAs(existing);
        fixture.ReservationWorkflow.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchReauthorizesTheExactReservedRootsBeforeCreatingTheOutboxTransition()
    {
        var fixture = new Fixture();
        var operation = fixture.DispatchableOperation();
        fixture.Operations.Add(operation);
        fixture.ReservationReader.Fragments = [fixture.Fragment(operation)];

        var dispatching = await fixture.Service.DispatchAsync(fixture.DispatchCommand(operation));

        dispatching.State.Should().Be(PayoutOperationState.Dispatching);
        var dispatch = fixture.SettlementWorkflow.Dispatches.Should().ContainSingle().Subject;
        dispatch.ActorId.Should().Be(fixture.ActorId);
        dispatch.SourceRoots.Should().ContainSingle();
        dispatch.ReauthenticationEvidenceHash.Should().MatchRegex("^[0-9a-f]{64}$");
        dispatch.KillSwitchEpoch.Should().Be(operation.KillSwitchEpoch);
    }

    [Fact]
    public async Task ProviderEventsAndReconciliationUseOnlyTenantScopedInFlightOperations()
    {
        var fixture = new Fixture();
        var operation = fixture.DispatchableOperation()
            .Transition(PayoutOperationState.Dispatching, Now.AddMinutes(1), "snapshot")
            .BindProviderDispatch("po_123", Now.AddMinutes(2));
        fixture.Operations.Add(operation);
        fixture.Provider.ReconciliationEvent = fixture.ProviderEvent(operation, PayoutProviderOutcome.Succeeded);

        var direct = await fixture.Service.ApplyProviderEventAsync(
            fixture.ProviderEvent(operation, PayoutProviderOutcome.Failed));
        var reconciled = await fixture.Service.ReconcileAsync(
            new ReconcilePayoutOperationCommand(fixture.TenantId, fixture.ActorId, operation.Id));

        direct.State.Should().Be(PayoutOperationState.Failed);
        reconciled.State.Should().Be(PayoutOperationState.Succeeded);
        fixture.SettlementWorkflow.ProviderEvents.Should().HaveCount(2);
        fixture.Service.Get(fixture.TenantId, operation.Id).Should().BeSameAs(operation);
        fixture.Service.List(fixture.TenantId).Should().ContainSingle();
    }

    [Fact]
    public async Task NonTerminalReconciliationPreservesTheAmbiguousOperation()
    {
        var fixture = new Fixture();
        var operation = fixture.DispatchableOperation()
            .Transition(PayoutOperationState.Dispatching, Now.AddMinutes(1), "snapshot")
            .Transition(PayoutOperationState.Ambiguous, Now.AddMinutes(2), providerPayoutId: "unknown:operation");
        fixture.Operations.Add(operation);
        fixture.Provider.ReconciliationEvent = fixture.ProviderEvent(operation, PayoutProviderOutcome.Ambiguous);

        var reconciled = await fixture.Service.ReconcileAsync(
            new ReconcilePayoutOperationCommand(fixture.TenantId, fixture.ActorId, operation.Id));

        reconciled.Should().BeSameAs(operation);
        fixture.SettlementWorkflow.ProviderEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveAndDispatchFailClosedForIncompleteApprovalOrMutableBindings()
    {
        var fixture = new Fixture();
        fixture.Requests.Request = fixture.Request with { State = PayoutRequestState.AwaitingSecondApproval };
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutEligibilityException>();

        fixture = new Fixture();
        fixture.Requests.Audit = [fixture.Requests.Audit[0]];
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutEligibilityException>();

        fixture = new Fixture();
        var operation = fixture.DispatchableOperation();
        fixture.Operations.Add(operation);
        fixture.Provider.Account = fixture.Provider.Account with { DestinationHash = "changed-destination" };
        await FluentActions.Awaiting(async () => await fixture.Service.DispatchAsync(fixture.DispatchCommand(operation)))
            .Should().ThrowAsync<PayoutProviderBindingException>();
    }

    [Fact]
    public async Task PolicyAccountReserveAndReauthenticationFailuresRemainFailClosed()
    {
        var fixture = new Fixture();
        fixture.Signatures.Valid = false;
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutExecutionDisabledException>();

        fixture = new Fixture();
        fixture.Provider.Account = fixture.Provider.Account with { PayoutsEnabled = false };
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutExecutionDisabledException>();

        fixture = new Fixture();
        var command = fixture.ReserveCommand() with
        {
            Reauthentication = fixture.Reauthentication(
                PayoutProtectedOperationBinding.Reservation(fixture.Request.Id)) with { ExpiresAt = Now }
        };
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(command))
            .Should().ThrowAsync<ReauthenticationEvidenceException>();
    }

    [Fact]
    public async Task MalformedCommandsAndInvalidReconciliationStatesAreRejected()
    {
        var fixture = new Fixture();
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(async () => await fixture.Service.DispatchAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(async () => await fixture.Service.ReconcileAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(async () => await fixture.Service.CreateOrRefreshAccountAsync(Guid.Empty, fixture.PayeeId))
            .Should().ThrowAsync<ArgumentException>();

        var reserved = fixture.DispatchableOperation();
        fixture.Operations.Add(reserved);
        await FluentActions.Awaiting(async () => await fixture.Service.ReconcileAsync(
                new ReconcilePayoutOperationCommand(fixture.TenantId, fixture.ActorId, reserved.Id)))
            .Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task ReservationRejectsWalletAmountIdentityAndBindingMismatches()
    {
        var fixture = new Fixture();
        fixture.Wallets.OwnerId = Guid.NewGuid();
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutEligibilityException>();

        fixture = new Fixture();
        fixture.Requests.Request = fixture.Request with { Amount = new CoinAmount(CurrencyCode.HardCoin, 99) };
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutEligibilityException>();

        fixture = new Fixture();
        fixture.Requests.Request = fixture.Request with { Amount = new CoinAmount(CurrencyCode.HardCoin, 1_000_001) };
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutEligibilityException>();

        fixture = new Fixture();
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(
                fixture.ReserveCommand() with { ActorId = Guid.Empty }))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(
                fixture.ReserveCommand() with { RequestId = Guid.Empty }))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(
                fixture.ReserveCommand() with
                {
                    Reauthentication = fixture.Reauthentication("client-selected-binding")
                }))
            .Should().ThrowAsync<ReauthenticationEvidenceException>();
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(
                fixture.ReserveCommand() with { Reauthentication = null! }))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReservationRequiresAnAuthenticActiveTenantOrGlobalPolicy()
    {
        var fixture = new Fixture();
        fixture.Policies.MissTenantPolicy = true;
        (await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand())).State
            .Should().Be(PayoutOperationState.Reserved);
        fixture.Policies.Lookups.Should().Equal(fixture.TenantId, null);

        fixture = new Fixture();
        fixture.Policies.Policy = null;
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutExecutionDisabledException>();

        foreach (var mutate in new Func<EconomyCapabilityPolicy, EconomyCapabilityPolicy>[]
                 {
                     policy => policy with { State = EconomyCapabilityPolicyState.PendingApproval },
                     policy => policy with { EffectiveAt = Now.AddMinutes(1) },
                     policy => policy with { ExpiresAt = Now },
                     policy => policy with { ProviderReady = false }
                 })
        {
            fixture = new Fixture();
            fixture.Policies.Policy = mutate(fixture.Policies.Policy!);
            await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
                .Should().ThrowAsync<PayoutExecutionDisabledException>();
        }

        fixture = new Fixture();
        fixture.Policies.Policy = fixture.Policies.Policy! with { PayloadHash = Hash("tampered") };
        await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
            .Should().ThrowAsync<PayoutExecutionDisabledException>();
    }

    [Fact]
    public async Task ReservationRejectsEveryMalformedPolicyPayloadShape()
    {
        foreach (var payload in new[]
                 {
                     "{}",
                     "{\"maximumAmountUnits\":100,\"minimumAmountUnits\":0,\"providerHash\":\"provider\"}",
                     "{\"maximumAmountUnits\":99,\"minimumAmountUnits\":100,\"providerHash\":\"provider\"}",
                     $"{{\"maximumAmountUnits\":100,\"minimumAmountUnits\":1,\"providerHash\":\"{new string('p', 129)}\"}}",
                     "not-json"
                 })
        {
            var fixture = new Fixture();
            fixture.Policies.Policy = fixture.Policies.Policy! with
            {
                CanonicalPayload = payload,
                PayloadHash = Hash(payload)
            };
            var exception = await FluentActions.Awaiting(
                    async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
                .Should().ThrowAsync<PayoutExecutionDisabledException>();
            exception.Which.InnerException.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ReservationRequiresEveryIndependentDualControlBinding()
    {
        foreach (var mutate in new Func<Fixture, IReadOnlyList<PayoutRequestReviewAuditEvent>>[]
                 {
                     fixture => [fixture.Requests.Audit[0], fixture.Requests.Audit[1] with { ActorId = fixture.Requests.Audit[0].ActorId }],
                     fixture => [fixture.Requests.Audit[0], fixture.Requests.Audit[1] with { TenantId = Guid.NewGuid() }],
                     fixture => [fixture.Requests.Audit[0], fixture.Requests.Audit[1] with { RequestId = Guid.NewGuid() }],
                     fixture => [fixture.Requests.Audit[0], fixture.Requests.Audit[1] with { ActorId = fixture.PayeeId }],
                     fixture => [fixture.Requests.Audit[0], fixture.Requests.Audit[1] with { Reason = " " }]
                 })
        {
            var fixture = new Fixture();
            fixture.Requests.Audit = mutate(fixture);
            await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
                .Should().ThrowAsync<PayoutEligibilityException>();
        }
    }

    [Fact]
    public async Task ProviderAccountMustSatisfyEveryIdentityAndReadinessPredicate()
    {
        foreach (var mutate in new Func<Fixture, ConnectAccountSnapshot>[]
                 {
                     fixture => fixture.Provider.Account with { PayeeId = Guid.NewGuid() },
                     fixture => fixture.Provider.Account with { ProviderAccountId = " " },
                     fixture => fixture.Provider.Account with { DestinationHash = " " },
                     fixture => fixture.Provider.Account with { State = ConnectAccountState.Restricted },
                     fixture => fixture.Provider.Account with { ChargesEnabled = false },
                     fixture => fixture.Provider.Account with { PayoutsEnabled = false },
                     fixture => fixture.Provider.Account with { ObservedAt = Now.AddTicks(1) },
                     fixture => fixture.Provider.Account with { ExpiresAt = Now },
                     fixture => fixture.Provider.Account with { EvidenceHash = " " }
                 })
        {
            var fixture = new Fixture();
            fixture.Provider.Account = mutate(fixture);
            await FluentActions.Awaiting(async () => await fixture.Service.ReserveApprovedAsync(fixture.ReserveCommand()))
                .Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task DispatchRejectsEveryStaleSnapshotAndIncompleteReservation()
    {
        async Task RejectAsync(
            Action<Fixture, PayoutOperation> mutate,
            Func<Fixture, PayoutOperation, DispatchPayoutOperationCommand>? command = null)
        {
            var fixture = new Fixture();
            var operation = fixture.DispatchableOperation();
            fixture.Operations.Add(operation);
            fixture.ReservationReader.Fragments = [fixture.Fragment(operation)];
            mutate(fixture, operation);
            await FluentActions.Awaiting(async () => await fixture.Service.DispatchAsync(
                    command?.Invoke(fixture, operation) ?? fixture.DispatchCommand(operation)))
                .Should().ThrowAsync<InvalidOperationException>();
        }

        await RejectAsync((fixture, operation) => fixture.Operations.Update(
            operation.Transition(PayoutOperationState.Dispatching, Now.AddTicks(1), "snapshot"), operation.Version));
        await RejectAsync((_, _) => { }, (fixture, operation) => fixture.DispatchCommand(operation) with
        {
            ExpectedVersion = operation.Version + 1
        });
        await RejectAsync((fixture, _) => fixture.Policies.Policy = fixture.Policies.Policy! with { Version = 4 });
        await RejectAsync((fixture, _) => fixture.Reserves.Head = fixture.Reserves.Head with { Version = new ReserveVersion(5) });
        await RejectAsync((fixture, _) => fixture.Reserves.Head = fixture.Reserves.Head with { AuthorizationEpoch = 8 });
        await RejectAsync((fixture, _) => fixture.Provider.Account = fixture.Provider.Account with { ProviderAccountId = "acct_changed" });
        await RejectAsync((fixture, _) => fixture.ReservationReader.Fragments = []);
        await RejectAsync((fixture, operation) => fixture.ReservationReader.Fragments =
            [fixture.Fragment(operation) with { Amount = new CoinAmount(CurrencyCode.HardCoin, 699) }]);
    }

    [Fact]
    public async Task DispatchAndReconciliationCommandsRequireCompleteScopedIdentity()
    {
        var fixture = new Fixture();
        var operation = fixture.DispatchableOperation();
        fixture.Operations.Add(operation);
        fixture.ReservationReader.Fragments = [fixture.Fragment(operation)];

        foreach (var command in new[]
                 {
                     fixture.DispatchCommand(operation) with { ActorId = Guid.Empty },
                     fixture.DispatchCommand(operation) with { OperationId = Guid.Empty },
                     fixture.DispatchCommand(operation) with { ExpectedVersion = 0 },
                     fixture.DispatchCommand(operation) with { Reauthentication = null! }
                 })
            await FluentActions.Awaiting(async () => await fixture.Service.DispatchAsync(command))
                .Should().ThrowAsync<ArgumentException>();

        await FluentActions.Awaiting(async () => await fixture.Service.ReconcileAsync(
                new ReconcilePayoutOperationCommand(fixture.TenantId, fixture.ActorId, Guid.Empty)))
            .Should().ThrowAsync<ArgumentException>();

        var dispatchingWithoutProviderId = operation.Transition(
            PayoutOperationState.Dispatching, Now.AddTicks(1), "snapshot");
        fixture.Operations.Update(dispatchingWithoutProviderId, operation.Version);
        await FluentActions.Awaiting(async () => await fixture.Service.ReconcileAsync(
                new ReconcilePayoutOperationCommand(fixture.TenantId, fixture.ActorId, operation.Id)))
            .Should().ThrowAsync<PayoutStaleCommandException>();
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid PayeeId { get; } = Guid.NewGuid();
        public WalletId WalletId { get; } = WalletId.New();
        public PayoutRequest Request { get; }
        public RecordingRequestStore Requests { get; }
        public InMemoryPayoutOperationStore Operations { get; } = new();
        public RecordingWalletDirectory Wallets { get; }
        public RecordingPolicyStore Policies { get; }
        public RecordingSignatureVerifier Signatures { get; } = new();
        public RecordingReserveControlPlane Reserves { get; }
        public RecordingFencingTokens FencingTokens { get; } = new();
        public RecordingReservationReader ReservationReader { get; } = new();
        public RecordingProvider Provider { get; }
        public RecordingJurisdictionResolver Jurisdictions { get; } = new();
        public RecordingReservationWorkflow ReservationWorkflow { get; } = new();
        public RecordingSettlementWorkflow SettlementWorkflow { get; } = new();
        public DurablePayoutApplicationService Service { get; }

        public Fixture()
        {
            Request = new PayoutRequest(
                Guid.NewGuid(), new IdempotencyKey("approved-request"), Hash("self-request"),
                PayeeId, WalletId, new CoinAmount(CurrencyCode.HardCoin, 700),
                PayoutRequestState.Approved, 3, Now.AddMinutes(-20), Now.AddMinutes(-10),
                Guid.NewGuid(), TenantId);
            Requests = new RecordingRequestStore(Request, TenantId);
            Wallets = new RecordingWalletDirectory(TenantId, PayeeId, WalletId);
            Policies = new RecordingPolicyStore(CreatePolicy(TenantId));
            Reserves = new RecordingReserveControlPlane(CreateReserve());
            Provider = new RecordingProvider(ReadyAccount(PayeeId));
            Service = new DurablePayoutApplicationService(
                Requests, Operations, Wallets, Policies, Signatures, Reserves, FencingTokens,
                ReservationReader, Provider, Jurisdictions, ReservationWorkflow, SettlementWorkflow,
                new FixedTimeProvider(Now));
        }

        public ReserveApprovedPayoutCommand ReserveCommand() => new(
            TenantId, ActorId, Request.Id,
            Reauthentication(PayoutProtectedOperationBinding.Reservation(Request.Id)));

        public DispatchPayoutOperationCommand DispatchCommand(PayoutOperation operation) => new(
            TenantId, ActorId, operation.Id, operation.Version,
            Reauthentication(PayoutProtectedOperationBinding.Dispatch(operation.Id, operation.Version)));

        public ReauthenticationEvidence Reauthentication(string binding) => new(
            ActorId, ProtectedOperationKind.Payout, binding,
            ReauthenticationAssurance.MultiFactor, Now.AddMinutes(-1), Now.AddMinutes(4), Hash("reauth"));

        public PayoutOperation DispatchableOperation() => new(
            Guid.NewGuid(), new IdempotencyKey("dispatchable-operation"), Hash("request"),
            ActorId, PayeeId, WalletId, new CoinAmount(CurrencyCode.HardCoin, 700),
            Provider.Account.ProviderAccountId, Provider.Account.DestinationHash,
            Hash("provider-binding"), Hash("eligibility"), null, null,
            PayoutOperationState.Reserved, 1, 91, 3, new ReserveVersion(4), 7,
            new PolicyVersion(3), Guid.NewGuid(), Now, Now, TenantId);

        public PersistedFragmentReservation Fragment(PayoutOperation operation)
        {
            var root = SourceStampId.New();
            return new PersistedFragmentReservation(
                Guid.NewGuid(), operation.Id, CreditLotId.New(), root, 0,
                new RootTraceRange(root, 0, operation.Amount.Units * 1000, 0), operation.Amount);
        }

        public PayoutProviderEvent ProviderEvent(PayoutOperation operation, PayoutProviderOutcome outcome) => new(
            $"evt_{Guid.NewGuid():N}", operation.Id, outcome, "po_123",
            operation.ProviderAccountId, operation.DestinationHash, Hash("provider-event"),
            "signature", Now.AddMinutes(3));
    }

    private static EconomyCapabilityPolicy CreatePolicy(Guid tenantId)
    {
        const string payload = "{\"maximumAmountUnits\":1000000,\"minimumAmountUnits\":100,\"providerHash\":\"stripe-connect-provider\"}";
        return new EconomyCapabilityPolicy(
            Guid.NewGuid(), $"{tenantId:N}:9:BR", tenantId,
            EconomyValueMovementCapability.PayoutExecution, "BR", 3, payload, Hash(payload),
            "key", "signature", Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(-2), Now.AddHours(-1),
            Now.AddHours(-1), Now.AddHours(1), true, EconomyCapabilityPolicyState.Active);
    }

    private static ReserveHead CreateReserve() => new(
        new ReserveVersion(4), new PolicyVersion(8), 7, Now.AddMinutes(-2), Now.AddMinutes(10),
        new ReserveRequirementSnapshot(1, 1, 1, 1, 1), 10_000, 10_000,
        ReserveCoverageState.Covered, [], Hash("reserve"));

    private static ConnectAccountSnapshot ReadyAccount(Guid payeeId) => new(
        payeeId, "acct_123", Hash("destination"), ConnectAccountState.Ready, true, true,
        2, Now.AddMinutes(-2), Now.AddMinutes(10), Hash("account"));

    private sealed class RecordingRequestStore : IPayoutRequestStore
    {
        public RecordingRequestStore(PayoutRequest request, Guid tenantId)
        {
            Request = request;
            Audit =
            [
                new PayoutRequestReviewAuditEvent(Guid.NewGuid(), request.Id, tenantId,
                    request.FirstApprovalActorId!.Value, PayoutRequestState.Approved,
                    "first approval", Now.AddMinutes(-12)),
                new PayoutRequestReviewAuditEvent(Guid.NewGuid(), request.Id, tenantId,
                    Guid.NewGuid(), PayoutRequestState.Approved,
                    "second approval", Now.AddMinutes(-10))
            ];
        }

        public PayoutRequest Request { get; set; }
        public IReadOnlyList<PayoutRequestReviewAuditEvent> Audit { get; set; }
        public PayoutRequest GetForReview(Guid requestId, Guid tenantId) => Request;
        public IReadOnlyList<PayoutRequestReviewAuditEvent> ListReviewAudit(Guid requestId, Guid tenantId) => Audit;
        public PayoutRequest? FindReplay(Guid tenantId, Guid payeeId, string idempotencyKey, string requestHash) => throw new NotSupportedException();
        public void Add(PayoutRequest request) => throw new NotSupportedException();
        public PayoutRequest GetForPayee(Guid tenantId, Guid requestId, Guid payeeId) => throw new NotSupportedException();
        public IReadOnlyList<PayoutRequest> ListForPayee(Guid tenantId, Guid payeeId, int take) => throw new NotSupportedException();
        public IReadOnlyList<PayoutRequest> ListForReview(Guid tenantId, int take) => throw new NotSupportedException();
        public PayoutRequest Update(PayoutRequest request, long expectedVersion) => throw new NotSupportedException();
        public PayoutRequest Review(PayoutRequest request, long expectedVersion, Guid tenantId, Guid reviewerId, PayoutRequestState outcome, string reason) => throw new NotSupportedException();
    }

    private sealed class RecordingWalletDirectory(Guid tenantId, Guid ownerId, WalletId walletId) : IEconomyWalletDirectory
    {
        public Guid OwnerId { get; set; } = ownerId;
        public List<(Guid TenantId, Guid OwnerId)> OwnerLookups { get; } = [];
        public ValueTask<EconomyWalletIdentity> GetOwnerWalletAsync(Guid requestedTenantId, Guid requestedOwnerId, CancellationToken cancellationToken = default)
        {
            OwnerLookups.Add((requestedTenantId, requestedOwnerId));
            return ValueTask.FromResult(new EconomyWalletIdentity(walletId, tenantId, OwnerId, WalletLifecycleState.Active));
        }
        public ValueTask<EconomyWalletIdentity> GetWalletAsync(Guid requestedTenantId, WalletId requestedWalletId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new EconomyWalletIdentity(walletId, tenantId, OwnerId, WalletLifecycleState.Active));
    }

    private sealed class RecordingPolicyStore(EconomyCapabilityPolicy? policy) : IEconomyCapabilityPolicyStore
    {
        public EconomyCapabilityPolicy? Policy { get; set; } = policy;
        public bool MissTenantPolicy { get; set; }
        public List<Guid?> Lookups { get; } = [];
        public ValueTask<EconomyCapabilityPolicy?> CurrentAsync(Guid? tenantId, EconomyValueMovementCapability capability, string jurisdictionCode, CancellationToken cancellationToken)
        {
            Lookups.Add(tenantId);
            return ValueTask.FromResult(tenantId.HasValue && MissTenantPolicy ? null : Policy);
        }
        public ValueTask<EconomyCapabilityPolicy> ProposeAsync(EconomyCapabilityPolicyProposal proposal, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy> ApproveAsync(Guid policyId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingSignatureVerifier : ICapabilityPolicySignatureVerifier
    {
        public bool Valid { get; set; } = true;
        public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken) => ValueTask.FromResult(Valid);
    }

    private sealed class RecordingReserveControlPlane(ReserveHead head) : IEconomyReserveCustodyControlPlane
    {
        public ReserveHead Head { get; set; } = head;
        public ValueTask<ReserveHead> CurrentHeadAsync(DateTimeOffset now, CancellationToken cancellationToken) => ValueTask.FromResult(Head);
        public ValueTask<DurableCustodyObservation> IngestObservationAsync(CustodyObservationCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyLiabilitySnapshot> CalculateLiabilitiesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<DurableReserveProposalState> ProposeAsync(DurableReserveProposalCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ReserveHead> ApproveAndActivateAsync(Guid proposalId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ReservePostingAuthorization> AuthorizeAsync(ReserveVersion version, long authorizationEpoch, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingFencingTokens : IPayoutFencingTokenAllocator
    {
        public ValueTask<long> AllocateAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(91L);
    }

    private sealed class RecordingReservationReader : IFifoFragmentReservationReader
    {
        public IReadOnlyList<PersistedFragmentReservation> Fragments { get; set; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Read(Guid operationId, PersistedFragmentReservationStatus status) => Fragments;
    }

    private sealed class RecordingProvider(ConnectAccountSnapshot account) : IConnectPayoutProvider
    {
        public ConnectAccountSnapshot Account { get; set; } = account;
        public bool ThrowOnRead { get; set; }
        public PayoutProviderEvent ReconciliationEvent { get; set; } = null!;
        public ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(Guid payeeId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ConnectOnboardingResult(Account, new Uri("https://connect.example/onboard")));
        public ValueTask<ConnectAccountSnapshot> GetAccountAsync(Guid payeeId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead) throw new InvalidOperationException("provider should not be read");
            return ValueTask.FromResult(Account);
        }
        public ValueTask<PayoutDispatchReceipt> DispatchAsync(PayoutDispatchCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PayoutProviderEvent> ReconcileAsync(Guid operationId, string providerPayoutId, CancellationToken cancellationToken = default) => ValueTask.FromResult(ReconciliationEvent);
    }

    private sealed class RecordingJurisdictionResolver : IEconomyJurisdictionResolver
    {
        public List<Guid> Subjects { get; } = [];

        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId,
            Guid actorId,
            string? providerJurisdiction,
            string? destinationJurisdiction,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken)
        {
            Subjects.Add(actorId);
            return ValueTask.FromResult(new EconomyJurisdictionResolution(
                "BR", 1, 1, Hash("jurisdiction-evidence")));
        }
    }

    private sealed class RecordingReservationWorkflow : IDurablePayoutReservationWorkflow
    {
        public List<DurablePayoutReservationRequest> Requests { get; } = [];
        public Task<PayoutOperation> ReserveAsync(DurablePayoutReservationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(request.Operation with
            {
                KillSwitchEpoch = 3,
                RiskDecisionId = Guid.NewGuid()
            });
        }
    }

    private sealed class RecordingSettlementWorkflow : IDurablePayoutSettlementWorkflow
    {
        public List<DurablePayoutDispatchRequest> Dispatches { get; } = [];
        public List<DurablePayoutProviderEventRequest> ProviderEvents { get; } = [];
        public Task<PayoutOperation> BeginDispatchAsync(DurablePayoutDispatchRequest request, CancellationToken cancellationToken = default)
        {
            Dispatches.Add(request);
            return Task.FromResult(new PayoutOperation(
                request.OperationId, new IdempotencyKey("dispatch-result"), Hash("request"),
                Guid.NewGuid(), Guid.NewGuid(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
                "acct", Hash("destination"), Hash("binding"), Hash("eligibility"), Hash("dispatch-snapshot"),
                null, PayoutOperationState.Dispatching, request.ExpectedVersion + 1, request.FencingToken,
                request.KillSwitchEpoch, new ReserveVersion(1), 1, new PolicyVersion(1), Guid.NewGuid(),
                request.OccurredAt, request.OccurredAt, Guid.NewGuid()));
        }
        public Task<PayoutOperation> ApplyProviderEventAsync(DurablePayoutProviderEventRequest request, CancellationToken cancellationToken = default)
        {
            ProviderEvents.Add(request);
            var state = request.ProviderEvent.Outcome == PayoutProviderOutcome.Succeeded
                ? PayoutOperationState.Succeeded
                : PayoutOperationState.Failed;
            return Task.FromResult(new PayoutOperation(
                request.ProviderEvent.OperationId, new IdempotencyKey("provider-result"), Hash("request"),
                Guid.NewGuid(), Guid.NewGuid(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
                request.ProviderEvent.ProviderAccountId, request.ProviderEvent.DestinationHash,
                Hash("binding"), Hash("eligibility"), "snapshot", request.ProviderEvent.ProviderPayoutId,
                state, 3, 1, 0, new ReserveVersion(1), 1, new PolicyVersion(1), Guid.NewGuid(),
                Now, request.ProviderEvent.ObservedAt, Guid.NewGuid()));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
