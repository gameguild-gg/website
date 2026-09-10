using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class DurablePayoutReservationWorkflowTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid DecisionId = Guid.Parse("92000000-0000-0000-0000-000000000001");

    public static IEnumerable<object[]> InvalidOperations()
    {
        yield return ["missing operation ID", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { Id = Guid.Empty }), typeof(ArgumentException)];
        yield return ["missing tenant ID", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { TenantId = Guid.Empty }), typeof(ArgumentException)];
        yield return ["non-reserved state", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { State = PayoutOperationState.Dispatching }), typeof(InvalidOperationException)];
        yield return ["non-initial version", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { Version = 2 }), typeof(InvalidOperationException)];
        yield return ["non-hard currency", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 700) }), typeof(ArgumentException)];
        yield return ["zero amount", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { Amount = new CoinAmount(CurrencyCode.HardCoin, 0) }), typeof(ArgumentException)];
        yield return ["negative kill switch epoch", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { KillSwitchEpoch = -1 }), typeof(ArgumentOutOfRangeException)];
        yield return ["missing fencing token", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { FencingToken = 0 }), typeof(ArgumentException)];
        yield return ["missing reserve authorization epoch", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { ReserveAuthorizationEpoch = 0 }), typeof(ArgumentException)];
        yield return ["client risk decision", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { RiskDecisionId = Guid.NewGuid() }), typeof(InvalidOperationException)];
        yield return ["missing request hash", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { RequestHash = " " }), typeof(ArgumentException)];
        yield return ["missing provider account", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { ProviderAccountId = " " }), typeof(ArgumentException)];
        yield return ["missing destination", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { DestinationHash = " " }), typeof(ArgumentException)];
        yield return ["missing provider binding", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { ProviderBindingHash = " " }), typeof(ArgumentException)];
        yield return ["missing eligibility binding", (Func<PayoutOperation, PayoutOperation>)(operation => operation with { EligibilityHash = " " }), typeof(ArgumentException)];
    }

    [Fact]
    public async Task Reserve_CreatesTheFifoReservationAndImmutableLedgerPosting()
    {
        var operation = CreateOperation();
        var context = new RecordingContext();
        var reservations = new RecordingReservations(operation);
        var postings = new RecordingPostings();
        var evidence = new RecordingAuthorizationEvidenceWriter();
        var store = new InMemoryPayoutOperationStore();
        var workflow = CreateWorkflow(context, store, reservations, postings, evidence: evidence);

        var reserved = await workflow.ReserveAsync(CreateRequest(operation));

        var authorizedOperation = operation with { RiskDecisionId = DecisionId, KillSwitchEpoch = 3 };
        reserved.Should().BeEquivalentTo(authorizedOperation);
        store.Get(operation.Id).Should().BeEquivalentTo(authorizedOperation);
        reservations.Requests.Should().ContainSingle().Which.Should().Be(new FifoFragmentReservationRequest(
            operation.Id,
            operation.WalletId,
            CurrencyCode.HardCoin,
            ProvenanceKind.EarnedHard,
            operation.Amount,
            PersistedFragmentReservationPurpose.Payout,
            operation.CreatedAt));
        postings.Requests.Should().ContainSingle();
        var posting = postings.Requests[0];
        posting.Posting.Id.Should().Be(new PostingId(operation.Id));
        posting.Posting.Template.Kind.Should().Be(PostingTemplateKind.PayoutReservation);
        posting.Posting.Lines.Should().SatisfyRespectively(
            line => line.Should().Be(new PostingLine(1, EntrySide.Debit, EconomyAccountCode.EarnedHardLiability, operation.Amount, operation.WalletId, null, ProvenanceKind.EarnedHard)),
            line => line.Should().Be(new PostingLine(2, EntrySide.Credit, EconomyAccountCode.PayoutPayableHard, operation.Amount, null, null, null)));
        posting.Allocations.Should().ContainSingle().Which.Should().Match<RegisteredPostingAllocation>(
            allocation => allocation.LineSequence == 1 && allocation.AmountUnits == operation.Amount.Units);
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
        evidence.Records.Should().ContainSingle().Which.Should().Match<PayoutAuthorizationEvidence>(item =>
            item.OperationId == operation.Id &&
            item.TenantId == operation.TenantId &&
            item.ActorId == operation.ActorId &&
            item.Phase == PayoutAuthorizationPhase.Reservation &&
            item.RiskDecisionId == DecisionId &&
            item.ReauthenticationEvidenceHash == new string('a', 64) &&
            item.OperationFingerprintHash.Length == 64 &&
            item.CapabilityReceiptHash == "receipt-hash");
    }

    [Fact]
    public async Task Reserve_ReturnsThePriorOperationWithoutOpeningATransactionForAnIdempotentReplay()
    {
        var operation = CreateOperation();
        var context = new RecordingContext();
        var store = new InMemoryPayoutOperationStore();
        store.Add(operation);
        var workflow = CreateWorkflow(context, store, new RecordingReservations(operation), new RecordingPostings());

        var replay = await workflow.ReserveAsync(CreateRequest(operation));

        replay.Should().BeSameAs(operation);
        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Reserve_RollsBackAndReturnsTheWinnerWhenReplayAppearsInsideTheTransaction()
    {
        var operation = CreateOperation();
        var context = new RecordingContext();
        var reservations = new RecordingReservations(operation);
        var postings = new RecordingPostings();
        var store = new ReplayOnSecondLookupStore(operation);
        var workflow = CreateWorkflow(context, store, reservations, postings);

        var replay = await workflow.ReserveAsync(CreateRequest(operation));

        replay.Should().BeSameAs(operation);
        store.AddCalled.Should().BeFalse();
        reservations.Requests.Should().BeEmpty();
        postings.Requests.Should().BeEmpty();
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Reserve_RollsBackWithoutPostingWhenFifoFragmentsDoNotEqualThePayoutAmount()
    {
        var operation = CreateOperation();
        var context = new RecordingContext();
        var reservations = new RecordingReservations(operation, operation.Amount.Units - 1);
        var postings = new RecordingPostings();
        var workflow = CreateWorkflow(context, new InMemoryPayoutOperationStore(), reservations, postings);

        await FluentActions.Invoking(() => workflow.ReserveAsync(CreateRequest(operation)))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();

        postings.Requests.Should().BeEmpty();
        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidOperations))]
    public async Task Reserve_RejectsInvalidPayoutOperationsBeforeMutatingPersistence(
        string _,
        Func<PayoutOperation, PayoutOperation> mutate,
        Type expectedException)
    {
        var operation = mutate(CreateOperation());
        var context = new RecordingContext();
        var reservations = new RecordingReservations(operation);
        var postings = new RecordingPostings();
        var workflow = CreateWorkflow(context, new InMemoryPayoutOperationStore(), reservations, postings);

        var exception = await FluentActions.Invoking(() => workflow.ReserveAsync(CreateRequest(operation)))
            .Should().ThrowAsync<Exception>();

        exception.Which.Should().BeOfType(expectedException);
        context.Transactions.Should().BeEmpty();
        reservations.Requests.Should().BeEmpty();
        postings.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Reserve_RejectsCapabilityReceiptsAndAuthoritiesThatDoNotBindTheOperation()
    {
        var operation = CreateOperation();
        var context = new RecordingContext();
        var invalidReceipt = new RecordingProtectedOrchestrator(operation) { ReturnUnboundReceipt = true };
        var workflow = CreateWorkflow(context, new InMemoryPayoutOperationStore(),
            new RecordingReservations(operation), new RecordingPostings(), invalidReceipt);

        await FluentActions.Invoking(() => workflow.ReserveAsync(CreateRequest(operation)))
            .Should().ThrowAsync<InvalidOperationException>();

        var invalidJurisdiction = new RecordingProtectedOrchestrator(operation)
        {
            ReturnUnboundJurisdiction = true
        };
        workflow = CreateWorkflow(new RecordingContext(), new InMemoryPayoutOperationStore(),
            new RecordingReservations(operation), new RecordingPostings(), invalidJurisdiction);
        await FluentActions.Invoking(() => workflow.ReserveAsync(CreateRequest(operation)))
            .Should().ThrowAsync<InvalidOperationException>();

        var unboundAuthority = new RecordingPostingResolver { ReturnUnboundAuthority = true };
        workflow = CreateWorkflow(new RecordingContext(), new InMemoryPayoutOperationStore(),
            new RecordingReservations(operation), new RecordingPostings(), resolver: unboundAuthority);
        await FluentActions.Invoking(() => workflow.ReserveAsync(CreateRequest(operation)))
            .Should().ThrowAsync<InvalidOperationException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Reserve_RejectsTenantAndRiskBindingsInsideTheDurableRequest()
    {
        var operation = CreateOperation();
        var workflow = CreateWorkflow(
            new RecordingContext(),
            new InMemoryPayoutOperationStore(),
            new RecordingReservations(operation),
            new RecordingPostings());
        var validRequest = CreateRequest(operation);

        await FluentActions.Invoking(() => workflow.ReserveAsync(
                validRequest with { Operation = operation with { TenantId = Guid.Empty } }))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => workflow.ReserveAsync(
                validRequest with { Operation = operation with { RiskDecisionId = Guid.NewGuid() } }))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Invoking(() => workflow.ReserveAsync(
                validRequest with { ReauthenticationEvidenceHash = " " }))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => workflow.ReserveAsync(
                validRequest with { JurisdictionCode = " " }))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => workflow.ReserveAsync(
                validRequest with { ReauthenticationEvidenceHash = "too-short" }))
            .Should().ThrowAsync<ArgumentException>();
    }

    private static PostgreSqlDurablePayoutReservationWorkflow CreateWorkflow(
        RecordingContext context,
        IPayoutOperationStore store,
        RecordingReservations reservations,
        RecordingPostings postings,
        RecordingProtectedOrchestrator? orchestrator = null,
        RecordingPostingResolver? resolver = null,
        RecordingAuthorizationEvidenceWriter? evidence = null) => new(
        context,
        store,
        reservations,
        orchestrator ?? new RecordingProtectedOrchestrator(reservations.Operation),
        evidence ?? new RecordingAuthorizationEvidenceWriter(),
        resolver ?? new RecordingPostingResolver(),
        postings);

    private static DurablePayoutReservationRequest CreateRequest(PayoutOperation operation) => new(
        operation,
        "BR",
        new string('a', 64),
        "provider-hash");

    private static PayoutOperation CreateOperation() => new(
        Guid.NewGuid(),
        new IdempotencyKey($"payout-reservation-{Guid.NewGuid():N}"),
        "request-hash",
        Guid.NewGuid(),
        Guid.NewGuid(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 700),
        "acct_1",
        "destination-hash",
        "provider-binding-hash",
        "eligibility-hash",
        null,
        null,
        PayoutOperationState.Reserved,
        1,
        7,
        3,
        new ReserveVersion(1),
        1,
        new PolicyVersion(1),
        Guid.Empty,
        Time,
        Time,
        Guid.NewGuid());

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

    private sealed class RecordingReservations(PayoutOperation operation, long? amountUnits = null) : IFifoFragmentReservationGateway
    {
        public PayoutOperation Operation { get; } = operation;
        private readonly PersistedFragmentReservation _fragment = new(
            Guid.NewGuid(),
            operation.Id,
            CreditLotId.New(),
            SourceStampId.New(),
            0,
            new RootTraceRange(SourceStampId.New(), 0, checked(Math.Max(1, operation.Amount.Units) * 1000), 0),
            new CoinAmount(operation.Amount.Currency, amountUnits ?? operation.Amount.Units));

        public List<FifoFragmentReservationRequest> Requests { get; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request)
        {
            Requests.Add(request);
            return [_fragment];
        }
        public long Transition(Guid operationId, PersistedFragmentReservationStatus expected, PersistedFragmentReservationStatus next, DateTimeOffset terminalAt) => 1;
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

    private sealed class RecordingProtectedOrchestrator(PayoutOperation operation)
        : IEconomyProtectedOperationOrchestrator
    {
        public bool ReturnUnboundReceipt { get; init; }
        public bool ReturnUnboundJurisdiction { get; init; }

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> protectedOperation,
            CancellationToken cancellationToken)
        {
            var tenantId = ReturnUnboundReceipt ? Guid.NewGuid() : operation.TenantId;
            var jurisdiction = ReturnUnboundJurisdiction ? "OTHER" : "BR";
            var fingerprint = "server-issued-operation-fingerprint";
            var rootHashes = intent.SourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), tenantId, operation.ActorId,
                EconomySubjectReference.ForUser(operation.TenantId, operation.PayeeId),
                jurisdiction, intent.Capability, fingerprint,
                operation.PolicyVersion.Value, operation.ReserveVersion.Value, DecisionId, 3,
                intent.ProviderReferenceHash, intent.DestinationHash,
                rootHashes, ["evidence"], Time, Time.AddMinutes(5),
                "receipt-hash", "key", "signature");
            return await protectedOperation(new EconomyProtectedOperationAuthorization(
                tenantId, operation.ActorId, jurisdiction, DecisionId, fingerprint, receipt), cancellationToken);
        }

        private static string Hash(string value) => Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }

    private sealed class RecordingPostingResolver : IRegisteredPostingCapabilityResolver
    {
        public bool ReturnUnboundAuthority { get; init; }

        public Task<RegisteredPostingCapability> ResolveAsync(
            string capabilityName,
            PostingTemplateKind templateKind,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
            string capabilityName,
            PostingTemplateKind templateKind,
            CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegisteredPostingAuthority(
                Guid.NewGuid(), receipt.ActorId,
                ReturnUnboundAuthority ? Guid.NewGuid() : receipt.TenantId,
                receipt.RiskDecisionId, receipt.OperationFingerprint, 1));
    }

    private sealed class ReplayOnSecondLookupStore(PayoutOperation replay) : IPayoutOperationStore
    {
        private int _lookups;
        public bool AddCalled { get; private set; }
        public PayoutOperation Get(Guid operationId) => throw new NotSupportedException();
        public PayoutOperation GetForTenant(Guid tenantId, Guid operationId) => throw new NotSupportedException();
        public IReadOnlyList<PayoutOperation> ListForTenant(Guid tenantId, int take) => throw new NotSupportedException();
        public IReadOnlyList<PayoutOperation> ListForPayee(Guid tenantId, Guid payeeId, int take) => throw new NotSupportedException();
        public PayoutOperation? FindReplay(Guid tenantId, string idempotencyKey, string requestHash) => ++_lookups == 2 ? replay : null;
        public void Add(PayoutOperation operation) => AddCalled = true;
        public PayoutOperation Update(PayoutOperation operation, long expectedVersion) => throw new NotSupportedException();
        public PayoutProviderEventRecord? FindProviderEvent(string eventId, string eventHash) => throw new NotSupportedException();
        public PayoutProviderEventRecord RecordProviderEvent(string eventId, string eventHash, PayoutOperation resultingOperation, long expectedVersion, DateTimeOffset recordedAt) => throw new NotSupportedException();
    }
}
