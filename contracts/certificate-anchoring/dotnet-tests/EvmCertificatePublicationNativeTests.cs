using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.CertificateAnchoring.NativeTests;

/// <summary>Actual signer/journal/RPC/EVM interoperability. This does not claim host-worker or external testnet acceptance.</summary>
public sealed class EvmCertificatePublicationNativeTests(JournalPostgreSqlFixture database) : IClassFixture<JournalPostgreSqlFixture>
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan LeaseTime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckDelay = TimeSpan.FromSeconds(1);
    private readonly NativeFixture fixture = ReadFixture();

    [Fact]
    public async Task SoftwareSignerPublishesOnlyAfterTheSignedPayloadIsDurableAndTheActualAnchorVerifies()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var journal = new EvmCertificateJournal(database, TimeProvider.System);
        var request = Request();
        var published = await Publish(scope.ServiceProvider, journal, request);
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, published.Status);
        var attempt = Assert.Single(await journal.ListAttemptsAsync(published.Id, Ct));
        Assert.NotNull(attempt.BroadcastAt);
        var proof = await scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>()
            .VerifyAnchorAsync(request.CertificateHash, attempt.TransactionHash, Ct);
        Assert.Equal(EvmCertificateProofStatus.Verified, proof.Status);
        Assert.Equal(fixture.SignerAddress.ToLowerInvariant(), proof.Evidence!.Signer);
    }

    [Fact]
    public async Task ActualRevocationUsesTheOriginalDigestAndInvalidatesItsAnchor()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var journal = new EvmCertificateJournal(database, TimeProvider.System);
        var request = Request();
        var anchor = await Publish(scope.ServiceProvider, journal, request);
        var revoked = await Publish(scope.ServiceProvider, journal, request with { Kind = EvmCertificateOperationKind.Revoke });
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, revoked.Status);
        Assert.Equal(anchor.CertificateHash, revoked.CertificateHash);
        Assert.NotEqual(anchor.TransactionHash, revoked.TransactionHash);
        var proof = await scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>()
            .VerifyAnchorAsync(anchor.CertificateHash, anchor.TransactionHash!, Ct);
        Assert.Equal(EvmCertificateProofStatus.Revoked, proof.Status);
        Assert.True(proof.Evidence!.State.RevokedAtBlock >= proof.Evidence.State.AnchoredAtBlock);
    }

    [Fact]
    public async Task ANewWorkerInstanceRecoversThePersistedHashAfterAnAcceptedButUnacknowledgedSend()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var clock = new RecoveryClock();
        var journal = new EvmCertificateJournal(database, clock);
        var request = Request();
        var operation = await journal.EnqueueAsync(request, Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await journal.TryAcquireAsync(operation.Id, LeaseTime, Ct));
        var prepared = await Prepare(scope.ServiceProvider, journal, lease);
        // The node really accepts this transaction. Deliberately omit the acknowledgement to the journal.
        var rpc = scope.ServiceProvider.GetRequiredService<IEvmCertificatePublicationRpc>();
        var actualHash = await rpc.SendRawTransactionAsync(prepared.Operation.PreparedTransaction, prepared.Attempt.RawTransaction, Ct);
        Assert.Equal(prepared.Attempt.TransactionHash, actualHash);
        Assert.Equal(EvmCertificatePublicationStatus.Prepared, (await journal.FindAsync(operation.Id, Ct))!.Status);
        Assert.Null(Assert.Single(await journal.ListAttemptsAsync(operation.Id, Ct)).BroadcastAt);
        // Simulate lease expiry and a fresh worker/service instance; no second transaction is submitted.
        clock.Advance(LeaseTime + TimeSpan.FromSeconds(1));
        var recovered = new EvmCertificateJournal(database, clock);
        var next = Assert.IsType<EvmCertificateJournalLease>(await recovered.TryAcquireAsync(operation.Id, LeaseTime, Ct));
        Assert.NotEqual(lease.Token, next.Token);
        var persisted = Assert.Single(await recovered.ListAttemptsAsync(operation.Id, Ct));
        var proof = await WaitForProof(scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>(), request, persisted.TransactionHash);
        var confirmed = await recovered.RecordObservationAsync(next, persisted.TransactionHash, proof, CheckDelay, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Confirmed, confirmed.Status);
        Assert.Equal(actualHash, confirmed.TransactionHash);
        Assert.Equal(prepared.Operation.Nonce, confirmed.Nonce);
        WriteEvidence("recovery", confirmed, proof);
    }

    [Fact]
    public async Task RegisteredTransportSuppressesHttpActivitiesAndDiagnosticRequestEventsWithAPositiveControl()
    {
        using var root = new Activity("native-evm-transport-privacy").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var observed = new HttpObservations(root.TraceId, root.SpanId);
        using var activities = observed.CreateActivityListener();
        ActivitySource.AddActivityListener(activities);
        using (var control = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false,
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = ValidateCertificate }
        }))
        {
            using var response = await control.PostAsync(fixture.RpcEndpoint,
                new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_chainId\",\"params\":[]}", Encoding.UTF8, "application/json"), Ct);
            response.EnsureSuccessStatusCode();
        }
        Assert.NotEmpty(observed.Activities);
        Assert.NotEmpty(observed.RequestEvents);
        var beforeActivities = observed.Activities.Count;
        var beforeEvents = observed.RequestEvents.Count;
        using var services = Services(); using var scope = services.CreateScope();
        var network = EvmCertificatePublicationNetwork.Capture("native-issuer", Options());
        var nonce = await scope.ServiceProvider.GetRequiredService<IEvmCertificatePublicationRpc>()
            .GetPendingNonceAsync(network, fixture.SignerAddress, Ct);
        Assert.True(nonce > 0);
        Assert.Equal(beforeActivities, observed.Activities.Count);
        Assert.Equal(beforeEvents, observed.RequestEvents.Count);
    }

    [Fact]
    public async Task ProductionWorkerPublishesAndFinalizesAnActualAnchor()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var clock = new RecoveryClock();
        var journal = new EvmCertificateJournal(database, clock);
        var request = Request();
        var queued = await journal.EnqueueAsync(request, Ct);
        var confirmed = await WaitForWorker(Worker(scope.ServiceProvider, journal, clock), journal, queued.Id, clock);
        var proof = await scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>()
            .VerifyAnchorAsync(request.CertificateHash, confirmed.TransactionHash!, Ct);
        Assert.Equal(EvmCertificateProofStatus.Verified, proof.Status);
        WriteEvidence("worker-anchor", confirmed, proof);
    }

    [Fact]
    public async Task ProductionWorkerRevokesTheSameOriginalDigestOnTheActualRegistry()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var clock = new RecoveryClock();
        var journal = new EvmCertificateJournal(database, clock);
        var request = Request();
        var worker = Worker(scope.ServiceProvider, journal, clock);
        var queued = await journal.EnqueueAsync(request, Ct);
        var anchor = await WaitForWorker(worker, journal, queued.Id, clock);
        var anchored = await scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>()
            .VerifyAnchorAsync(request.CertificateHash, anchor.TransactionHash!, Ct);
        WriteEvidence("worker-before-revoke", anchor, anchored);
        var revoke = await journal.EnqueueAsync(request with { Kind = EvmCertificateOperationKind.Revoke }, Ct);
        var revoked = await WaitForWorker(worker, journal, revoke.Id, clock);
        var verifier = scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>();
        var proof = await verifier.VerifyRevocationAsync(request.CertificateHash, revoked.TransactionHash!, Ct);
        Assert.Equal(EvmCertificateProofStatus.Verified, proof.Status);
        Assert.Equal(EvmCertificateProofStatus.Revoked, (await verifier.VerifyAnchorAsync(request.CertificateHash, anchor.TransactionHash!, Ct)).Status);
        WriteEvidence("worker-revoke", revoked, proof);
    }

    [Fact]
    public async Task ProductionWorkerRecoversAnActualSendThatNeverAcknowledgedItsJournalAttempt()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var clock = new RecoveryClock();
        var journal = new EvmCertificateJournal(database, clock);
        var request = Request();
        var queued = await journal.EnqueueAsync(request, Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await journal.TryAcquireAsync(queued.Id, LeaseTime, Ct));
        var prepared = await Prepare(scope.ServiceProvider, journal, lease);
        var hash = await scope.ServiceProvider.GetRequiredService<IEvmCertificatePublicationRpc>()
            .SendRawTransactionAsync(prepared.Operation.PreparedTransaction, prepared.Attempt.RawTransaction, Ct);
        Assert.Null(Assert.Single(await journal.ListAttemptsAsync(queued.Id, Ct)).BroadcastAt);
        clock.Advance(LeaseTime + TimeSpan.FromSeconds(1));
        var fresh = new EvmCertificateJournal(database, clock);
        var confirmed = await WaitForWorker(Worker(scope.ServiceProvider, fresh, clock), fresh, queued.Id, clock);
        Assert.Equal(hash, confirmed.TransactionHash);
        Assert.Single(await fresh.ListAttemptsAsync(queued.Id, Ct));
        var proof = await scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>()
            .VerifyAnchorAsync(request.CertificateHash, hash, Ct);
        WriteEvidence("worker-recovery", confirmed, proof);
    }

    [Fact]
    public async Task ExplicitHostedWorkerActuallyPublishesFromTheDurableQueueAndStopsCleanly()
    {
        using var services = Services(collection =>
        {
            collection.AddLogging();
            collection.AddScoped(_ => new EvmCertificateJournal(database, TimeProvider.System));
            collection.AddEvmCertificatePublicationWorker(WorkerPolicy());
        });
        var journal = new EvmCertificateJournal(database, TimeProvider.System);
        var request = Request();
        var queued = await journal.EnqueueAsync(request, Ct);
        var hosted = Assert.Single(services.GetServices<Microsoft.Extensions.Hosting.IHostedService>());
        await hosted.StartAsync(Ct);
        try
        {
            EvmCertificateJournalSnapshot? confirmed = null;
            for (var i = 0; i < 400; i++)
            {
                var observed = await journal.FindAsync(queued.Id, Ct);
                if (observed?.Status == EvmCertificatePublicationStatus.Confirmed) { confirmed = observed; break; }
                Assert.NotEqual(EvmCertificatePublicationStatus.NeedsAttention, observed?.Status);
                await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
            }
            Assert.NotNull(confirmed);
            using var scope = services.CreateScope();
            var proof = await scope.ServiceProvider.GetRequiredService<IEvmCertificateProofVerifier>()
                .VerifyAnchorAsync(request.CertificateHash, confirmed.TransactionHash!, Ct);
            Assert.Equal(EvmCertificateProofStatus.Verified, proof.Status);
            WriteEvidence("hosted-worker", confirmed, proof);
        }
        finally { await hosted.StopAsync(Ct); }
    }

    private EvmCertificatePublicationPolicy WorkerPolicy() => EvmCertificatePublicationPolicy.Create(
        EvmCertificatePublicationNetwork.Capture("native-issuer", Options()), "native-issuer-v1",
        EvmCertificateFeeBudget.Create(250000, 10000000000L, 250000L * 10000000000L),
        10000000000L, 1000000000L, CheckDelay, LeaseTime, TimeSpan.FromSeconds(30), 3);

    private EvmCertificatePublicationWorker Worker(IServiceProvider services, EvmCertificateJournal journal, TimeProvider clock) => new(
        journal, services.GetRequiredService<IEvmCertificatePublicationRpc>(), services.GetRequiredService<IEvmCertificateTransactionSigner>(),
        services.GetRequiredService<IEvmCertificateProofVerifier>(), WorkerPolicy(), clock);

    private static async Task<EvmCertificateJournalSnapshot> WaitForWorker(EvmCertificatePublicationWorker worker,
        EvmCertificateJournal journal, Guid id, RecoveryClock clock)
    {
        for (var i = 0; i < 100; i++)
        {
            await worker.ProcessAsync(id, Ct);
            var operation = Assert.IsType<EvmCertificateJournalSnapshot>(await journal.FindAsync(id, Ct));
            if (operation.Status == EvmCertificatePublicationStatus.Confirmed) return operation;
            Assert.NotEqual(EvmCertificatePublicationStatus.NeedsAttention, operation.Status);
            clock.Advance(CheckDelay);
            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
        throw new InvalidOperationException("The production worker did not observe the isolated registry's finalized operation.");
    }

    private async Task<EvmCertificateJournalSnapshot> Publish(IServiceProvider services, EvmCertificateJournal journal, EvmCertificateJournalRequest request)
    {
        var queued = await journal.EnqueueAsync(request, Ct);
        var lease = Assert.IsType<EvmCertificateJournalLease>(await journal.TryAcquireAsync(queued.Id, LeaseTime, Ct));
        var prepared = await Prepare(services, journal, lease);
        var hash = await services.GetRequiredService<IEvmCertificatePublicationRpc>()
            .SendRawTransactionAsync(prepared.Operation.PreparedTransaction, prepared.Attempt.RawTransaction, Ct);
        await journal.RecordBroadcastAsync(lease, hash, Ct);
        var verifier = services.GetRequiredService<IEvmCertificateProofVerifier>();
        var proof = await WaitForProof(verifier, request, hash);
        Assert.Equal(EvmCertificateProofStatus.Verified, proof.Status);
        var confirmed = await journal.RecordObservationAsync(lease, hash, proof, CheckDelay, Ct);
        WriteEvidence(request.Kind == EvmCertificateOperationKind.Anchor ? "anchor" : "revoke", confirmed, proof);
        return confirmed;
    }

    private static async Task<(EvmCertificateJournalSnapshot Operation, EvmCertificateJournalAttempt Attempt)> Prepare(
        IServiceProvider services, EvmCertificateJournal journal, EvmCertificateJournalLease lease)
    {
        var signer = services.GetRequiredService<IEvmCertificateTransactionSigner>();
        var address = await signer.GetAddressAsync("native-issuer-v1", Ct);
        var queued = Assert.IsType<EvmCertificateJournalSnapshot>(await journal.FindAsync(lease.OperationId, Ct));
        var pending = await services.GetRequiredService<IEvmCertificatePublicationRpc>().GetPendingNonceAsync(queued.Network, address, Ct);
        var budget = EvmCertificateFeeBudget.Create(250000, 10000000000L, 250000L * 10000000000L);
        var operation = await journal.ReserveNonceAsync(lease, "native-issuer-v1", address, pending, budget, Ct);
        var raw = await signer.SignAsync("native-issuer-v1", operation.PreparedTransaction, 10000000000L, 1000000000L, Ct);
        var attempt = await journal.StoreSignedAttemptAsync(lease, raw, Ct);
        Assert.Equal(raw, Assert.Single(await journal.ListAttemptsAsync(operation.Id, Ct)).RawTransaction);
        return (operation, attempt);
    }

    private static async Task<EvmCertificateProofResult> WaitForProof(IEvmCertificateProofVerifier verifier,
        EvmCertificateJournalRequest request, string hash)
    {
        for (var i = 0; i < 100; i++)
        {
            var proof = request.Kind == EvmCertificateOperationKind.Anchor
                ? await verifier.VerifyAnchorAsync(request.CertificateHash, hash, Ct)
                : await verifier.VerifyRevocationAsync(request.CertificateHash, hash, Ct);
            if (proof.Status is not EvmCertificateProofStatus.Pending and not EvmCertificateProofStatus.NotFinalized and not EvmCertificateProofStatus.Reorged)
            {
                return proof;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
        throw new InvalidOperationException("The isolated EVM fixture did not yield a finalized operation proof.");
    }

    private ServiceProvider Services(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddEvmCertificatePublicationTransport(Options());
        services.AddHttpClient(EvmCertificateRpcClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler((handler, _) =>
            {
                var sockets = Assert.IsType<SocketsHttpHandler>(handler);
                Assert.Null(sockets.ActivityHeadersPropagator);
                // Test-only trust for this isolated, hash-pinned loopback fixture. Production trust is untouched.
                sockets.SslOptions.RemoteCertificateValidationCallback = ValidateCertificate;
            });
        services.AddSingleton<IEvmCertificatePrivateKeySource>(new EvmCertificateEnvironmentKeySource(
            new Dictionary<string, string> { ["native-issuer-v1"] = "GAMEGUILD_EVM_NATIVE_SIGNER_PRIVATE_KEY" }));
        services.AddScoped<IEvmCertificateTransactionSigner, EvmCertificateSoftwareSigner>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private bool ValidateCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain, SslPolicyErrors errors) =>
        sender is SslStream && chain is not null && certificate is not null &&
        (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None &&
        Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())).Equals(fixture.ServerCertificateSha256, StringComparison.OrdinalIgnoreCase);

    private EvmCertificateJournalRequest Request() => new(EvmCertificatePublicationNetwork.Capture("native-issuer", Options()),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "native-publisher", "0x" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), EvmCertificateOperationKind.Anchor);

    private EvmCertificateNetworkOptions Options() => new()
    {
        ChainId = fixture.ChainId, RpcEndpoint = fixture.RpcEndpoint, ContractAddress = fixture.ContractAddress,
        IssuerId = fixture.IssuerId, ExpectedRuntimeCodeSha256 = fixture.ExpectedRuntimeCodeSha256,
        FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
    };

    private static void WriteEvidence(string scenario, EvmCertificateJournalSnapshot operation, EvmCertificateProofResult proof)
    {
        var fixturePath = Environment.GetEnvironmentVariable("GAMEGUILD_EVM_NATIVE_FIXTURE")
            ?? throw new InvalidOperationException("The hosted evidence directory is required.");
        var directory = Path.GetDirectoryName(fixturePath)
            ?? throw new InvalidOperationException("The hosted evidence directory is invalid.");
        // Explicit public projection: no private key, credential source, endpoint or raw environment value.
        var evidence = new
        {
            scenario, operation = operation.Kind.ToString(), status = operation.Status.ToString(),
            operation.Network.ChainId, operation.Network.ContractAddress, operation.CertificateHash,
            operation.TransactionHash, operation.Nonce, evidence = proof.Evidence
        };
        File.WriteAllText(Path.Combine(directory, "publication-" + Guid.NewGuid().ToString("N") + ".json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private static NativeFixture ReadFixture()
    {
        var path = Environment.GetEnvironmentVariable("GAMEGUILD_EVM_NATIVE_FIXTURE");
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("The hosted native EVM fixture is required.");
        }
        var value = JsonSerializer.Deserialize<NativeFixture>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The native EVM fixture is invalid.");
        var uri = new Uri(value.RpcEndpoint, UriKind.Absolute);
        if (!uri.IsLoopback || uri.Scheme != Uri.UriSchemeHttps || value.ChainId != 12345)
        {
            throw new InvalidOperationException("Native publication tests require the isolated loopback HTTPS fixture.");
        }
        return value;
    }

    private sealed record NativeFixture(long ChainId, string RpcEndpoint, string ServerCertificateSha256, string ContractAddress,
        string IssuerId, string ExpectedRuntimeCodeSha256, string SignerAddress);

    private sealed class RecoveryClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class HttpObservations : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private readonly ActivityTraceId trace;
        private readonly ActivitySpanId parentSpan;
        private readonly IDisposable allListeners;
        private readonly ConcurrentBag<IDisposable> subscriptions = [];
        internal ConcurrentBag<string> Activities { get; } = [];
        internal ConcurrentBag<string> RequestEvents { get; } = [];

        internal HttpObservations(ActivityTraceId trace, ActivitySpanId parentSpan)
        {
            this.trace = trace;
            this.parentSpan = parentSpan;
            allListeners = DiagnosticListener.AllListeners.Subscribe(this);
        }

        internal ActivityListener CreateActivityListener() => new()
        {
            ShouldListenTo = source => source.Name == "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == trace && activity.ParentSpanId == parentSpan)
                {
                    Activities.Add(activity.OperationName);
                }
            }
        };

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "HttpHandlerDiagnosticListener")
            {
                subscriptions.Add(value.Subscribe(this));
            }
        }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            var current = Activity.Current;
            if (current is not null && current.TraceId == trace && (current.SpanId == parentSpan || current.ParentSpanId == parentSpan))
            {
                RequestEvents.Add(value.Key);
            }
        }
        public void OnCompleted()
        {
            // Diagnostic completion owns no resources; subscriptions are released by Dispose.
        }
        public void OnError(Exception error) => throw new InvalidOperationException("The native diagnostic observer failed.", error);
        public void Dispose()
        {
            allListeners.Dispose();
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
