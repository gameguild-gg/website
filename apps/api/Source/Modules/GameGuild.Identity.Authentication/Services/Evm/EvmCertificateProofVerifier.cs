using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>Checks direct registry transactions against canonical, policy-qualified chain state.</summary>
/// <remarks>No database row, submission hash, current signer grant or unconfirmed receipt establishes a valid proof.</remarks>
public sealed class EvmCertificateProofVerifier : IEvmCertificateProofVerifier
{
    private readonly IEvmCertificateRpc rpc;
    private readonly long chainId;
    private readonly string contractAddress;
    private readonly string issuerId;
    private readonly string expectedCodeHash;
    private readonly EvmCertificateFinalityMode finality;
    private readonly int minimumConfirmations;

    public EvmCertificateProofVerifier(IEvmCertificateRpc rpc, EvmCertificateNetworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(rpc);
        ArgumentNullException.ThrowIfNull(options);
        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        }
        this.rpc = rpc;
        chainId = options.ChainId;
        contractAddress = options.ContractAddress.ToLowerInvariant();
        issuerId = options.IssuerId.ToLowerInvariant();
        expectedCodeHash = options.ExpectedRuntimeCodeSha256.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? options.ExpectedRuntimeCodeSha256[2..].ToLowerInvariant()
            : options.ExpectedRuntimeCodeSha256.ToLowerInvariant();
        finality = options.FinalityMode;
        minimumConfirmations = options.MinimumConfirmations;
    }

    public Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync() => ValidateNetworkAsync(CancellationToken.None);

    public async Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync(CancellationToken cancellationToken)
    {
        var actualChain = EvmCertificateProtocol.Quantity(await rpc.RequestAsync(EvmCertificateRpcMethod.ChainId, [], cancellationToken).ConfigureAwait(false));
        if (actualChain != (ulong)chainId)
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.NetworkMismatch);
        }

        var block = await VerificationBlockAsync(cancellationToken).ConfigureAwait(false);
        var pinned = new { blockHash = block.Hash, requireCanonical = true };
        var code = EvmCertificateProtocol.Data(await rpc.RequestAsync(EvmCertificateRpcMethod.GetCode, [contractAddress, pinned], cancellationToken).ConfigureAwait(false));
        if (code.Length <= 2)
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.RuntimeCodeMismatch);
        }
        var codeHash = Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code[2..]))).ToLowerInvariant();
        if (!string.Equals(codeHash, expectedCodeHash, StringComparison.Ordinal))
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.RuntimeCodeMismatch);
        }

        var protocol = EvmCertificateProtocol.Data(await ContractCallAsync(EvmCertificateProtocol.ProtocolVersionCall, pinned, cancellationToken).ConfigureAwait(false), 32);
        if (EvmCertificateProtocol.UInt64Word(protocol[2..]) != 1)
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.ProtocolVersionMismatch);
        }

        var actualIssuer = EvmCertificateProtocol.Data(await ContractCallAsync(EvmCertificateProtocol.IssuerIdCall, pinned, cancellationToken).ConfigureAwait(false), 32);
        if (!string.Equals(actualIssuer, issuerId, StringComparison.Ordinal))
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.IssuerMismatch);
        }

        return new(chainId, contractAddress, issuerId, codeHash, finality, minimumConfirmations, block);
    }

    public Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash) =>
        VerifyAnchorAsync(certificateHash, transactionHash, CancellationToken.None);

    public Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken) =>
        VerifyAsync(EvmCertificateOperationKind.Anchor, certificateHash, transactionHash, cancellationToken);

    public Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash) =>
        VerifyRevocationAsync(certificateHash, transactionHash, CancellationToken.None);

    public Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken) =>
        VerifyAsync(EvmCertificateOperationKind.Revoke, certificateHash, transactionHash, cancellationToken);

    private async Task<EvmCertificateProofResult> VerifyAsync(EvmCertificateOperationKind operation, string certificateHash, string transactionHash, CancellationToken cancellationToken)
    {
        var digest = EvmCertificateProtocol.NormalizeHashArgument(certificateHash, nameof(certificateHash));
        var hash = EvmCertificateProtocol.NormalizeHashArgument(transactionHash, nameof(transactionHash));
        var network = await ValidateNetworkAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var receipt = await rpc.RequestAsync(EvmCertificateRpcMethod.GetTransactionReceipt, [hash], cancellationToken).ConfigureAwait(false);
            if (receipt.ValueKind == JsonValueKind.Null)
            {
                return new(EvmCertificateProofStatus.Pending);
            }
            EvmCertificateProtocol.RequireObject(receipt);
            var status = EvmCertificateProtocol.PropertyQuantity(receipt, "status");
            if (status > 1 || EvmCertificateProtocol.PropertyData(receipt, "transactionHash", 32) != hash ||
                EvmCertificateProtocol.PropertyData(receipt, "to", 20) != contractAddress)
            {
                return new(EvmCertificateProofStatus.InvalidEvidence);
            }

            var receiptBlock = new EvmCertificateBlockReference(
                EvmCertificateProtocol.PropertyQuantity(receipt, "blockNumber"),
                EvmCertificateProtocol.PropertyData(receipt, "blockHash", 32));
            if (receiptBlock.Number > network.VerificationBlock.Number)
            {
                return new(EvmCertificateProofStatus.NotFinalized);
            }

            var canonical = await rpc.RequestAsync(EvmCertificateRpcMethod.GetBlockByNumber,
                [EvmCertificateProtocol.EncodeQuantity(receiptBlock.Number), false], cancellationToken).ConfigureAwait(false);
            if (canonical.ValueKind == JsonValueKind.Null)
            {
                return new(EvmCertificateProofStatus.Reorged);
            }
            var canonicalBlock = EvmCertificateProtocol.Block(canonical);
            if (canonicalBlock.Number != receiptBlock.Number)
            {
                return new(EvmCertificateProofStatus.InvalidEvidence);
            }
            if (canonicalBlock.Hash != receiptBlock.Hash)
            {
                return new(EvmCertificateProofStatus.Reorged);
            }

            var transaction = await rpc.RequestAsync(EvmCertificateRpcMethod.GetTransactionByHash, [hash], cancellationToken).ConfigureAwait(false);
            if (transaction.ValueKind == JsonValueKind.Null)
            {
                return new(EvmCertificateProofStatus.Pending);
            }
            EvmCertificateProtocol.RequireObject(transaction);
            var signer = EvmCertificateProtocol.PropertyData(transaction, "from", 20);
            if (!MatchesTransaction(transaction, receipt, receiptBlock, operation, digest, hash, signer))
            {
                return new(EvmCertificateProofStatus.InvalidEvidence);
            }

            // A receipt can change/disappear during a reorganization. Even a
            // revert is terminal only after canonical inclusion meets policy.
            if (status == 0)
            {
                return new(EvmCertificateProofStatus.Reverted);
            }
            if (!MatchesEvent(receipt, receiptBlock, operation, digest, hash, signer))
            {
                return new(EvmCertificateProofStatus.InvalidEvidence);
            }

            // Current signer authorization must not erase evidence issued before key rotation.
            var pinned = new { blockHash = network.VerificationBlock.Hash, requireCanonical = true };
            var state = EvmCertificateProtocol.State(await ContractCallAsync(EvmCertificateProtocol.GetAnchorCall + digest[2..], pinned, cancellationToken).ConfigureAwait(false));
            if (state.AnchorSigner == EvmCertificateProtocol.ZeroAddress || state.AnchoredAtBlock > receiptBlock.Number ||
                (state.RevokeSigner == EvmCertificateProtocol.ZeroAddress && state.RevokedAtBlock != 0) ||
                (state.RevokeSigner != EvmCertificateProtocol.ZeroAddress &&
                 (state.RevokedAtBlock < state.AnchoredAtBlock || state.RevokedAtBlock > network.VerificationBlock.Number)))
            {
                return new(EvmCertificateProofStatus.InvalidEvidence);
            }

            var evidence = new EvmCertificateEvidence(operation, digest, hash, signer, receiptBlock, network, state);
            if (operation == EvmCertificateOperationKind.Anchor)
            {
                if (state.AnchorSigner != signer || state.AnchoredAtBlock != receiptBlock.Number)
                {
                    return new(EvmCertificateProofStatus.InvalidEvidence);
                }

                return new(state.RevokeSigner == EvmCertificateProtocol.ZeroAddress
                    ? EvmCertificateProofStatus.Verified : EvmCertificateProofStatus.Revoked, evidence);
            }

            return state.RevokeSigner == signer && state.RevokedAtBlock == receiptBlock.Number
                ? new(EvmCertificateProofStatus.Verified, evidence)
                : new(EvmCertificateProofStatus.InvalidEvidence);
        }
        catch (EvmCertificateEvidenceException error) when (error.Failure == EvmCertificateEvidenceFailure.MalformedRpcData)
        {
            return new(EvmCertificateProofStatus.InvalidEvidence);
        }
    }

    private bool MatchesTransaction(JsonElement transaction, JsonElement receipt, EvmCertificateBlockReference block,
        EvmCertificateOperationKind operation, string digest, string transactionHash, string signer)
    {
        var expectedInput = (operation == EvmCertificateOperationKind.Anchor ? EvmCertificateProtocol.AnchorCall : EvmCertificateProtocol.RevokeCall) + digest[2..];
        return EvmCertificateProtocol.PropertyData(transaction, "hash", 32) == transactionHash &&
               EvmCertificateProtocol.PropertyData(transaction, "to", 20) == contractAddress &&
               EvmCertificateProtocol.PropertyData(receipt, "from", 20) == signer &&
               EvmCertificateProtocol.PropertyData(transaction, "input", 36) == expectedInput &&
               EvmCertificateProtocol.PropertyQuantity(transaction, "value") == 0 &&
               EvmCertificateProtocol.PropertyData(transaction, "blockHash", 32) == block.Hash &&
               EvmCertificateProtocol.PropertyQuantity(transaction, "blockNumber") == block.Number &&
               (!transaction.TryGetProperty("chainId", out var transactionChain) || EvmCertificateProtocol.Quantity(transactionChain) == (ulong)chainId);
    }

    private bool MatchesEvent(JsonElement receipt, EvmCertificateBlockReference block, EvmCertificateOperationKind operation,
        string digest, string transactionHash, string signer)
    {
        if (!receipt.TryGetProperty("logs", out var logs) || logs.ValueKind != JsonValueKind.Array)
        {
            throw EvmCertificateProtocol.Malformed();
        }
        var expectedTopic = operation == EvmCertificateOperationKind.Anchor ? EvmCertificateProtocol.AnchorEventTopic : EvmCertificateProtocol.RevokeEventTopic;
        var matches = 0;
        foreach (var log in logs.EnumerateArray())
        {
            EvmCertificateProtocol.RequireObject(log);
            if (EvmCertificateProtocol.PropertyData(log, "address", 20) != contractAddress)
            {
                continue;
            }
            if (!log.TryGetProperty("topics", out var topics) || topics.ValueKind != JsonValueKind.Array || topics.GetArrayLength() != 4)
            {
                continue;
            }
            if (EvmCertificateProtocol.Data(topics[0], 32) != expectedTopic || EvmCertificateProtocol.Data(topics[1], 32) != digest)
            {
                continue;
            }
            if (EvmCertificateProtocol.Data(topics[2], 32) != issuerId ||
                EvmCertificateProtocol.AddressWord(EvmCertificateProtocol.Data(topics[3], 32)[2..]) != signer ||
                !log.TryGetProperty("removed", out var removed) || removed.ValueKind != JsonValueKind.False ||
                EvmCertificateProtocol.PropertyData(log, "transactionHash", 32) != transactionHash ||
                EvmCertificateProtocol.PropertyData(log, "blockHash", 32) != block.Hash ||
                EvmCertificateProtocol.PropertyQuantity(log, "blockNumber") != block.Number ||
                !log.TryGetProperty("data", out var data) || EvmCertificateProtocol.UInt64Word(EvmCertificateProtocol.Data(data, 32)[2..]) != block.Number)
            {
                return false;
            }

            matches++;
        }

        return matches == 1;
    }

    private async Task<EvmCertificateBlockReference> VerificationBlockAsync(CancellationToken cancellationToken)
    {
        if (finality == EvmCertificateFinalityMode.Finalized)
        {
            return EvmCertificateProtocol.Block(await rpc.RequestAsync(EvmCertificateRpcMethod.GetBlockByNumber, ["finalized", false], cancellationToken).ConfigureAwait(false));
        }

        var latest = EvmCertificateProtocol.Block(await rpc.RequestAsync(EvmCertificateRpcMethod.GetBlockByNumber, ["latest", false], cancellationToken).ConfigureAwait(false));
        var distance = (ulong)(minimumConfirmations - 1);
        if (latest.Number < distance)
        {
            throw new EvmCertificateEvidenceException(EvmCertificateEvidenceFailure.InsufficientChainHistory);
        }
        var number = latest.Number - distance;
        if (number == latest.Number)
        {
            return latest;
        }
        var qualified = EvmCertificateProtocol.Block(await rpc.RequestAsync(EvmCertificateRpcMethod.GetBlockByNumber, [EvmCertificateProtocol.EncodeQuantity(number), false], cancellationToken).ConfigureAwait(false));
        if (qualified.Number != number)
        {
            throw EvmCertificateProtocol.Malformed();
        }
        return qualified;
    }

    private Task<JsonElement> ContractCallAsync(string data, object pinnedBlock, CancellationToken cancellationToken) =>
        rpc.RequestAsync(EvmCertificateRpcMethod.Call, [new { to = contractAddress, data }, pinnedBlock], cancellationToken);
}
