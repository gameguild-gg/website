using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class PostgreSqlAdminWithdrawalDispatchOutboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 19, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WriterAndProcessor_PersistDispatchSubmittedAmbiguousAndReplaySafeOutcomes()
    {
        await using var fixture = await Fixture.CreateAsync();

        var submitted = await fixture.AddAsync(AdminWithdrawalRunState.Dispatching);
        var submittedProcessor = fixture.Processor(command => Receipt(command, AdminWithdrawalProviderOutcome.Submitted));
        var submittedResult = await submittedProcessor.ProcessNextAsync(" worker ", Now);
        var submittedRun = fixture.Store.Get(submitted.Command.TenantId, submitted.Command.RunId);
        submittedResult.Should().NotBeNull();
        submittedResult!.TenantId.Should().Be(submitted.Command.TenantId);
        submittedResult.RunId.Should().Be(submitted.Command.RunId);
        submittedResult.Outcome.Should().Be(AdminWithdrawalProviderOutcome.Submitted);
        submittedResult.Processed.Should().BeTrue();
        submittedResult.AttemptCount.Should().Be(1);
        submittedRun.State.Should().Be(AdminWithdrawalRunState.Dispatching);
        submittedRun.ProviderTransferId.Should().Be("po_provider");
        fixture.Audit.Events(submitted.Command.TenantId, submitted.Command.RunId)
            .Should().ContainSingle(item => item.Kind == "provider-accepted");
        (await submittedProcessor.ProcessNextAsync("worker", Now)).Should().BeNull();

        var ambiguous = await fixture.AddAsync(AdminWithdrawalRunState.Dispatching);
        var ambiguousResult = await fixture.Processor(command =>
                Receipt(command, AdminWithdrawalProviderOutcome.Ambiguous))
            .ProcessNextAsync("worker", Now);
        ambiguousResult!.Outcome.Should().Be(AdminWithdrawalProviderOutcome.Ambiguous);
        fixture.Store.Get(ambiguous.Command.TenantId, ambiguous.Command.RunId).State
            .Should().Be(AdminWithdrawalRunState.Ambiguous);
        fixture.Audit.Events(ambiguous.Command.TenantId, ambiguous.Command.RunId)
            .Should().ContainSingle(item => item.Kind == "dispatch-ambiguous");

        var alreadyAmbiguous = await fixture.AddAsync(AdminWithdrawalRunState.Ambiguous);
        var replayResult = await fixture.Processor(command =>
                Receipt(command, AdminWithdrawalProviderOutcome.Ambiguous))
            .ProcessNextAsync("worker", Now);
        replayResult!.Processed.Should().BeTrue();
        fixture.Store.Get(alreadyAmbiguous.Command.TenantId, alreadyAmbiguous.Command.RunId).Version
            .Should().Be(alreadyAmbiguous.Run.Version);
    }

    [Fact]
    public async Task Processor_ReturnsReplayWhenAnotherWorkerCompletedClaimedMessage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.AddAsync(AdminWithdrawalRunState.Dispatching);
        var provider = new CallbackProvider(async command =>
        {
            var row = await fixture.Context.Set<AdminWithdrawalDispatchOutboxRow>()
                .SingleAsync(value => value.Id == item.Row.Id);
            row.CompletedAt = Now;
            await fixture.Context.SaveChangesAsync();
            return Receipt(command, AdminWithdrawalProviderOutcome.Submitted);
        });

        var result = await fixture.Processor(provider).ProcessNextAsync("worker", Now);

        result!.Processed.Should().BeFalse();
        result.AttemptCount.Should().Be(1);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("missing")]
    [InlineData("expiry")]
    public async Task Processor_RejectsStaleLeaseAndDoesNotConsumeMessage(string stale)
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = await fixture.AddAsync(AdminWithdrawalRunState.Dispatching);
        var provider = new CallbackProvider(async command =>
        {
            var row = await fixture.Context.Set<AdminWithdrawalDispatchOutboxRow>()
                .SingleAsync(value => value.Id == item.Row.Id);
            if (stale == "owner") row.LeaseOwner = "another-worker";
            else if (stale == "missing") row.LeaseExpiresAt = null;
            else row.LeaseExpiresAt = Now.AddTicks(-1);
            await fixture.Context.SaveChangesAsync();
            return Receipt(command, AdminWithdrawalProviderOutcome.Submitted);
        });

        await FluentActions.Awaiting(() => fixture.Processor(provider)
                .ProcessNextAsync("worker", Now).AsTask())
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
    }

    [Fact]
    public async Task Processor_RejectsRunThatIsNoLongerDispatchable()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAsync(AdminWithdrawalRunState.Succeeded);

        await FluentActions.Awaiting(() => fixture.Processor(command =>
                Receipt(command, AdminWithdrawalProviderOutcome.Submitted))
                .ProcessNextAsync("worker", Now).AsTask())
            .Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
    }

    [Fact]
    public async Task Processor_FailsClosedForInvalidPayloadEvidenceAndEveryReceiptBinding()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invalids = new[]
        {
            "hash", "null", "command-tenant", "row-tenant", "row-run", "evidence",
            "receipt-tenant", "receipt-run", "receipt-fence", "receipt-epoch", "receipt-amount",
            "receipt-source", "receipt-destination", "receipt-time"
        };

        foreach (var invalid in invalids)
        {
            var item = await fixture.AddAsync(AdminWithdrawalRunState.Dispatching);
            if (invalid == "hash") item.Row.PayloadHash = "invalid";
            if (invalid == "null")
            {
                item.Row.Payload = "null";
                item.Row.PayloadHash = Hash(item.Row.Payload);
            }
            if (invalid == "command-tenant")
                ReplaceCommand(item, item.Command with { TenantId = Guid.Empty });
            if (invalid == "row-tenant") item.Row.TenantId = Guid.NewGuid();
            if (invalid == "row-run") ReplaceCommand(item, item.Command with { RunId = Guid.NewGuid() });
            await fixture.Context.SaveChangesAsync();

            var processor = fixture.Processor(command => invalid switch
            {
                "receipt-tenant" => Receipt(command) with { TenantId = Guid.NewGuid() },
                "receipt-run" => Receipt(command) with { RunId = Guid.NewGuid() },
                "receipt-fence" => Receipt(command) with { FencingToken = command.FencingToken + 1 },
                "receipt-epoch" => Receipt(command) with { ExecutionEpoch = command.ExecutionEpoch + 1 },
                "receipt-amount" => Receipt(command) with
                    { Amount = new CoinAmount(CurrencyCode.HardCoin, command.Amount.Units + 1) },
                "receipt-source" => Receipt(command) with { SourceAssetKey = "changed" },
                "receipt-destination" => Receipt(command) with { DestinationHash = "changed" },
                "receipt-time" => Receipt(command) with { ObservedAt = command.RequestedAt.AddTicks(-1) },
                _ => Receipt(command)
            }, evidenceValid: invalid != "evidence");

            await FluentActions.Awaiting(() => processor.ProcessNextAsync("worker", Now).AsTask())
                .Should().ThrowAsync<Exception>();
            fixture.Context.ChangeTracker.Clear();
            var persisted = await fixture.Context.Set<AdminWithdrawalDispatchOutboxRow>()
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
        var item = await fixture.AddAsync(AdminWithdrawalRunState.Dispatching);
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
        var context = new StubApplicationDbContext();
        var store = new InMemoryAdminWithdrawalStore();
        var audit = new AdminWithdrawalAuditTrail();
        var provider = new CallbackProvider(command => ValueTask.FromResult(Receipt(command)));
        var evidence = new EvidenceVerifier(true);

        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxWriter(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxWriter(context))
            .Should().Throw<InvalidOperationException>();
        await FluentActions.Awaiting(() => new PostgreSqlAdminWithdrawalDispatchOutboxWriter(fixture.Context)
                .AddAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
                null!, store, audit, provider, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
                context, null!, audit, provider, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
                context, store, null!, provider, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
                context, store, audit, null!, evidence)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
                context, store, audit, provider, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalDispatchOutboxProcessor(
                context, store, audit, provider, evidence)).Should().Throw<InvalidOperationException>();

        var processor = fixture.Processor(command => Receipt(command));
        await FluentActions.Awaiting(() => processor.ProcessNextAsync(" ", Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => processor.ProcessNextAsync(new string('x', 201), Now).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await processor.ProcessNextAsync("worker", Now)).Should().BeNull();
    }

    private static void ReplaceCommand(OutboxItem item, AdminWithdrawalDispatchCommand command)
    {
        item.Row.Payload = JsonSerializer.Serialize(command, JsonOptions);
        item.Row.PayloadHash = Hash(item.Row.Payload);
    }

    private static AdminWithdrawalProviderReceipt Receipt(
        AdminWithdrawalDispatchCommand command,
        AdminWithdrawalProviderOutcome outcome = AdminWithdrawalProviderOutcome.Submitted) => new(
        command.RunId, command.TenantId, outcome, "po_provider", command.FencingToken,
        command.ExecutionEpoch, command.Amount, command.SourceAssetKey, command.DestinationHash,
        "provider-evidence", "signature", Now);

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
            Store = new PostgreSqlAdminWithdrawalStore(context);
            Audit = new PostgreSqlAdminWithdrawalAuditTrail(context);
            Writer = new PostgreSqlAdminWithdrawalDispatchOutboxWriter(context);
        }

        public ApplicationDbContext Context { get; }
        public PostgreSqlAdminWithdrawalStore Store { get; }
        public PostgreSqlAdminWithdrawalAuditTrail Audit { get; }
        public PostgreSqlAdminWithdrawalDispatchOutboxWriter Writer { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var database = await EconomyPostgreSqlTestDatabase.CreateAsync("treasury_dispatch_outbox");
            var context = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(database.ConnectionString).Options);
            await context.Database.MigrateAsync();
            return new Fixture(database, context);
        }

        public async Task<OutboxItem> AddAsync(AdminWithdrawalRunState state)
        {
            _sequence++;
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var walletId = WalletId.New();
            var runId = Guid.NewGuid();
            var approverId = Guid.NewGuid();
            var run = new AdminWithdrawalRun(
                runId, tenantId, new IdempotencyKey("run-" + runId.ToString("N")), "request-hash",
                new DateOnly(2026, 8, _sequence), actorId, null, walletId,
                new CoinAmount(CurrencyCode.HardCoin, 2_000), "stripe:platform:cash", "destination-hash",
                AdminWithdrawalRunState.PendingApproval, 1, 11, 11, new ReserveVersion(1), 1,
                new PolicyVersion(1), null, null, Now.AddMinutes(-5), Now.AddMinutes(-5));
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
                VALUES ({walletId.Value}, {actorId}, {tenantId}, 1, {Now.AddMinutes(-3)});
                """);
            Store.Add(run);
            if (state != AdminWithdrawalRunState.PendingApproval)
            {
                var approved = run with
                {
                    ApprovedBy = approverId,
                    State = AdminWithdrawalRunState.Approved,
                    Version = 2,
                    UpdatedAt = Now.AddMinutes(-4)
                };
                Store.Update(approved, run.Version);
                run = approved;
            }
            if (state is not (AdminWithdrawalRunState.PendingApproval or AdminWithdrawalRunState.Approved))
            {
                var dispatching = run with
                {
                    State = AdminWithdrawalRunState.Dispatching,
                    DispatchSnapshotHash = "dispatch-snapshot",
                    Version = 3,
                    UpdatedAt = Now.AddMinutes(-3)
                };
                Store.Update(dispatching, run.Version);
                run = dispatching;
            }
            if (state == AdminWithdrawalRunState.Ambiguous)
            {
                var ambiguous = run with
                {
                    State = AdminWithdrawalRunState.Ambiguous,
                    Version = 4,
                    UpdatedAt = Now.AddMinutes(-2)
                };
                Store.Update(ambiguous, run.Version);
                run = ambiguous;
            }
            if (state == AdminWithdrawalRunState.Succeeded)
            {
                var succeeded = run with
                {
                    State = AdminWithdrawalRunState.Succeeded,
                    ProviderTransferId = "po_old",
                    Version = 4,
                    UpdatedAt = Now.AddMinutes(-2)
                };
                Store.RecordProviderEvent(
                    "fixture-event-" + run.Id.ToString("N"), "fixture-event-hash", succeeded, run.Version);
                run = succeeded;
            }
            var command = new AdminWithdrawalDispatchCommand(
                run.Id, run.TenantId, run.Version, run.FencingToken, run.ExecutionEpoch, run.Amount,
                run.SourceAssetKey, run.DestinationHash, run.DispatchSnapshotHash ?? "dispatch-snapshot",
                run.IdempotencyKey.Value, Now.AddMinutes(-1));
            var payload = JsonSerializer.Serialize(command, JsonOptions);
            var row = new AdminWithdrawalDispatchOutboxRow
            {
                Id = Guid.NewGuid(), TenantId = run.TenantId, RunId = run.Id,
                IdempotencyKey = command.IdempotencyKey, Payload = payload, PayloadHash = Hash(payload),
                CreatedAt = Now.AddSeconds(_sequence), AvailableAt = Now.AddMinutes(-1), AttemptCount = 0
            };
            await Writer.AddAsync(row);
            return new OutboxItem(run, command, row);
        }

        public PostgreSqlAdminWithdrawalDispatchOutboxProcessor Processor(
            Func<AdminWithdrawalDispatchCommand, AdminWithdrawalProviderReceipt> receipt,
            bool evidenceValid = true) => Processor(new CallbackProvider(command =>
                ValueTask.FromResult(receipt(command))), evidenceValid);

        public PostgreSqlAdminWithdrawalDispatchOutboxProcessor Processor(
            IAdminWithdrawalProvider provider,
            bool evidenceValid = true) => new(
            Context, Store, Audit, provider, new EvidenceVerifier(evidenceValid));

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _database.DisposeAsync();
        }
    }

    private sealed record OutboxItem(
        AdminWithdrawalRun Run,
        AdminWithdrawalDispatchCommand Command,
        AdminWithdrawalDispatchOutboxRow Row);

    private sealed class CallbackProvider(
        Func<AdminWithdrawalDispatchCommand, ValueTask<AdminWithdrawalProviderReceipt>> dispatch)
        : IAdminWithdrawalProvider
    {
        public ValueTask<AdminWithdrawalProviderReceipt> DispatchAsync(
            AdminWithdrawalDispatchCommand command,
            CancellationToken cancellationToken = default) => dispatch(command);

        public ValueTask<AdminWithdrawalProviderEvent> ReconcileAsync(
            Guid tenantId,
            Guid runId,
            string idempotencyKey,
            string? providerTransferId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class EvidenceVerifier(bool valid) : IAdminWithdrawalProviderEvidenceVerifier
    {
        public bool Verify(AdminWithdrawalProviderReceipt receipt) => valid;
        public bool Verify(AdminWithdrawalProviderEvent providerEvent) => valid;
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
