using System.Net.Security;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.CertificateAnchoring.NativeTests;

/// <summary>Requires the hosted Anvil/TLS fixture; missing configuration fails instead of silently skipping.</summary>
public sealed class EvmCertificateNativeTests : IDisposable
{
    private readonly NativeFixture fixture;
    private readonly EvmCertificateRpcClient rpc;

    public EvmCertificateNativeTests()
    {
        var path = Environment.GetEnvironmentVariable("GAMEGUILD_EVM_NATIVE_FIXTURE");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("The hosted native EVM fixture is required.");
        fixture = JsonSerializer.Deserialize<NativeFixture>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                  ?? throw new InvalidOperationException("The native EVM fixture is invalid.");
        var endpoint = new Uri(fixture.RpcEndpoint, UriKind.Absolute);
        if (!endpoint.IsLoopback || endpoint.Scheme != Uri.UriSchemeHttps || fixture.ChainId != 12345)
        {
            throw new InvalidOperationException("Native tests require the isolated loopback HTTPS chain fixture.");
        }

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
                certificate is not null && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None &&
                Convert.ToHexString(SHA256.HashData(certificate.RawData)).Equals(fixture.ServerCertificateSha256, StringComparison.OrdinalIgnoreCase)
        };
        rpc = new EvmCertificateRpcClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, Options());
    }

    [Fact]
    public async Task ActualAnchorReceiptEventAndDeployedImmutableCodeVerifyOverHttps()
    {
        var verifier = new EvmCertificateProofVerifier(rpc, Options());
        var result = await verifier.VerifyAnchorAsync(fixture.ActiveCertificateHash, fixture.ActiveAnchorTransactionHash);
        Assert.Equal(EvmCertificateProofStatus.Verified, result.Status);
        var evidence = Assert.IsType<EvmCertificateEvidence>(result.Evidence);
        Assert.Equal(fixture.SignerAddress.ToLowerInvariant(), evidence.Signer);
        Assert.Equal(fixture.ExpectedRuntimeCodeSha256, evidence.Network.RuntimeCodeSha256);
        Assert.Equal(fixture.ContractAddress.ToLowerInvariant(), evidence.Network.ContractAddress);
        Assert.True(evidence.TransactionBlock.Number > 0);
    }

    [Fact]
    public async Task ActualRevocationInvalidatesTheOriginalAnchorProof()
    {
        var verifier = new EvmCertificateProofVerifier(rpc, Options());
        var result = await verifier.VerifyAnchorAsync(fixture.RevokedCertificateHash, fixture.RevokedAnchorTransactionHash);
        Assert.Equal(EvmCertificateProofStatus.Revoked, result.Status);
    }

    [Fact]
    public async Task ActualRevocationReceiptMatchesStoredContractRevokerAndBlock()
    {
        var verifier = new EvmCertificateProofVerifier(rpc, Options());
        var result = await verifier.VerifyRevocationAsync(fixture.RevokedCertificateHash, fixture.RevokeTransactionHash);
        Assert.Equal(EvmCertificateProofStatus.Verified, result.Status);
        var evidence = Assert.IsType<EvmCertificateEvidence>(result.Evidence);
        Assert.Equal(fixture.SignerAddress.ToLowerInvariant(), evidence.State.RevokeSigner);
        Assert.Equal(evidence.TransactionBlock.Number, evidence.State.RevokedAtBlock);
    }

    [Fact]
    public async Task ActualChainCannotBeAcceptedWithAnIncorrectConfiguredCodeDigest()
    {
        var options = Options();
        options.ExpectedRuntimeCodeSha256 = new string('0', 64);
        var verifier = new EvmCertificateProofVerifier(rpc, options);
        var error = await Assert.ThrowsAsync<EvmCertificateEvidenceException>(() => verifier.ValidateNetworkAsync());
        Assert.Equal(EvmCertificateEvidenceFailure.RuntimeCodeMismatch, error.Failure);
    }

    [Fact]
    public async Task ActualChainCannotBeAcceptedAsAnotherNetwork()
    {
        var options = Options();
        options.ChainId = 12346;
        var verifier = new EvmCertificateProofVerifier(rpc, options);
        var error = await Assert.ThrowsAsync<EvmCertificateEvidenceException>(() => verifier.ValidateNetworkAsync());
        Assert.Equal(EvmCertificateEvidenceFailure.NetworkMismatch, error.Failure);
    }

    public void Dispose() => rpc.Dispose();

    private EvmCertificateNetworkOptions Options() => new()
    {
        ChainId = fixture.ChainId,
        RpcEndpoint = fixture.RpcEndpoint,
        ContractAddress = fixture.ContractAddress,
        IssuerId = fixture.IssuerId,
        ExpectedRuntimeCodeSha256 = fixture.ExpectedRuntimeCodeSha256,
        FinalityMode = EvmCertificateFinalityMode.Confirmations,
        MinimumConfirmations = 1
    };

    private sealed record NativeFixture(
        long ChainId,
        string RpcEndpoint,
        string ServerCertificateSha256,
        string ContractAddress,
        string IssuerId,
        string ExpectedRuntimeCodeSha256,
        string SignerAddress,
        string ActiveCertificateHash,
        string ActiveAnchorTransactionHash,
        string RevokedCertificateHash,
        string RevokedAnchorTransactionHash,
        string RevokeTransactionHash);
}
