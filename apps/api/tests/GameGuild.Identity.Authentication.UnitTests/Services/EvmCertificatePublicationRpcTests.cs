using System.Net;
using System.Text;
using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificatePublicationRpcTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task PendingNonceUsesTheValidatedNetworkAndAnExactPendingAddressQuery()
    {
        var options = Options(); var network = Network(options); var verifier = new NetworkVerifier(network);
        var handler = new CaptureHandler(root => Reply(root, "0x7"));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, verifier);
        Assert.Equal(7UL, await rpc.GetPendingNonceAsync(network, "0x" + new string('A', 40), Ct));
        Assert.Equal(1, verifier.Validations);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("eth_getTransactionCount", request.GetProperty("method").GetString());
        Assert.Equal("0x" + new string('a', 40), request.GetProperty("params")[0].GetString());
        Assert.Equal("pending", request.GetProperty("params")[1].GetString());
    }

    [Theory]
    [InlineData("0x00")]
    [InlineData("0X1")]
    [InlineData("0x-1")]
    [InlineData("1")]
    [InlineData("0x")]
    [InlineData("0x10000000000000000")]
    public async Task PendingNonceRejectsNonCanonicalOrOverflowingQuantities(string quantity)
    {
        var options = Options(); var network = Network(options);
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(new CaptureHandler(root => Reply(root, quantity))), options, new NetworkVerifier(network));
        await Assert.ThrowsAsync<EvmCertificateEvidenceException>(() => rpc.GetPendingNonceAsync(network, EthECKey.GenerateKey().GetPublicAddress(), Ct));
    }

    [Theory]
    [InlineData("chain")]
    [InlineData("contract")]
    [InlineData("issuer")]
    [InlineData("code")]
    [InlineData("finality")]
    [InlineData("confirmations")]
    public async Task AChangedNetworkIdentityPreventsEveryWritePortRequest(string field)
    {
        var options = Options(); var network = Network(options); var verifier = new NetworkVerifier(network);
        verifier.Identity = field switch
        {
            "chain" => verifier.Identity with { ChainId = 12346 },
            "contract" => verifier.Identity with { ContractAddress = "0x" + new string('a', 40) },
            "issuer" => verifier.Identity with { IssuerId = "0x" + new string('a', 64) },
            "code" => verifier.Identity with { RuntimeCodeSha256 = new string('a', 64) },
            "finality" => verifier.Identity with { FinalityMode = EvmCertificateFinalityMode.Finalized },
            _ => verifier.Identity with { MinimumConfirmations = 2 }
        };
        var handler = new CaptureHandler(root => Reply(root, "0x7"));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, verifier);
        var error = await Assert.ThrowsAsync<EvmCertificateEvidenceException>(() => rpc.GetPendingNonceAsync(network, EthECKey.GenerateKey().GetPublicAddress(), Ct));
        Assert.Equal(EvmCertificateEvidenceFailure.NetworkMismatch, error.Failure);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(EvmCertificateOperationKind.Anchor)]
    [InlineData(EvmCertificateOperationKind.Revoke)]
    public async Task BroadcastSendsOnlyTheBoundEnvelopeAndRequiresItsCalculatedHash(EvmCertificateOperationKind kind)
    {
        var key = EthECKey.GenerateKey(); var options = Options(); var prepared = Preparation(key, options, kind);
        var raw = Sign(key, prepared); var expected = EvmCertificateSignedEnvelope.Validate(raw, prepared);
        var handler = new CaptureHandler(root => Reply(root, expected.TransactionHash));
        var verifier = new NetworkVerifier(prepared.Network);
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, verifier);
        Assert.Equal(expected.TransactionHash, await rpc.SendRawTransactionAsync(prepared, raw, Ct));
        Assert.Equal(1, verifier.Validations);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("eth_sendRawTransaction", request.GetProperty("method").GetString());
        Assert.Equal(1, request.GetProperty("params").GetArrayLength());
        Assert.Equal(expected.RawTransaction, request.GetProperty("params")[0].GetString());
    }

    [Fact]
    public async Task AProviderCannotReplaceTheHashOfTheSignedPayload()
    {
        var key = EthECKey.GenerateKey(); var options = Options(); var prepared = Preparation(key, options, EvmCertificateOperationKind.Anchor);
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(new CaptureHandler(root => Reply(root, "0x" + new string('a', 64)))), options, new NetworkVerifier(prepared.Network));
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => rpc.SendRawTransactionAsync(prepared, Sign(key, prepared), Ct));
        Assert.Equal(EvmCertificateRpcFailure.InvalidResponse, error.Failure);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task InvalidEnvelopeIsRejectedBeforeNetworkValidationOrBroadcast()
    {
        var key = EthECKey.GenerateKey(); var options = Options(); var prepared = Preparation(key, options, EvmCertificateOperationKind.Anchor);
        var verifier = new NetworkVerifier(prepared.Network); var handler = new CaptureHandler(root => Reply(root, "0x7"));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, verifier);
        await Assert.ThrowsAsync<EvmCertificateJournalException>(() => rpc.SendRawTransactionAsync(prepared, "0x02c0", Ct));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, verifier.Validations);
    }

    [Fact]
    public async Task AnotherCapturedNetworkCannotUseTheConfiguredEndpoint()
    {
        var options = Options(); var configured = Network(options); var otherOptions = Options(); otherOptions.ChainId = 12346;
        var verifier = new NetworkVerifier(configured); var handler = new CaptureHandler(root => Reply(root, "0x7"));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, verifier);
        await Assert.ThrowsAsync<EvmCertificateEvidenceException>(() => rpc.GetPendingNonceAsync(Network(otherOptions), EthECKey.GenerateKey().GetPublicAddress(), Ct));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, verifier.Validations);
    }

    [Fact]
    public async Task ConfigurationChangesCannotRedirectAnExistingTransport()
    {
        var options = Options(); var captured = Network(options); var handler = new CaptureHandler(root => Reply(root, "0x7"));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, new NetworkVerifier(captured));
        options.RpcEndpoint = "https://changed.invalid/"; options.ChainId = 12346;
        Assert.Equal(7UL, await rpc.GetPendingNonceAsync(captured, EthECKey.GenerateKey().GetPublicAddress(), Ct));
        Assert.Equal("https://rpc.invalid/", handler.Endpoint);
    }

    [Fact]
    public async Task ProviderErrorsNeverEchoUntrustedMessagesOrEndpointValues()
    {
        var options = Options(); var network = Network(options); var sentinel = "provider-value-" + Guid.NewGuid().ToString("N");
        var handler = new CaptureHandler(root => JsonSerializer.Serialize(new { jsonrpc = "2.0", id = root.GetProperty("id").GetInt64(), error = new { code = -32000, message = sentinel } }));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, new NetworkVerifier(network));
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => rpc.GetPendingNonceAsync(network, EthECKey.GenerateKey().GetPublicAddress(), Ct));
        Assert.Equal(EvmCertificateRpcFailure.RpcFailure, error.Failure);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(sentinel, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.RpcEndpoint, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationPreventsAnyRequest()
    {
        var options = Options(); var network = Network(options); var verifier = new NetworkVerifier(network);
        var handler = new CaptureHandler(root => Reply(root, "0x7"));
        using var rpc = new EvmCertificatePublicationRpcClient(new HttpClient(handler), options, verifier);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rpc.GetPendingNonceAsync(network, EthECKey.GenerateKey().GetPublicAddress(), cancellation.Token));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, verifier.Validations);
    }

    private static EvmCertificateNetworkOptions Options() => new()
    {
        ChainId = 12345, RpcEndpoint = "https://rpc.invalid/", ContractAddress = "0x" + new string('1', 40),
        IssuerId = "0x" + new string('2', 64), ExpectedRuntimeCodeSha256 = new string('3', 64),
        FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
    };
    private static EvmCertificatePublicationNetwork Network(EvmCertificateNetworkOptions options) => EvmCertificatePublicationNetwork.Capture("issuer-a", options);
    private static EvmCertificatePreparedTransaction Preparation(EthECKey key, EvmCertificateNetworkOptions options, EvmCertificateOperationKind kind) =>
        new(Network(options), kind, "0x" + new string('4', 64), key.GetPublicAddress().ToLowerInvariant(), 7, EvmCertificateFeeBudget.Create(100000, 10, 1000000));
    private static string Sign(EthECKey key, EvmCertificatePreparedTransaction prepared) => "0x" + new Transaction1559Signer().SignTransaction(key,
        new Transaction1559(prepared.Network.ChainId, prepared.Nonce, 1, 2, prepared.Budget.GasLimit, prepared.Network.ContractAddress, 0, prepared.Input, []));
    private static string Reply(JsonElement root, object? value) => JsonSerializer.Serialize(new { jsonrpc = "2.0", id = root.GetProperty("id").GetInt64(), result = value });

    private sealed class CaptureHandler(Func<JsonElement, string> reply) : HttpMessageHandler
    {
        internal List<JsonElement> Requests { get; } = [];
        internal string? Endpoint { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Endpoint = request.RequestUri!.AbsoluteUri;
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Requests.Add(json.RootElement.Clone());
            return new(HttpStatusCode.OK) { Content = new StringContent(reply(json.RootElement), Encoding.UTF8, "application/json") };
        }
    }

    private sealed class NetworkVerifier : IEvmCertificateProofVerifier
    {
        internal NetworkVerifier(EvmCertificatePublicationNetwork network) => Identity = new(network.ChainId, network.ContractAddress,
            network.IssuerId, network.RuntimeCodeSha256, network.FinalityMode, network.MinimumConfirmations, new(1, "0x" + new string('a', 64)));
        internal EvmCertificateNetworkIdentity Identity { get; set; }
        internal int Validations { get; private set; }
        public Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync() => ValidateNetworkAsync(Ct);
        public Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Validations++; return Task.FromResult(Identity);
        }
        public Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash) =>
            VerifyAnchorAsync(certificateHash, transactionHash, Ct);
        public Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(certificateHash);
            ArgumentException.ThrowIfNullOrWhiteSpace(transactionHash);
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("This fixture supports network validation only.");
        }
        public Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash) =>
            VerifyAnchorAsync(certificateHash, transactionHash, Ct);
        public Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken) =>
            VerifyAnchorAsync(certificateHash, transactionHash, cancellationToken);
    }
}
