using System.Data.Common;
using System.Numerics;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nethereum.Model;
using Nethereum.Signer;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace GameGuild.CertificateAnchoring.NativeTests;

/// <summary>Actual PostgreSQL migrations, atomicity and fencing. Constructed observations test journal binding only;
/// actual chain proof is independently exercised by EvmCertificateNativeTests against Anvil over HTTPS.</summary>
public sealed class EvmCertificateJournalPostgreSqlTests(JournalPostgreSqlFixture fixture) : IClassFixture<JournalPostgreSqlFixture>
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static EvmCertificateFeeBudget Budget => EvmCertificateFeeBudget.Create(100000, 10, 1000000);
    private readonly JournalClock clock = new();

    private EvmCertificateJournal Store() => new(fixture, clock);

    [Fact]
    public async Task InitialMigrationUsesDedicatedHistoryAndMatchesProviderModel()
    {
        await using var db = fixture.CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(["20261019190000_EvmCertificateJournalInitial"], await db.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        Assert.All(db.Model.GetEntityTypes(), e => Assert.Equal(EvmCertificateJournalDbContext.SchemaName, e.GetSchema()));
        Assert.Equal(4, db.Model.GetEntityTypes().Count());
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL";
        Assert.True(Assert.IsType<bool>(await command.ExecuteScalarAsync(Ct)));
    }

    [Fact]
    public void SharedApplicationHistoryIsRejectedBeforeAnyDatabaseWrite()
    {
        var options = new DbContextOptionsBuilder<EvmCertificateJournalDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        Assert.Throws<ArgumentException>(() => new EvmCertificateJournalDbContext(options));
    }

    [Fact]
    public async Task ConcurrentDuplicateIntentsConvergeAcrossIndependentContexts()
    {
        var request = Request();
        var all = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Store().EnqueueAsync(request, Ct)));
        Assert.Single(all.Select(o => o.Id).Distinct());
        Assert.All(all, o => Assert.Equal(EvmCertificatePublicationStatus.Queued, o.Status));
        Assert.Single(await Store().ListEventsAsync(all[0].Id, Ct));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("tenant")]
    [InlineData("digest")]
    [InlineData("profile")]
    [InlineData("document")]
    public async Task DuplicateIdentityCannotChangeCapturedIntent(string field)
    {
        var request = Request();
        var original = await Store().EnqueueAsync(request, Ct);
        var changed = field switch
        {
            "owner" => request with { OwnerUserId = Guid.NewGuid() },
            "tenant" => request with { TenantId = Guid.NewGuid() },
            "digest" => request with { CertificateHash = Digest() },
            "profile" => request with { Network = EvmCertificatePublicationNetwork.Capture("changed-profile", Options()) },
            _ => request with { DocumentId = Guid.NewGuid() }
        };
        await Rejected(EvmCertificateJournalFailure.Conflict, () => Store().EnqueueAsync(changed, Ct));
        var retained = Assert.IsType<EvmCertificateJournalSnapshot>(await Store().FindAsync(original.Id, Ct));
        Assert.Equal(request.OwnerUserId, retained.OwnerUserId);
        Assert.Equal(request.TenantId, retained.TenantId);
        Assert.Equal(request.CertificateHash, retained.CertificateHash);
        Assert.Equal(request.Network.Profile, retained.Network.Profile);
        Assert.Single(await Store().ListEventsAsync(original.Id, Ct));
    }

    [Fact]
    public async Task OnlyOneWorkerAcquiresAnOperationAndExpiredLeaseIsFenced()
    {
        var op = await Store().EnqueueAsync(Request(), Ct);
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Store().TryAcquireAsync(op.Id, LeaseDuration, Ct)));
        var first = Assert.Single(claims, c => c is not null)!;
        clock.Advance(LeaseDuration + TimeSpan.FromSeconds(1));
        var next = Assert.IsType<EvmCertificateJournalLease>(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
        Assert.NotEqual(first.Token, next.Token);
        var key = EthECKey.GenerateKey();
        await Rejected(EvmCertificateJournalFailure.LeaseLost,
            () => Store().ReserveNonceAsync(first, "signer-a", key.GetPublicAddress(), 7, Budget, Ct));
        var accepted = await Store().ReserveNonceAsync(next, "signer-a", key.GetPublicAddress(), 7, Budget, Ct);
        Assert.Equal(7UL, accepted.Nonce);
    }

    [Fact]
    public async Task NoncesAreUniqueAcrossWorkersDocumentsAndContractsForTheSameWallet()
    {
        var key = EthECKey.GenerateKey();
        var operations = new List<EvmCertificateJournalSnapshot>();
        var leases = new List<EvmCertificateJournalLease>();
        for (var i = 0; i < 8; i++)
        {
            var options = Options();
            options.ContractAddress = i % 2 == 0 ? "0x" + new string('1', 40) : "0x" + new string('6', 40);
            var op = await Store().EnqueueAsync(Request() with { Network = EvmCertificatePublicationNetwork.Capture("issuer-a", options) }, Ct);
            operations.Add(op);
            leases.Add(Assert.IsType<EvmCertificateJournalLease>(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct)));
        }
        var reservations = await Task.WhenAll(leases.Select(lease => Store().ReserveNonceAsync(lease, "signer-a", key.GetPublicAddress(), 7, Budget, Ct)));
        Assert.Equal(Enumerable.Range(7, 8).Select(i => (ulong?)i), reservations.Select(r => r.Nonce).Order());
        Assert.Equal(8, reservations.Select(r => r.Id).Distinct().Count());
        foreach (var op in operations)
        {
            Assert.Equal(2, (await Store().ListEventsAsync(op.Id, Ct)).Count);
        }
    }

    [Fact]
    public async Task RestartRetainsNonceBudgetAndSignedAttemptBeforeAnyBroadcast()
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        var raw = Sign(key, op.PreparedTransaction, 2, 1);
        var attempt = await Store().StoreSignedAttemptAsync(lease, raw, Ct);
        Assert.Null(attempt.BroadcastAt);
        var restarted = Store();
        var retained = Assert.IsType<EvmCertificateJournalSnapshot>(await restarted.FindAsync(op.Id, Ct));
        Assert.Equal(op.Nonce, retained.Nonce);
        Assert.Equal(Budget.MaximumTransactionFeeWei, retained.Budget!.MaximumTransactionFeeWei);
        Assert.Equal(EvmCertificatePublicationStatus.Prepared, retained.Status);
        var persisted = Assert.Single(await restarted.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(raw.ToLowerInvariant(), persisted.RawTransaction);
        Assert.Null(persisted.BroadcastAt);
        Assert.Equal(attempt.Id, (await restarted.StoreSignedAttemptAsync(lease, raw, Ct)).Id);
        var unchanged = await restarted.ReserveNonceAsync(lease, "signer-a", key.GetPublicAddress(), 999,
            EvmCertificateFeeBudget.Create(100000, 20, 2000000), Ct);
        Assert.Equal(op.Nonce, unchanged.Nonce);
        Assert.Equal(new BigInteger(10), unchanged.Budget!.MaximumFeePerGasWei);
    }

    [Fact]
    public async Task BroadcastRequiresAPreviouslyPersistedExactSignedAttempt()
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        await Rejected(EvmCertificateJournalFailure.InvalidState, () => Store().RecordBroadcastAsync(lease, Digest(), Ct));
        var attempt = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 2, 1), Ct);
        await Rejected(EvmCertificateJournalFailure.InvalidState, () => Store().RecordBroadcastAsync(lease, Digest(), Ct));
        var submitted = await Store().RecordBroadcastAsync(lease, attempt.TransactionHash, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Submitted, submitted.Status);
        Assert.NotNull(Assert.Single(await Store().ListAttemptsAsync(op.Id, Ct)).BroadcastAt);
    }

    [Fact]
    public async Task ReplacementKeepsNonceIntentAndHistoryWithinTheOriginalFeeCap()
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        var first = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 2, 1), Ct);
        await Store().RecordBroadcastAsync(lease, first.TransactionHash, Ct);
        var replacement = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 3, 2), Ct);
        Assert.NotEqual(first.TransactionHash, replacement.TransactionHash);
        Assert.Equal(2, replacement.Sequence);
        await Rejected(EvmCertificateJournalFailure.InvalidTransaction,
            () => Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 11, 2), Ct));
        await Rejected(EvmCertificateJournalFailure.InvalidTransaction,
            () => Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 4, 1), Ct));
        Assert.Equal(2, (await Store().ListAttemptsAsync(op.Id, Ct)).Count);
        var winner = await Store().RecordObservationAsync(lease, first.TransactionHash,
            Observation(op, first.TransactionHash, key.GetPublicAddress()), RetryDelay, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, winner.Status);
        Assert.Equal(first.TransactionHash, winner.TransactionHash);
        Assert.Equal(op.Nonce, winner.Nonce);
    }

    [Fact]
    public async Task CancellationAfterDatabaseWriteRollsBackBothNonceAndOperation()
    {
        var key = EthECKey.GenerateKey();
        var op = await Store().EnqueueAsync(Request(), Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
        using var cancellation = new CancellationTokenSource();
        var cancelAfterWrite = new CancelAfterNonceWrite(cancellation);
        var intercepted = new EvmCertificateJournal(fixture.WithInterceptor(cancelAfterWrite), clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => intercepted.ReserveNonceAsync(lease,
            "signer-a", key.GetPublicAddress(), 7, Budget, cancellation.Token));
        Assert.True(cancelAfterWrite.ObservedDatabaseWrite);
        var retained = Assert.IsType<EvmCertificateJournalSnapshot>(await Store().FindAsync(op.Id, Ct));
        Assert.Null(retained.Nonce);
        Assert.Equal(EvmCertificatePublicationStatus.Queued, retained.Status);
        Assert.Single(await Store().ListEventsAsync(op.Id, Ct));
        Assert.Equal(7UL, (await Store().ReserveNonceAsync(lease, "signer-a", key.GetPublicAddress(), 7, Budget, Ct)).Nonce);
    }

    [Fact]
    public async Task RetryReleasesLeaseAndKeepsTheSamePreparedPayloadAndNonce()
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        var attempt = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 2, 1), Ct);
        var retry = await Store().ScheduleRetryAsync(lease, EvmCertificateJournalFailure.RpcUnavailable, RetryDelay, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Prepared, retry.Status);
        Assert.Null(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
        await Rejected(EvmCertificateJournalFailure.LeaseLost, () => Store().RecordBroadcastAsync(lease, attempt.TransactionHash, Ct));
        clock.Advance(RetryDelay);
        Assert.NotNull(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
        Assert.Equal(attempt.RawTransaction, Assert.Single(await Store().ListAttemptsAsync(op.Id, Ct)).RawTransaction);
        Assert.Equal(op.Nonce, (await Store().FindAsync(op.Id, Ct))!.Nonce);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("chain")]
    [InlineData("contract")]
    [InlineData("signer")]
    [InlineData("hash")]
    [InlineData("state")]
    public async Task ObservationCannotConfirmAnotherReservedIntent(string field)
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        var attempt = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 2, 1), Ct);
        var evidence = Observation(op, attempt.TransactionHash, key.GetPublicAddress()).Evidence!;
        var altered = field switch
        {
            "digest" => evidence with { CertificateHash = Digest() },
            "chain" => evidence with { Network = evidence.Network with { ChainId = 99 } },
            "contract" => evidence with { Network = evidence.Network with { ContractAddress = "0x" + new string('a', 40) } },
            "signer" => evidence with { Signer = EthECKey.GenerateKey().GetPublicAddress().ToLowerInvariant() },
            "hash" => evidence with { TransactionHash = Digest() },
            _ => evidence with { State = evidence.State with { AnchoredAtBlock = 0 } }
        };
        await Rejected(EvmCertificateJournalFailure.InvalidEvidence, () => Store().RecordObservationAsync(lease,
            attempt.TransactionHash, new(EvmCertificateProofStatus.Verified, altered), RetryDelay, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Prepared, (await Store().FindAsync(op.Id, Ct))!.Status);
    }

    [Fact]
    public async Task ReorgAppendsHistoryAndRemovesConfirmedStatusWithoutLosingSignedAttempts()
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        var attempt = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 2, 1), Ct);
        await Store().RecordObservationAsync(lease, attempt.TransactionHash, Observation(op, attempt.TransactionHash, key.GetPublicAddress()), RetryDelay, Ct);
        clock.Advance(RetryDelay);
        var next = Assert.IsType<EvmCertificateJournalLease>(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
        var reorged = await Store().RecordObservationAsync(next, attempt.TransactionHash, new(EvmCertificateProofStatus.Reorged), RetryDelay, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Reorged, reorged.Status);
        var history = await Store().ListEventsAsync(op.Id, Ct);
        Assert.Equal([EvmCertificateProofStatus.Verified, EvmCertificateProofStatus.Reorged], history.Where(e => e.ProofStatus.HasValue).Select(e => e.ProofStatus!.Value));
        Assert.Single(await Store().ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(history.Count, history.Select(e => e.Revision).Distinct().Count());
    }

    [Fact]
    public async Task InvalidDefaultObservationFailsClosedAndCannotBeClaimedAgain()
    {
        var key = EthECKey.GenerateKey();
        var (op, lease) = await Reserved(key);
        var attempt = await Store().StoreSignedAttemptAsync(lease, Sign(key, op.PreparedTransaction, 2, 1), Ct);
        var invalid = await Store().RecordObservationAsync(lease, attempt.TransactionHash, new(default), RetryDelay, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.NeedsAttention, invalid.Status);
        Assert.Equal(EvmCertificateJournalFailure.InvalidEvidence, invalid.LastFailure);
        clock.Advance(RetryDelay);
        Assert.Null(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
    }

    [Fact]
    public async Task RevocationWaitsForVerifiedAnchorAndUsesItsOriginalDigest()
    {
        var request = Request();
        var anchor = await Store().EnqueueAsync(request, Ct);
        var revoke = await Store().EnqueueAsync(request with { Kind = EvmCertificateOperationKind.Revoke }, Ct);
        Assert.Null(await Store().TryAcquireAsync(revoke.Id, LeaseDuration, Ct));
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Store().TryAcquireAsync(anchor.Id, LeaseDuration, Ct));
        // Recovery may adopt independently verified chain state; no own signed attempt is invented.
        var hash = Digest();
        await Store().RecordObservationAsync(lease, hash, Observation(anchor, hash, EthECKey.GenerateKey().GetPublicAddress()), RetryDelay, Ct);
        Assert.NotNull(await Store().TryAcquireAsync(revoke.Id, LeaseDuration, Ct));
        Assert.Equal(anchor.CertificateHash, revoke.CertificateHash);
        Assert.Empty(await Store().ListAttemptsAsync(anchor.Id, Ct));
    }

    private async Task<(EvmCertificateJournalSnapshot Operation, EvmCertificateJournalLease Lease)> Reserved(EthECKey key)
    {
        var op = await Store().EnqueueAsync(Request(), Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Store().TryAcquireAsync(op.Id, LeaseDuration, Ct));
        return (await Store().ReserveNonceAsync(lease, "signer-a", key.GetPublicAddress(), 7, Budget, Ct), lease);
    }

    private static EvmCertificateJournalRequest Request() => new(EvmCertificatePublicationNetwork.Capture("issuer-a", Options()),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "native-fixture", Digest(), EvmCertificateOperationKind.Anchor);

    private static string Digest() => "0x" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static EvmCertificateNetworkOptions Options() => new()
    {
        ChainId = 12345, RpcEndpoint = "https://rpc.invalid/", ContractAddress = "0x" + new string('1', 40),
        IssuerId = "0x" + new string('2', 64), ExpectedRuntimeCodeSha256 = new string('3', 64),
        FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
    };

    private static string Sign(EthECKey key, EvmCertificatePreparedTransaction prepared, int fee, int priority)
    {
        var tx = new Transaction1559(prepared.Network.ChainId, prepared.Nonce, priority, fee,
            prepared.Budget.GasLimit, prepared.Network.ContractAddress, 0, prepared.Input, []);
        return "0x" + new Transaction1559Signer().SignTransaction(key, tx);
    }

    private static EvmCertificateProofResult Observation(EvmCertificateJournalSnapshot op, string hash, string signer)
    {
        signer = signer.ToLowerInvariant();
        var block = new EvmCertificateBlockReference(12, "0x" + new string('a', 64));
        var network = new EvmCertificateNetworkIdentity(op.Network.ChainId, op.Network.ContractAddress, op.Network.IssuerId,
            op.Network.RuntimeCodeSha256, op.Network.FinalityMode, op.Network.MinimumConfirmations, block);
        return new(EvmCertificateProofStatus.Verified, new(op.Kind, op.CertificateHash, hash, signer, block, network,
            new(signer, "0x" + new string('0', 40), block.Number, 0)));
    }

    private static async Task Rejected<T>(EvmCertificateJournalFailure failure, Func<Task<T>> action)
    {
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(async () => { await action(); });
        Assert.Equal(failure, error.Failure);
        Assert.Null(error.InnerException);
    }

    private sealed class JournalClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 19, 19, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class CancelAfterNonceWrite(CancellationTokenSource source) : DbCommandInterceptor
    {
        internal bool ObservedDatabaseWrite { get; private set; }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is EvmCertificateJournalDbContext && command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal) &&
                command.CommandText.Contains("evm_certificate_nonces", StringComparison.Ordinal))
            {
                ObservedDatabaseWrite = true;
                source.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return ValueTask.FromResult(result);
        }
    }
}

public sealed class JournalPostgreSqlFixture : IDbContextFactory<EvmCertificateJournalDbContext>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("mirror.gcr.io/library/postgres:17-alpine@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24")
        .WithDatabase("certificate_journal")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public string ConnectionString => postgres.GetConnectionString();

    public EvmCertificateJournalDbContext CreateDbContext() => new(Options().Options);

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    internal IDbContextFactory<EvmCertificateJournalDbContext> WithInterceptor(IInterceptor interceptor) =>
        new InterceptedFactory(Options().AddInterceptors(interceptor).Options);

    private DbContextOptionsBuilder<EvmCertificateJournalDbContext> Options() =>
        new DbContextOptionsBuilder<EvmCertificateJournalDbContext>().UseNpgsql(ConnectionString,
            p => p.MigrationsHistoryTable(EvmCertificateJournalDbContext.MigrationsHistoryTable, EvmCertificateJournalDbContext.SchemaName));

    private sealed class InterceptedFactory(DbContextOptions<EvmCertificateJournalDbContext> options) : IDbContextFactory<EvmCertificateJournalDbContext>
    {
        public EvmCertificateJournalDbContext CreateDbContext() => new(options);
    }
}
