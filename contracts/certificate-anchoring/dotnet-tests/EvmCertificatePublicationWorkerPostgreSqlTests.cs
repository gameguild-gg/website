using System.Numerics;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace GameGuild.CertificateAnchoring.NativeTests;

/// <summary>Real PostgreSQL worker state transitions. RPC responses here test control flow, not chain proof.</summary>
public sealed class EvmCertificatePublicationWorkerPostgreSqlTests(JournalPostgreSqlFixture database) : IClassFixture<JournalPostgreSqlFixture>
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly WorkerClock clock = new();
    private readonly WorkerPorts ports = new();
    private static EvmCertificateFeeBudget Budget => EvmCertificateFeeBudget.Create(100000, 100, 10000000);
    private EvmCertificateJournal Journal => new(database, clock);

    [Fact]
    public async Task WorkerCommitsExactSignedPayloadBeforeTransportAndRetainsOneNonce()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        ports.BeforeSend = async raw => Assert.Equal(raw, Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct)).RawTransaction);
        var first = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Submitted, first.Status);
        Assert.Equal(7UL, first.Nonce);
        Assert.NotNull(Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct)).BroadcastAt);
        clock.Advance(TimeSpan.FromSeconds(6));
        var next = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(first.Nonce, next.Nonce);
        Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(1, ports.SignCalls);
    }

    [Fact]
    public async Task AcceptedSendWithoutAcknowledgementRecoversByPersistedHashWithoutResolvingAKey()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        ports.FailSendAfterAccept = true;
        var uncertain = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        var attempt = Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Null(attempt.BroadcastAt);
        Assert.Equal(EvmCertificatePublicationStatus.Prepared, uncertain.Status);
        ports.Proof = _ => Verified(uncertain, attempt.TransactionHash);
        ports.SignerUnavailable = true;
        clock.Advance(TimeSpan.FromSeconds(6));
        var confirmed = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, confirmed.Status);
        Assert.Equal(attempt.TransactionHash, confirmed.TransactionHash);
        Assert.Equal(1, ports.AddressCalls);
        Assert.Equal(1, ports.SignCalls);
        Assert.Equal(1, ports.SendCalls);
    }

    [Fact]
    public async Task ReplacementKeepsTheReservedIntentAndIncreasesBothFeesInsideTheCapturedBudget()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var initial = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        clock.Advance(TimeSpan.FromSeconds(31));
        var replaced = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        var attempts = await Journal.ListAttemptsAsync(op.Id, Ct);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(initial.Nonce, replaced.Nonce);
        Assert.Equal(initial.CertificateHash, replaced.CertificateHash);
        Assert.Equal(initial.Network.ChainId, replaced.Network.ChainId);
        Assert.Equal(initial.Network.ContractAddress, replaced.Network.ContractAddress);
        Assert.Equal(initial.SignerAddress, replaced.SignerAddress);
        Assert.Equal(initial.Budget!.GasLimit, replaced.Budget!.GasLimit);
        Assert.Equal(initial.Budget.MaximumTransactionFeeWei, replaced.Budget.MaximumTransactionFeeWei);
        Assert.True(attempts[1].MaximumFeePerGasWei > attempts[0].MaximumFeePerGasWei);
        Assert.True(attempts[1].PriorityFeePerGasWei > attempts[0].PriorityFeePerGasWei);
        Assert.True(attempts[1].MaximumFeePerGasWei <= initial.Budget!.MaximumFeePerGasWei);
    }

    [Fact]
    public async Task AnOlderReplacementCanWinCanonicalVerificationWithoutAnyFurtherSigning()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var initial = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        var firstHash = Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct)).TransactionHash;
        clock.Advance(TimeSpan.FromSeconds(31));
        await Worker().ProcessAsync(op.Id, Ct);
        ports.Proof = hash => hash == firstHash ? Verified(initial, hash) : new(EvmCertificateProofStatus.Pending);
        clock.Advance(TimeSpan.FromSeconds(6));
        var confirmed = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, confirmed.Status);
        Assert.Equal(firstHash, confirmed.TransactionHash);
        Assert.Equal(2, ports.SignCalls);
    }

    [Fact]
    public async Task IncludedTransactionWaitsForFinalityWithoutFeeReplacement()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        await Worker().ProcessAsync(op.Id, Ct);
        ports.Proof = _ => new(EvmCertificateProofStatus.NotFinalized);
        clock.Advance(TimeSpan.FromSeconds(31));
        var waiting = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Submitted, waiting.Status);
        Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(1, ports.SendCalls);
    }

    [Fact]
    public async Task ExhaustedAttemptLimitRequiresAttentionWithoutAllocatingAnotherNonce()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var first = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker(maximumAttempts: 1).ProcessAsync(op.Id, Ct));
        clock.Advance(TimeSpan.FromSeconds(31));
        var stopped = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker(maximumAttempts: 1).ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.NeedsAttention, stopped.Status);
        Assert.Equal(first.Nonce, stopped.Nonce);
        Assert.Equal(first.TransactionHash, stopped.TransactionHash);
        Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Null(await Worker().ProcessAsync(op.Id, Ct));
    }

    [Fact]
    public async Task MissingSignerBeforeAnyHashBecomesDurableAttention()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        ports.SignerUnavailable = true;
        var stopped = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.NeedsAttention, stopped.Status);
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, stopped.LastFailure);
        Assert.Null(stopped.Nonce);
        Assert.Null(stopped.TransactionHash);
        Assert.Empty(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(0, ports.SendCalls);
    }

    [Fact]
    public async Task NetworkDriftStopsBeforeAnySignerOrBroadcastCall()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        ports.WrongNetwork = true;
        var stopped = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.NeedsAttention, stopped.Status);
        Assert.Equal(EvmCertificateJournalFailure.InvalidEvidence, stopped.LastFailure);
        Assert.Equal(0, ports.AddressCalls);
        Assert.Equal(0, ports.SendCalls);
    }

    [Fact]
    public async Task CompetingWorkersDoNotProduceCompetingInitialAttempts()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        await Task.WhenAll(Worker().ProcessAsync(op.Id, Ct), Worker().ProcessAsync(op.Id, Ct));
        Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(1, ports.SignCalls);
        Assert.Equal(1, ports.SendCalls);
    }

    [Fact]
    public async Task ExistingExternallyVerifiedHashIsMonitoredWithoutResigning()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var hash = "0x" + new string('f', 64);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        await Journal.RecordObservationAsync(lease, hash, Verified(op, hash), TimeSpan.FromSeconds(5), Ct);
        ports.Proof = observed => Verified(op, observed);
        ports.SignerUnavailable = true;
        clock.Advance(TimeSpan.FromSeconds(6));
        var confirmed = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, confirmed.Status);
        Assert.Equal(hash, confirmed.TransactionHash);
        Assert.Empty(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(0, ports.AddressCalls);
        Assert.Equal(0, ports.SignCalls);
        Assert.Equal(0, ports.SendCalls);
    }

    [Fact]
    public async Task AnExternalHashLostToReorgDoesNotAuthorizeAnUnrelatedNewTransaction()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var hash = "0x" + new string('f', 64);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        await Journal.RecordObservationAsync(lease, hash, Verified(op, hash), TimeSpan.FromSeconds(5), Ct);
        clock.Advance(TimeSpan.FromSeconds(6));
        var reorged = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Reorged, reorged.Status);
        clock.Advance(TimeSpan.FromSeconds(6));
        var stopped = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.NeedsAttention, stopped.Status);
        Assert.Equal(hash, stopped.TransactionHash);
        Assert.Null(stopped.Nonce);
        Assert.Equal(0, ports.SendCalls);
    }

    [Fact]
    public async Task ExpiredWorkerCannotQuarantineAnOperationOwnedByItsReplacement()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var stale = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        clock.Advance(TimeSpan.FromMinutes(3));
        var current = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        var failure = await Assert.ThrowsAsync<EvmCertificateJournalException>(() =>
            Journal.MarkNeedsAttentionAsync(stale, EvmCertificateJournalFailure.SignerUnavailable, Ct));
        Assert.Equal(EvmCertificateJournalFailure.LeaseLost, failure.Failure);
        var currentState = await Journal.ScheduleCheckAsync(current, TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Queued, currentState.Status);
        Assert.Null(currentState.LastFailure);
    }

    [Fact]
    public async Task ARegularProofCheckClearsTransientFailureWithoutLosingIntent()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        await Journal.ScheduleRetryAsync(lease, EvmCertificateJournalFailure.RpcUnavailable, TimeSpan.FromSeconds(5), Ct);
        clock.Advance(TimeSpan.FromSeconds(6));
        var next = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        var scheduled = await Journal.ScheduleCheckAsync(next, TimeSpan.FromSeconds(5), Ct);
        Assert.Null(scheduled.LastFailure);
        Assert.Equal(op.CertificateHash, scheduled.CertificateHash);
        Assert.Equal(op.OwnerUserId, scheduled.OwnerUserId);
        Assert.Equal(op.TenantId, scheduled.TenantId);
        Assert.Null(scheduled.TransactionHash);
        Assert.Equal(EvmCertificatePublicationStatus.Queued, scheduled.Status);
    }

    [Fact]
    public async Task RepeatedUnacknowledgedSendsEventuallyUseABoundedReplacementWithTheSameNonce()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        ports.FailSendAfterAccept = true;
        var first = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        clock.Advance(TimeSpan.FromSeconds(31));
        var next = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        var attempts = await Journal.ListAttemptsAsync(op.Id, Ct);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(first.Nonce, next.Nonce);
        Assert.All(attempts, a => Assert.Null(a.BroadcastAt));
        Assert.True(attempts[1].MaximumFeePerGasWei > attempts[0].MaximumFeePerGasWei);
        Assert.Equal(2, ports.SignCalls);
    }

    [Fact]
    public async Task CallerCancellationRetainsThePreparedPayloadForAnExpiredLeaseRecovery()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        using var cancelled = new CancellationTokenSource();
        ports.BeforeSend = _ => { cancelled.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Worker().ProcessAsync(op.Id, cancelled.Token));
        var prepared = Assert.IsType<EvmCertificateJournalSnapshot>(await Journal.FindAsync(op.Id, Ct));
        var attempt = Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Null(attempt.BroadcastAt);
        ports.BeforeSend = null;
        ports.Proof = hash => Verified(prepared, hash);
        ports.SignerUnavailable = true;
        clock.Advance(TimeSpan.FromMinutes(3));
        var recovered = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, recovered.Status);
        Assert.Equal(attempt.TransactionHash, recovered.TransactionHash);
        Assert.Equal(1, ports.SignCalls);
    }

    [Fact]
    public async Task ANewPolicyCannotIncreaseTheBudgetAlreadyCapturedWithTheNonce()
    {
        var op = await Journal.EnqueueAsync(Request(), Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await Journal.TryAcquireAsync(op.Id, TimeSpan.FromMinutes(2), Ct));
        var captured = EvmCertificateFeeBudget.Create(100000, 20, 2000000);
        await Journal.ReserveNonceAsync(lease, "worker-signer", ports.Address, 7, captured, Ct);
        await Journal.ScheduleCheckAsync(lease, TimeSpan.FromSeconds(1), Ct);
        clock.Advance(TimeSpan.FromSeconds(2));
        await Worker().ProcessAsync(op.Id, Ct);
        clock.Advance(TimeSpan.FromSeconds(31));
        var stopped = Assert.IsType<EvmCertificateJournalSnapshot>(await Worker().ProcessAsync(op.Id, Ct));
        Assert.Equal(EvmCertificatePublicationStatus.NeedsAttention, stopped.Status);
        Assert.Equal(new BigInteger(20), stopped.Budget!.MaximumFeePerGasWei);
        Assert.Equal(new BigInteger(2000000), stopped.Budget.MaximumTransactionFeeWei);
        Assert.Single(await Journal.ListAttemptsAsync(op.Id, Ct));
        Assert.Equal(1, ports.SignCalls);
    }

    private EvmCertificatePublicationWorker Worker(int maximumAttempts = 3) => new(Journal, ports, ports, ports,
        EvmCertificatePublicationPolicy.Create(Network(), "worker-signer", Budget, 20, 2,
            TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30), maximumAttempts), clock);

    private static EvmCertificatePublicationNetwork Network() => EvmCertificatePublicationNetwork.Capture("worker-profile", new EvmCertificateNetworkOptions
    {
        ChainId = 12345, RpcEndpoint = "https://rpc.invalid/", ContractAddress = "0x" + new string('1', 40),
        IssuerId = "0x" + new string('2', 64), ExpectedRuntimeCodeSha256 = new string('3', 64),
        FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
    });

    private static EvmCertificateJournalRequest Request() => new(Network(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "worker-fixture", "0x" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), EvmCertificateOperationKind.Anchor);

    private EvmCertificateProofResult Verified(EvmCertificateJournalSnapshot op, string hash)
    {
        var block = new EvmCertificateBlockReference(12, "0x" + new string('a', 64));
        var n = op.Network;
        return new(EvmCertificateProofStatus.Verified, new(op.Kind, op.CertificateHash, hash, ports.Address, block,
            new(n.ChainId, n.ContractAddress, n.IssuerId, n.RuntimeCodeSha256, n.FinalityMode, n.MinimumConfirmations, block),
            new(ports.Address, "0x" + new string('0', 40), 12, 0)));
    }

    private sealed class WorkerClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 19, 22, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class WorkerPorts : IEvmCertificatePublicationRpc, IEvmCertificateTransactionSigner, IEvmCertificateProofVerifier
    {
        private readonly EthECKey key = EthECKey.GenerateKey();
        internal string Address => key.GetPublicAddress().ToLowerInvariant();
        internal int AddressCalls { get; private set; }
        internal int SignCalls { get; private set; }
        internal int SendCalls { get; private set; }
        internal bool FailSendAfterAccept { get; set; }
        internal bool SignerUnavailable { get; set; }
        internal bool WrongNetwork { get; set; }
        internal Func<string, Task>? BeforeSend { get; set; }
        internal Func<string, EvmCertificateProofResult> Proof { get; set; } = _ => new(EvmCertificateProofStatus.Pending);

        public Task<string> GetAddressAsync(string reference, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal("worker-signer", reference); AddressCalls++;
            if (SignerUnavailable) throw new EvmCertificateJournalException(EvmCertificateJournalFailure.SignerUnavailable);
            return Task.FromResult(Address);
        }

        public Task<string> SignAsync(string reference, EvmCertificatePreparedTransaction intent, BigInteger maxFee, BigInteger priority, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal("worker-signer", reference); SignCalls++;
            if (SignerUnavailable) throw new EvmCertificateJournalException(EvmCertificateJournalFailure.SignerUnavailable);
            var tx = new Transaction1559(intent.Network.ChainId, intent.Nonce, priority, maxFee, intent.Budget.GasLimit,
                intent.Network.ContractAddress, 0, intent.Input, []);
            return Task.FromResult("0x" + new Transaction1559Signer().SignTransaction(key, tx));
        }

        public Task<ulong> GetPendingNonceAsync(EvmCertificatePublicationNetwork network, string address, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal(Network().ChainId, network.ChainId); Assert.Equal(Address, address);
            return Task.FromResult(7UL);
        }

        public async Task<string> SendRawTransactionAsync(EvmCertificatePreparedTransaction intent, string raw, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal(Address, intent.SignerAddress); SendCalls++;
            if (BeforeSend is not null) await BeforeSend(raw);
            ct.ThrowIfCancellationRequested();
            if (FailSendAfterAccept) throw new EvmCertificateRpcException(EvmCertificateRpcFailure.Timeout);
            return "0x" + Convert.ToHexString(Nethereum.Util.Sha3Keccack.Current.CalculateHash(Convert.FromHexString(raw[2..]))).ToLowerInvariant();
        }

        public Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync() => ValidateNetworkAsync(Ct);
        public Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); var n = Network();
            return Task.FromResult(new EvmCertificateNetworkIdentity(WrongNetwork ? 1 : n.ChainId, n.ContractAddress, n.IssuerId,
                n.RuntimeCodeSha256, n.FinalityMode, n.MinimumConfirmations, new(12, "0x" + new string('a', 64))));
        }
        public Task<EvmCertificateProofResult> VerifyAnchorAsync(string digest, string hash) => VerifyAnchorAsync(digest, hash, Ct);
        public Task<EvmCertificateProofResult> VerifyAnchorAsync(string digest, string hash, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal(66, digest.Length); return Task.FromResult(Proof(hash));
        }
        public Task<EvmCertificateProofResult> VerifyRevocationAsync(string digest, string hash) => VerifyRevocationAsync(digest, hash, Ct);
        public Task<EvmCertificateProofResult> VerifyRevocationAsync(string digest, string hash, CancellationToken ct) => VerifyAnchorAsync(digest, hash, ct);
    }
}
