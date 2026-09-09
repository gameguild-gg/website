using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PostgreSqlPayoutDispatchOutboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 21, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WriterAndProcessor_PersistSubmittedAmbiguousAndReplaySafeOutcomes()
    {
        await using var fixture = await Fixture.CreateAsync();

        var submitted = await fixture.AddAsync(PayoutOperationState.Dispatching);
        var submittedResult = await fixture.Processor(command =>
                Receipt(command, PayoutProviderOutcome.Submitted))
            .ProcessNextAsync(" worker ", Now);
        submittedResult.Should().NotBeNull();
        submittedResult!.OperationId.Should().Be(submitted.Command.OperationId);
        submittedResult.Outcome.Should().Be(PayoutProviderOutcome.Submitted);
        submittedResult.Processed.Should().BeTrue();
        submittedResult.AttemptCount.Should().Be(1);
        fixture.Store.Get(submitted.Command.OperationId).ProviderPayoutId.Should().Be("po_provider");
        (await fixture.Processor(command => Receipt(command)).ProcessNextAsync("worker", Now))
            .Should().BeNull();

        var ambiguous = await fixture.AddAsync(PayoutOperationState.Dispatching);
        var ambiguousResult = await fixture.Processor(command =>
                Receipt(command, PayoutProviderOutcome.Ambiguous))
            .ProcessNextAsync("worker", Now);
        ambiguousResult!.Outcome.Should().Be(PayoutProviderOutcome.Ambiguous);
        fixture.Store.Get(ambiguous.Operation.Id).State.Should().Be(PayoutOperationState.Ambiguous);

        var alreadyAmbiguous = await fixture.AddAsync(PayoutOperationState.Ambiguous);
        var replay = await fixture.Processor(command =>
                Receipt(command, PayoutProviderOutcome.Ambiguous))
            .ProcessNextAsync("worker", Now);
        replay!.Processed.Should().BeTrue();
        fixture.Store.Get(alreadyAmbiguous.Operation.Id).Version
            .Should().Be(alreadyAmbiguous.Operation.Version);
    }

    [Fact]
    public async Task Processor_ReturnsReplayWhenAnotherWorkerCompletedTheClaimedMessage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.AddAsync(PayoutOperationState.Dispatching);
        var provider = new CallbackProvider(async command =>
        {
            var row = await fixture.Context.Set<PayoutDispatchOutboxRow>()
                .SingleAsync(value => value.Id == item.Row.Id);
            row.CompletedAt = Now;
            await fixture.Context.SaveChangesAsync();
            return Receipt(command);
        });

        var result = await fixture.Processor(provider).ProcessNextAsync("worker", Now);

        result!.Processed.Should().BeFalse();
        result.AttemptCount.Should().Be(1);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("missing")]
    [InlineData("expiry")]
    public async Task Processor_RejectsEveryStaleLeaseShape(string stale)
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.AddAsync(PayoutOperationState.Dispatching);
        var provider = new CallbackProvider(async command =>
        {
            var row = await fixture.Context.Set<PayoutDispatchOutboxRow>()
                .SingleAsync(value => value.Id == item.Row.Id);
            if (stale == "owner") row.LeaseOwner = "another-worker";
            else if (stale == "missing") row.LeaseExpiresAt = null;
            else row.LeaseExpiresAt = Now.AddTicks(-1);
            await fixture.Context.SaveChangesAsync();
            return Receipt(command);
        });

        await FluentActions.Awaiting(() => fixture.Processor(provider)
                .ProcessNextAsync("worker", Now).AsTask())
            .Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task Processor_RejectsAnOperationThatIsNoLongerDispatchable()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAsync(PayoutOperationState.Succeeded);

        await FluentActions.Awaiting(() => fixture.Processor(command => Receipt(command))
                .ProcessNextAsync("worker", Now).AsTask())
            .Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task Processor_FailsClosedForPayloadEvidenceOperationAndEveryReceiptBinding()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invalids = new[]
        {
            "hash", "null", "operation", "evidence", "receipt-operation",
            "receipt-account", "receipt-destination", "receipt-time"
        };
        foreach (var invalid in invalids)
        {
            var item = await fixture.AddAsync(PayoutOperationState.Dispatching);
            if (invalid == "hash") item.Row.PayloadHash = "invalid";
            if (invalid == "null")
            {
                item.Row.Payload = "null";
                item.Row.PayloadHash = Hash(item.Row.Payload);
            }
            if (invalid == "operation")
            {
                var changed = item.Command with { OperationId = Guid.NewGuid() };
                item.Row.Payload = JsonSerializer.Serialize(changed, JsonOptions);
                item.Row.PayloadHash = Hash(item.Row.Payload);
            }
            await fixture.Context.SaveChangesAsync();

            var processor = fixture.Processor(command => invalid switch
            {
                "receipt-operation" => Receipt(command) with { OperationId = Guid.NewGuid() },
                "receipt-account" => Receipt(command) with { ProviderAccountId = "changed" },
                "receipt-destination" => Receipt(command) with { DestinationHash = "changed" },
                "receipt-time" => Receipt(command) with { ObservedAt = command.RequestedAt.AddTicks(-1) },
                _ => Receipt(command)
            }, evidenceValid: invalid != "evidence");

            await FluentActions.Awaiting(() => processor.ProcessNextAsync("worker", Now).AsTask())
                .Should().ThrowAsync<Exception>();
            fixture.Context.ChangeTracker.Clear();
            var persisted = await fixture.Context.Set<PayoutDispatchOutboxRow>()
                .SingleAsync(row => row.Id == item.Row.Id);
            persisted.LastErrorCode.Should().Be("provider-error");
            persisted.CompletedAt = Now;
            persisted.LeaseOwner = null;
            persisted.LeaseExpiresAt = null;
            await fixture.Context.SaveChangesAsync();
            fixture.Context.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task Processor_RejectsAttemptCounterOverflow()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.AddAsync(PayoutOperationState.Dispatching);
        item.Row.AttemptCount = int.MaxValue;
        await fixture.Context.SaveChangesAsync();

        await FluentActions.Awaiting(() => fixture.Processor(command => Receipt(command))
                .ProcessNextAsync("worker", Now).AsTask())
            .Should().ThrowAsync<OverflowException>();
    }

    [Fact]
    public async Task WriterAndProcessor_ValidateDependenciesRowsAndWorkerIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var stub = new NonRelationalContext();
        var store = new InMemoryPayoutOperationStore();
        var provider = new CallbackProvider(command => ValueTask.FromResult(Receipt(command)));
        var evidence = new EvidenceVerifier(true);

        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxWriter(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxWriter(stub))
            .Should().Throw<InvalidOperationException>();
        await FluentActions.Awaiting(() => new PostgreSqlPayoutDispatchOutboxWriter(fixture.Context)
                .AddAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxProcessor(
                null!, store, provider, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxProcessor(
                stub, null!, provider, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxProcessor(
                stub, store, null!, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxProcessor(
                stub, store, provider, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutDispatchOutboxProcessor(
                stub, store, provider, evidence)).Should().Throw<InvalidOperationException>();

        var processor = fixture.Processor(command => Receipt(command));
        await FluentActions.Awaiting(() => processor.ProcessNextAsync(" ", Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => processor.ProcessNextAsync(new string('x', 201), Now).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await processor.ProcessNextAsync("worker", Now)).Should().BeNull();
    }

    private static PayoutDispatchReceipt Receipt(
        PayoutDispatchCommand command,
        PayoutProviderOutcome outcome = PayoutProviderOutcome.Submitted) => new(
        command.OperationId, outcome, "po_provider", command.ProviderAccountId,
        command.DestinationHash, "provider-evidence", "signature", Now);

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly EconomyPostgreSqlTestDatabase _database;
        private int _sequence;

        private Fixture(EconomyPostgreSqlTestDatabase database, ApplicationDbContext context)
        {
            _database = database;
            Context = context;
            Store = new PostgreSqlPayoutOperationStore(context);
            Writer = new PostgreSqlPayoutDispatchOutboxWriter(context);
        }

        public ApplicationDbContext Context { get; }
        public PostgreSqlPayoutOperationStore Store { get; }
        public PostgreSqlPayoutDispatchOutboxWriter Writer { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var database = await EconomyPostgreSqlTestDatabase.CreateAsync("payout_dispatch_outbox");
            var context = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(database.ConnectionString).Options);
            await context.Database.MigrateAsync();
            return new Fixture(database, context);
        }

        public async Task<OutboxItem> AddAsync(PayoutOperationState targetState)
        {
            _sequence++;
            var tenantId = Guid.NewGuid();
            var operation = new PayoutOperation(
                Guid.NewGuid(), new IdempotencyKey("outbox-" + Guid.NewGuid().ToString("N")),
                "request-hash", Guid.NewGuid(), Guid.NewGuid(), WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 2_000), "acct_1", "destination-hash",
                "binding-hash", "eligibility-hash", null, null,
                PayoutOperationState.Reserved, 1, 11, 13, new ReserveVersion(1), 1,
                new PolicyVersion(1), Guid.NewGuid(), Now.AddMinutes(-5), Now.AddMinutes(-5), tenantId);
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
                VALUES ({operation.WalletId.Value}, {operation.PayeeId}, {tenantId}, 1, {Now.AddMinutes(-6)});
                """);
            Store.Add(operation);
            if (targetState != PayoutOperationState.Reserved)
            {
                var dispatching = operation.Transition(
                    PayoutOperationState.Dispatching, Now.AddMinutes(-4),
                    dispatchSnapshotHash: "dispatch-snapshot");
                Store.Update(dispatching, operation.Version);
                operation = dispatching;
            }
            if (targetState == PayoutOperationState.Ambiguous)
            {
                var ambiguous = operation.Transition(
                    PayoutOperationState.Ambiguous, Now.AddMinutes(-3),
                    providerPayoutId: "unknown:" + operation.Id.ToString("N"));
                Store.Update(ambiguous, operation.Version);
                operation = ambiguous;
            }
            if (targetState == PayoutOperationState.Succeeded)
            {
                var succeeded = operation.Transition(
                    PayoutOperationState.Succeeded, Now.AddMinutes(-3),
                    providerPayoutId: "po_old");
                Store.Update(succeeded, operation.Version);
                operation = succeeded;
            }
            var command = new PayoutDispatchCommand(
                operation.Id, operation.Version, operation.FencingToken, operation.KillSwitchEpoch,
                operation.ProviderAccountId, operation.DestinationHash, operation.Amount,
                operation.DispatchSnapshotHash ?? "dispatch-snapshot",
                operation.IdempotencyKey.Value + ":dispatch", Now.AddMinutes(-2));
            var payload = JsonSerializer.Serialize(command, JsonOptions);
            var row = new PayoutDispatchOutboxRow
            {
                Id = Guid.NewGuid(), OperationId = operation.Id,
                IdempotencyKey = command.IdempotencyKey, Payload = payload, PayloadHash = Hash(payload),
                CreatedAt = Now.AddSeconds(_sequence), AvailableAt = Now.AddMinutes(-1)
            };
            await Writer.AddAsync(row);
            return new OutboxItem(operation, command, row);
        }

        public PostgreSqlPayoutDispatchOutboxProcessor Processor(
            Func<PayoutDispatchCommand, PayoutDispatchReceipt> receipt,
            bool evidenceValid = true) => Processor(new CallbackProvider(command =>
                ValueTask.FromResult(receipt(command))), evidenceValid);

        public PostgreSqlPayoutDispatchOutboxProcessor Processor(
            IConnectPayoutProvider provider,
            bool evidenceValid = true) => new(
            Context, Store, provider, new EvidenceVerifier(evidenceValid));

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _database.DisposeAsync();
        }
    }

    private sealed record OutboxItem(
        PayoutOperation Operation,
        PayoutDispatchCommand Command,
        PayoutDispatchOutboxRow Row);

    private sealed class CallbackProvider(
        Func<PayoutDispatchCommand, ValueTask<PayoutDispatchReceipt>> dispatch) : IConnectPayoutProvider
    {
        public ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(
            Guid payeeId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ConnectAccountSnapshot> GetAccountAsync(
            Guid payeeId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PayoutDispatchReceipt> DispatchAsync(
            PayoutDispatchCommand command,
            CancellationToken cancellationToken = default) => dispatch(command);
        public ValueTask<PayoutProviderEvent> ReconcileAsync(
            Guid operationId,
            string providerPayoutId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class EvidenceVerifier(bool valid) : IPayoutProviderEvidenceVerifier
    {
        public bool Verify(PayoutDispatchReceipt receipt) => valid;
        public bool Verify(PayoutProviderEvent providerEvent) => valid;
    }

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
