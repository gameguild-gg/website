using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class DurablePayoutSettlementWorkflowTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 10, 19, 0, 0, TimeSpan.Zero);
    private static readonly Guid DecisionId = Guid.Parse("93000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task BeginDispatch_TransitionsAFencedReservationAndAllowsOnlyTheExactRetry()
    {
        var operation = CreateOperation();
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var context = new RecordingContext();
        var orchestrator = new RecordingProtectedOrchestrator(operation);
        var evidence = new RecordingAuthorizationEvidenceWriter();
        var workflow = CreateWorkflow(context, store, orchestrator: orchestrator, authorizationEvidence: evidence);
        var request = CreateDispatchRequest(operation);

        var dispatching = await workflow.BeginDispatchAsync(request);

        dispatching.State.Should().Be(PayoutOperationState.Dispatching);
        dispatching.Version.Should().Be(2);
        dispatching.DispatchSnapshotHash.Should().HaveLength(64);
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var replay = await workflow.BeginDispatchAsync(request);
        replay.Should().BeEquivalentTo(dispatching);
        context.Transactions.Should().HaveCount(2);
        context.Transactions[1].CommitCalled.Should().BeTrue();
        orchestrator.Intents.Should().ContainSingle().Which.ProtectedSubjectId.Should().Be(operation.PayeeId);
        evidence.Records.Should().ContainSingle().Which.Should().Match<PayoutAuthorizationEvidence>(item =>
            item.OperationId == operation.Id &&
            item.TenantId == operation.TenantId &&
            item.ActorId == operation.ActorId &&
            item.Phase == PayoutAuthorizationPhase.Dispatch &&
            item.ReauthenticationEvidenceHash == new string('a', 64) &&
            item.OperationFingerprintHash.Length == 64 &&
            item.CapabilityReceiptHash == "receipt-hash");

        var stale = request with { ExpectedVersion = operation.Version + 1 };
        await FluentActions.Invoking(() => workflow.BeginDispatchAsync(stale))
            .Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task BeginDispatch_RollsBackWhenTheReservedFragmentsWereAlreadyMoved()
    {
        var operation = CreateOperation();
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var context = new RecordingContext();
        var workflow = CreateWorkflow(
            context, store, new RecordingReservations(transitionCount: 0),
            orchestrator: new RecordingProtectedOrchestrator(operation));

        await FluentActions.Invoking(() => workflow.BeginDispatchAsync(CreateDispatchRequest(operation)))
            .Should().ThrowAsync<PayoutStaleCommandException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ProviderEvent_ConsumesReservedFragmentsAndRecordsAnIdempotentSuccess()
    {
        var operation = CreateOperation().Transition(
            PayoutOperationState.Dispatching,
            Time.AddMinutes(1),
            dispatchSnapshotHash: "dispatch-snapshot");
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var context = new RecordingContext();
        var reservations = new RecordingReservations();
        var postings = new RecordingPostings();
        var workflow = CreateWorkflow(context, store, reservations, postings);
        var providerEvent = CreateProviderEvent(operation, PayoutProviderOutcome.Succeeded);

        var settled = await workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(providerEvent));

        settled.State.Should().Be(PayoutOperationState.Succeeded);
        settled.Version.Should().Be(3);
        settled.ProviderPayoutId.Should().Be(providerEvent.ProviderPayoutId);
        reservations.Transitions.Should().ContainSingle().Which.Should().Be(new ReservationTransition(
            operation.Id,
            PersistedFragmentReservationStatus.Dispatching,
            PersistedFragmentReservationStatus.Consumed,
            providerEvent.ObservedAt));
        postings.Requests.Should().ContainSingle();
        postings.Requests[0].Posting.Template.Kind.Should().Be(PostingTemplateKind.PayoutSuccess);
        postings.Authorities.Issued.Should().ContainSingle();
        postings.Authorities.Consumed.Should().ContainSingle();
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var replay = await workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(providerEvent));
        replay.Should().BeEquivalentTo(settled);
        postings.Requests.Should().ContainSingle();
        reservations.Transitions.Should().ContainSingle();
    }

    [Fact]
    public async Task ProviderEvent_RejectsUnboundEvidenceBeforeAnyPostingOrFragmentMutation()
    {
        var operation = CreateOperation().Transition(
            PayoutOperationState.Dispatching,
            Time.AddMinutes(1),
            dispatchSnapshotHash: "dispatch-snapshot");
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var context = new RecordingContext();
        var reservations = new RecordingReservations();
        var postings = new RecordingPostings();
        var workflow = CreateWorkflow(context, store, reservations, postings);
        var invalidEvent = CreateProviderEvent(operation, PayoutProviderOutcome.Failed) with { DestinationHash = "wrong" };

        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                invalidEvent)))
            .Should().ThrowAsync<PayoutProviderBindingException>();

        var invalidAccount = CreateProviderEvent(operation, PayoutProviderOutcome.Failed) with
        {
            ProviderAccountId = "wrong"
        };
        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                invalidAccount)))
            .Should().ThrowAsync<PayoutProviderBindingException>();

        postings.Requests.Should().BeEmpty();
        reservations.Transitions.Should().BeEmpty();
        context.Transactions.Should().HaveCount(2).And.OnlyContain(transaction => transaction.RollbackCalled);
    }

    [Fact]
    public async Task ProviderEvent_RejectsAnAuthorityNotBoundToTheOperationTenantAndActor()
    {
        var operation = CreateOperation().Transition(
            PayoutOperationState.Dispatching,
            Time.AddMinutes(1),
            dispatchSnapshotHash: "dispatch-snapshot");
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var context = new RecordingContext();
        var authorityIssuer = new RecordingProviderAuthorityIssuer { ReturnUnboundAuthority = true };
        var workflow = CreateWorkflow(context, store, authorityIssuer: authorityIssuer);

        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                CreateProviderEvent(operation, PayoutProviderOutcome.Failed))))
            .Should().ThrowAsync<PayoutEvidenceException>();
    }

    [Fact]
    public async Task DispatchAndProviderEvent_RejectMalformedRequestsBeforeTheyCanChangeAPayout()
    {
        var operation = CreateOperation();
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, new InMemoryPayoutOperationStore());

        var validDispatch = CreateDispatchRequest(operation);
        foreach (var invalid in new[]
                 {
                     validDispatch with { OperationId = Guid.Empty },
                     validDispatch with { ActorId = Guid.Empty },
                     validDispatch with { JurisdictionCode = " " },
                     validDispatch with { ReauthenticationEvidenceHash = " " },
                     validDispatch with { ReauthenticationEvidenceHash = "too-short" },
                     validDispatch with { ProviderHash = " " },
                     validDispatch with { SourceRoots = null! },
                     validDispatch with { SourceRoots = [] }
                 })
            await FluentActions.Invoking(() => workflow.BeginDispatchAsync(invalid))
                .Should().ThrowAsync<ArgumentException>();
        foreach (var invalid in new[]
                 {
                     validDispatch with { ExpectedVersion = 0 },
                     validDispatch with { FencingToken = 0 },
                     validDispatch with { KillSwitchEpoch = -1 }
                 })
            await FluentActions.Invoking(() => workflow.BeginDispatchAsync(invalid))
                .Should().ThrowAsync<ArgumentOutOfRangeException>();

        var eventWithNoOperation = CreateProviderEvent(operation, PayoutProviderOutcome.Failed) with { OperationId = Guid.Empty };
        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                eventWithNoOperation)))
            .Should().ThrowAsync<ArgumentException>();
        var nonTerminalEvent = CreateProviderEvent(operation, PayoutProviderOutcome.Failed) with { Outcome = PayoutProviderOutcome.Submitted };
        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                nonTerminalEvent)))
            .Should().ThrowAsync<PayoutEvidenceException>();

        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task BeginDispatch_AtomicallyRejectsEveryUnboundCapabilityReceipt()
    {
        foreach (var (binding, mutate) in new (string, Func<CapabilityAuthorizationReceipt, CapabilityAuthorizationReceipt>)[]
                 {
                     ("tenant", receipt => receipt with { TenantId = Guid.NewGuid() }),
                     ("actor", receipt => receipt with { ActorId = Guid.NewGuid() }),
                     ("subject", receipt => receipt with { SubjectReference = "other-subject" }),
                     ("jurisdiction", receipt => receipt with { JurisdictionCode = "OTHER" }),
                     ("risk", receipt => receipt with { RiskDecisionId = Guid.NewGuid() }),
                     ("policy", receipt => receipt with { PolicyVersion = 2 }),
                     ("reserve", receipt => receipt with { ReserveVersion = 2 }),
                     ("kill-switch", receipt => receipt with { KillSwitchEpoch = 4 }),
                     ("provider", receipt => receipt with { ProviderHash = "other-provider" }),
                     ("destination", receipt => receipt with { DestinationHash = "other-destination" }),
                     ("source-root", receipt => receipt with { SourceRootHashes = ["other-root"] })
                 })
        {
            var operation = CreateOperation();
            var store = new InMemoryPayoutOperationStore();
            store.Add(operation);
            var context = new RecordingContext();
            var orchestrator = new RecordingProtectedOrchestrator(operation) { MutateReceipt = mutate };
            var workflow = CreateWorkflow(context, store, orchestrator: orchestrator);

            await FluentActions.Invoking(() => workflow.BeginDispatchAsync(CreateDispatchRequest(operation)))
                .Should().ThrowAsync<PayoutStaleCommandException>("the {0} binding changed", binding);

            store.Get(operation.Id).State.Should().Be(PayoutOperationState.Reserved);
            context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ProviderEvent_RejectsUnorderedOrPredatedTerminalEvidence()
    {
        var reserved = CreateOperation();
        var reservedStore = new InMemoryPayoutOperationStore();
        reservedStore.Add(reserved);
        var reservedContext = new RecordingContext();
        var reservedWorkflow = CreateWorkflow(reservedContext, reservedStore);

        await FluentActions.Invoking(() => reservedWorkflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                CreateProviderEvent(reserved, PayoutProviderOutcome.Failed))))
            .Should().ThrowAsync<PayoutStaleCommandException>();

        var dispatching = reserved.Transition(PayoutOperationState.Dispatching, Time.AddMinutes(1), dispatchSnapshotHash: "snapshot");
        var dispatchingStore = new InMemoryPayoutOperationStore();
        dispatchingStore.Add(dispatching);
        var dispatchingContext = new RecordingContext();
        var dispatchingWorkflow = CreateWorkflow(dispatchingContext, dispatchingStore);
        var predatedEvent = CreateProviderEvent(dispatching, PayoutProviderOutcome.Failed) with { ObservedAt = Time.AddMinutes(-1) };

        await FluentActions.Invoking(() => dispatchingWorkflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                predatedEvent)))
            .Should().ThrowAsync<PayoutEvidenceException>();

        reservedContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
        dispatchingContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ProviderEvent_RejectsInvalidEvidenceBeforeOpeningTheSettlementTransaction()
    {
        var operation = CreateOperation().Transition(PayoutOperationState.Dispatching, Time.AddMinutes(1), dispatchSnapshotHash: "snapshot");
        var context = new RecordingContext();
        var workflow = CreateWorkflow(context, new InMemoryPayoutOperationStore(), evidence: new RejectProviderEvidence());

        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                CreateProviderEvent(operation, PayoutProviderOutcome.Failed))))
            .Should().ThrowAsync<PayoutEvidenceException>();

        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderEvent_ReturnsTheConcurrentReplayAndRollsBackWithoutPosting()
    {
        var operation = CreateOperation().Transition(PayoutOperationState.Dispatching, Time.AddMinutes(1), dispatchSnapshotHash: "snapshot");
        var context = new RecordingContext();
        var reservations = new RecordingReservations();
        var postings = new RecordingPostings();
        var workflow = CreateWorkflow(context, new ReplayOnSecondProviderEventLookupStore(operation), reservations, postings);

        var replay = await workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
            CreateProviderEvent(operation, PayoutProviderOutcome.Failed)));

        replay.Should().BeSameAs(operation);
        reservations.Transitions.Should().BeEmpty();
        postings.Requests.Should().BeEmpty();
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ProviderEvent_RollsBackWhenNoReservedFragmentCanBeTransitioned()
    {
        var operation = CreateOperation().Transition(PayoutOperationState.Dispatching, Time.AddMinutes(1), dispatchSnapshotHash: "snapshot");
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var context = new RecordingContext();
        var reservations = new RecordingReservations(transitionCount: 0);
        var postings = new RecordingPostings();
        var workflow = CreateWorkflow(context, store, reservations, postings);

        await FluentActions.Invoking(() => workflow.ApplyProviderEventAsync(new DurablePayoutProviderEventRequest(
                CreateProviderEvent(operation, PayoutProviderOutcome.Failed))))
            .Should().ThrowAsync<PayoutStaleCommandException>();

        postings.Requests.Should().ContainSingle();
        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    private static PostgreSqlDurablePayoutSettlementWorkflow CreateWorkflow(
        RecordingContext context,
        IPayoutOperationStore store,
        RecordingReservations? reservations = null,
        RecordingPostings? postings = null,
        IPayoutProviderEvidenceVerifier? evidence = null,
        RecordingProviderAuthorityIssuer? authorityIssuer = null,
        RecordingProtectedOrchestrator? orchestrator = null,
        RecordingAuthorizationEvidenceWriter? authorizationEvidence = null)
    {
        var postingsGateway = postings ?? new RecordingPostings();
        postingsGateway.Authorities = authorityIssuer ?? new RecordingProviderAuthorityIssuer();
        return new PostgreSqlDurablePayoutSettlementWorkflow(
            context,
            store,
            reservations ?? new RecordingReservations(),
            orchestrator ?? new RecordingProtectedOrchestrator(null),
            authorizationEvidence ?? new RecordingAuthorizationEvidenceWriter(),
            postingsGateway,
            postingsGateway.Authorities,
            evidence ?? new AcceptProviderEvidence(),
            new RecordingDispatchOutbox());
    }

    private static DurablePayoutDispatchRequest CreateDispatchRequest(PayoutOperation operation) => new(
        operation.Id,
        operation.ActorId,
        operation.Version,
        operation.FencingToken,
        operation.KillSwitchEpoch,
        "BR",
        new string('a', 64),
        "provider-hash",
        [SourceStampId.New()],
        Time.AddMinutes(1));

    private static PayoutOperation CreateOperation() => new(
        Guid.NewGuid(),
        new IdempotencyKey($"payout-{Guid.NewGuid():N}"),
        "request-hash",
        Guid.NewGuid(),
        Guid.NewGuid(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 700),
        "acct_1",
        "destination-hash",
        "binding-hash",
        "eligibility-hash",
        null,
        null,
        PayoutOperationState.Reserved,
        1,
        8,
        3,
        new ReserveVersion(1),
        1,
        new PolicyVersion(1),
        Guid.NewGuid(),
        Time,
        Time,
        Guid.NewGuid());

    private static PayoutProviderEvent CreateProviderEvent(PayoutOperation operation, PayoutProviderOutcome outcome) => new(
        $"evt_{Guid.NewGuid():N}",
        operation.Id,
        outcome,
        "provider-payout",
        operation.ProviderAccountId,
        operation.DestinationHash,
        "provider-evidence",
        "signature",
        Time.AddMinutes(2));

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

    private sealed class RecordingReservations(long transitionCount = 1) : IFifoFragmentReservationGateway
    {
        public List<ReservationTransition> Transitions { get; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request) => [];
        public long Transition(Guid operationId, PersistedFragmentReservationStatus expected, PersistedFragmentReservationStatus next, DateTimeOffset terminalAt)
        {
            Transitions.Add(new ReservationTransition(operationId, expected, next, terminalAt));
            return transitionCount;
        }
    }

    private sealed class RecordingPostings : IRegisteredPostingGateway
    {
        public List<RegisteredPostingRequest> Requests { get; } = [];
        public RecordingProviderAuthorityIssuer Authorities { get; set; } = new();
        public RegisteredPostingReceipt Post(RegisteredPostingRequest request)
        {
            Requests.Add(request);
            return new RegisteredPostingReceipt(request.Posting.Id, 1, "journal-hash", false);
        }
    }

    private sealed class AcceptProviderEvidence : IPayoutProviderEvidenceVerifier
    {
        public bool Verify(PayoutDispatchReceipt receipt) => true;
        public bool Verify(PayoutProviderEvent providerEvent) => true;
    }

    private sealed class RecordingAuthorizationEvidenceWriter : IPayoutAuthorizationEvidenceWriter
    {
        public List<PayoutAuthorizationEvidence> Records { get; } = [];

        public Task AppendAsync(
            PayoutAuthorizationEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            Records.Add(evidence);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProtectedOrchestrator(PayoutOperation? operation)
        : IEconomyProtectedOperationOrchestrator
    {
        public List<EconomyProtectedOperationIntent> Intents { get; } = [];
        public Func<CapabilityAuthorizationReceipt, CapabilityAuthorizationReceipt>? MutateReceipt { get; init; }

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> protectedOperation,
            CancellationToken cancellationToken)
        {
            var current = operation ?? throw new InvalidOperationException("A dispatch operation is required.");
            Intents.Add(intent);
            var fingerprint = "server-issued-dispatch-fingerprint";
            var rootHashes = intent.SourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), current.TenantId, current.ActorId,
                EconomySubjectReference.ForUser(current.TenantId, current.PayeeId),
                "BR", intent.Capability, fingerprint,
                current.PolicyVersion.Value, current.ReserveVersion.Value, DecisionId,
                current.KillSwitchEpoch, intent.ProviderReferenceHash, intent.DestinationHash,
                rootHashes, ["evidence"], Time, Time.AddMinutes(5), "receipt-hash",
                "key", "signature");
            receipt = MutateReceipt?.Invoke(receipt) ?? receipt;
            return await protectedOperation(new EconomyProtectedOperationAuthorization(
                current.TenantId, current.ActorId, "BR", DecisionId, fingerprint, receipt),
                cancellationToken);
        }

        private static string Hash(string value) => Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }

    private sealed class RecordingProviderAuthorityIssuer :
        IProviderEvidencePostingAuthorityIssuer
    {
        public List<ProviderEvidencePostingAuthorityRequest> Issued { get; } = [];
        public List<RegisteredPostingAuthority> Consumed { get; } = [];
        public bool ReturnUnboundAuthority { get; init; }

        public ValueTask<RegisteredPostingAuthority> IssueAsync(
            ProviderEvidencePostingAuthorityRequest request,
            CancellationToken cancellationToken = default)
        {
            Issued.Add(request);
            var tenantId = ReturnUnboundAuthority ? Guid.NewGuid() : request.TenantId;
            var actorId = ReturnUnboundAuthority ? Guid.NewGuid() : request.ActorId;
            return ValueTask.FromResult(new RegisteredPostingAuthority(
                Guid.NewGuid(), actorId, tenantId, Guid.NewGuid(), request.OperationFingerprint, 1));
        }

        public ValueTask ConsumeAsync(
            RegisteredPostingAuthority authority,
            DateTimeOffset consumedAt,
            CancellationToken cancellationToken = default)
        {
            Consumed.Add(authority);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingDispatchOutbox : IPayoutDispatchOutboxWriter
    {
        public List<PayoutDispatchOutboxRow> Rows { get; } = [];

        public Task AddAsync(PayoutDispatchOutboxRow row, CancellationToken cancellationToken = default)
        {
            Rows.Add(row);
            return Task.CompletedTask;
        }
    }

    private sealed class RejectProviderEvidence : IPayoutProviderEvidenceVerifier
    {
        public bool Verify(PayoutDispatchReceipt receipt) => false;
        public bool Verify(PayoutProviderEvent providerEvent) => false;
    }

    private sealed class ReplayOnSecondProviderEventLookupStore(PayoutOperation operation) : IPayoutOperationStore
    {
        private int _providerEventLookups;

        public PayoutOperation Get(Guid operationId) => operation;
        public PayoutOperation GetForTenant(Guid tenantId, Guid operationId) => throw new NotSupportedException();
        public IReadOnlyList<PayoutOperation> ListForTenant(Guid tenantId, int take) => throw new NotSupportedException();
        public IReadOnlyList<PayoutOperation> ListForPayee(Guid tenantId, Guid payeeId, int take) => throw new NotSupportedException();
        public PayoutOperation? FindReplay(Guid tenantId, string idempotencyKey, string requestHash) => throw new NotSupportedException();
        public void Add(PayoutOperation payoutOperation) => throw new NotSupportedException();
        public PayoutOperation Update(PayoutOperation payoutOperation, long expectedVersion) => throw new NotSupportedException();
        public PayoutProviderEventRecord? FindProviderEvent(string eventId, string eventHash) => ++_providerEventLookups == 2
            ? new PayoutProviderEventRecord(eventId, eventHash, operation.Id, operation.State, Time)
            : null;
        public PayoutProviderEventRecord RecordProviderEvent(string eventId, string eventHash, PayoutOperation resultingOperation, long expectedVersion, DateTimeOffset recordedAt) => throw new NotSupportedException();
    }

    private sealed record ReservationTransition(
        Guid OperationId,
        PersistedFragmentReservationStatus Expected,
        PersistedFragmentReservationStatus Next,
        DateTimeOffset TerminalAt);
}
