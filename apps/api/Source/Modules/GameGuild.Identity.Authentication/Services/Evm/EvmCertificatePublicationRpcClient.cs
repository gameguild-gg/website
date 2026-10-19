using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>Network-bound pending nonce and signed broadcast port. It never accepts an arbitrary RPC method.</summary>
public sealed class EvmCertificatePublicationRpcClient : IEvmCertificatePublicationRpc, IDisposable
{
    private readonly EvmCertificateRpcTransport transport;
    private readonly EvmCertificatePublicationNetwork configured;
    private readonly IEvmCertificateProofVerifier verifier;

    public EvmCertificatePublicationRpcClient(HttpClient http, EvmCertificateNetworkOptions options, IEvmCertificateProofVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        configured = EvmCertificatePublicationNetwork.Capture("transport", options);
        transport = new EvmCertificateRpcTransport(http, options);
        this.verifier = verifier;
    }

    public async Task<ulong> GetPendingNonceAsync(EvmCertificatePublicationNetwork network, string signerAddress, CancellationToken cancellationToken)
    {
        var signer = EvmCertificatePublicationValues.Address(signerAddress, nameof(signerAddress));
        await RequireNetworkAsync(network, cancellationToken).ConfigureAwait(false);
        var result = await transport.RequestAsync("eth_getTransactionCount", [signer, "pending"], cancellationToken).ConfigureAwait(false);
        return EvmCertificateProtocol.Quantity(result);
    }

    public async Task<string> SendRawTransactionAsync(EvmCertificatePreparedTransaction intent, string rawTransaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var envelope = EvmCertificateSignedEnvelope.Validate(rawTransaction, intent);
        await RequireNetworkAsync(intent.Network, cancellationToken).ConfigureAwait(false);
        var result = await transport.RequestAsync("eth_sendRawTransaction", [envelope.RawTransaction], cancellationToken).ConfigureAwait(false);
        var returnedHash = EvmCertificateProtocol.Data(result, 32);
        if (!string.Equals(returnedHash, envelope.TransactionHash, StringComparison.Ordinal))
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.InvalidResponse);
        }
        return envelope.TransactionHash;
    }

    public void Dispose() => transport.Dispose();

    private async Task RequireNetworkAsync(EvmCertificatePublicationNetwork expected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        cancellationToken.ThrowIfCancellationRequested();
        if (expected.ChainId != configured.ChainId || expected.ContractAddress != configured.ContractAddress ||
            expected.IssuerId != configured.IssuerId || expected.RuntimeCodeSha256 != configured.RuntimeCodeSha256 ||
            expected.FinalityMode != configured.FinalityMode || expected.MinimumConfirmations != configured.MinimumConfirmations)
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.NetworkMismatch);
        }
        var actual = await verifier.ValidateNetworkAsync(cancellationToken).ConfigureAwait(false);
        if (actual is null || actual.ChainId != expected.ChainId || actual.ContractAddress != expected.ContractAddress ||
            actual.IssuerId != expected.IssuerId || actual.RuntimeCodeSha256 != expected.RuntimeCodeSha256 ||
            actual.FinalityMode != expected.FinalityMode || actual.MinimumConfirmations != expected.MinimumConfirmations)
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.NetworkMismatch);
        }
    }
}
