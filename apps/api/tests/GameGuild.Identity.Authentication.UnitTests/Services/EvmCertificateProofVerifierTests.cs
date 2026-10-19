using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using Nethereum.Util;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificateProofVerifierTests
{
    [Fact]
    public async Task AnchorRequiresMatchingReceiptEventStateAndPinnedCanonicalBlock()
    {
        var fixture = new ChainFixture();
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        var result = await verifier.VerifyAnchorAsync(ChainFixture.Digest[2..], ChainFixture.TransactionHash);
        Assert.Equal(EvmCertificateProofStatus.Verified, result.Status);
        var evidence = Assert.IsType<EvmCertificateEvidence>(result.Evidence);
        Assert.Equal(ChainFixture.Digest, evidence.CertificateHash);
        Assert.Equal(ChainFixture.TransactionHash, evidence.TransactionHash);
        Assert.Equal(100UL, evidence.TransactionBlock.Number);
        Assert.Equal(ChainFixture.AnchorBlockHash, evidence.TransactionBlock.Hash);
        Assert.Equal(200UL, evidence.Network.VerificationBlock.Number);
        Assert.Equal(EvmCertificateFinalityMode.Finalized, evidence.Network.FinalityMode);
        foreach (var call in fixture.Calls.Where(call => call.Method is EvmCertificateRpcMethod.Call or EvmCertificateRpcMethod.GetCode))
        {
            Assert.Equal(ChainFixture.HeadHash, call.Parameters[1].GetProperty("blockHash").GetString());
            Assert.True(call.Parameters[1].GetProperty("requireCanonical").GetBoolean());
        }
    }

    [Fact]
    public async Task ARemovedOriginalSignerDoesNotInvalidateHistoricalAnchors()
    {
        var fixture = new ChainFixture();
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        Assert.Equal(EvmCertificateProofStatus.Verified,
            (await verifier.VerifyAnchorAsync(ChainFixture.Digest, ChainFixture.TransactionHash)).Status);
        // The fixture reports false for current signer authorization. Historical
        // authorization is enforced by the pinned contract and original event.
    }

    [Theory]
    [InlineData(ProofMutation.PendingReceipt, EvmCertificateProofStatus.Pending)]
    [InlineData(ProofMutation.Reverted, EvmCertificateProofStatus.Reverted)]
    [InlineData(ProofMutation.RevertedUnfinalized, EvmCertificateProofStatus.NotFinalized)]
    [InlineData(ProofMutation.FutureBlock, EvmCertificateProofStatus.NotFinalized)]
    [InlineData(ProofMutation.CanonicalMismatch, EvmCertificateProofStatus.Reorged)]
    [InlineData(ProofMutation.Revoked, EvmCertificateProofStatus.Revoked)]
    [InlineData(ProofMutation.WrongContract, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongTransactionHash, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongSigner, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongInput, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongChainInTransaction, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.MissingLog, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.DuplicateLog, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongLogDigest, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongLogIssuer, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.RemovedLog, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongStateSigner, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.WrongStateBlock, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.MissingState, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.MalformedQuantity, EvmCertificateProofStatus.InvalidEvidence)]
    [InlineData(ProofMutation.InvalidAddressPadding, EvmCertificateProofStatus.InvalidEvidence)]
    public async Task IncompleteOrInconsistentEvidenceCannotVerify(ProofMutation mutation, EvmCertificateProofStatus expected)
    {
        var fixture = new ChainFixture { Mutation = mutation };
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        var result = await verifier.VerifyAnchorAsync(ChainFixture.Digest, ChainFixture.TransactionHash);
        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(ProofMutation.WrongNetwork, EvmCertificateEvidenceFailure.NetworkMismatch)]
    [InlineData(ProofMutation.WrongCode, EvmCertificateEvidenceFailure.RuntimeCodeMismatch)]
    [InlineData(ProofMutation.WrongIssuer, EvmCertificateEvidenceFailure.IssuerMismatch)]
    [InlineData(ProofMutation.WrongProtocol, EvmCertificateEvidenceFailure.ProtocolVersionMismatch)]
    public async Task ConfiguredNetworkAndRegistryIdentityAreValidated(ProofMutation mutation, EvmCertificateEvidenceFailure expected)
    {
        var fixture = new ChainFixture { Mutation = mutation };
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        var error = await Assert.ThrowsAsync<EvmCertificateEvidenceException>(() => verifier.ValidateNetworkAsync());
        Assert.Equal(expected, error.Failure);
    }

    [Fact]
    public async Task UnsupportedFinalizedTagDoesNotFallBackToLatest()
    {
        var fixture = new ChainFixture { RejectFinalizedTag = true };
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        await Assert.ThrowsAsync<EvmCertificateRpcException>(() => verifier.ValidateNetworkAsync());
        Assert.DoesNotContain(fixture.Calls, call => call.Method == EvmCertificateRpcMethod.GetBlockByNumber && call.Parameters[0].GetString() == "latest");
    }

    [Fact]
    public async Task ConfirmationPolicyPinsStateAtTheConfirmedHeight()
    {
        var fixture = new ChainFixture();
        fixture.Options.ChainId = 12345;
        fixture.Options.FinalityMode = EvmCertificateFinalityMode.Confirmations;
        fixture.Options.MinimumConfirmations = 12;
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        var result = await verifier.VerifyAnchorAsync(ChainFixture.Digest, ChainFixture.TransactionHash);
        Assert.Equal(EvmCertificateProofStatus.Verified, result.Status);
        Assert.Equal(EvmCertificateFinalityMode.Confirmations, result.Evidence!.Network.FinalityMode);
        Assert.Equal(200UL, result.Evidence.Network.VerificationBlock.Number);
        Assert.Contains(fixture.Calls, call => call.Method == EvmCertificateRpcMethod.GetBlockByNumber && call.Parameters[0].GetString() == "0xc8");
    }

    [Fact]
    public async Task FinalizedRevocationIsVerifiedAgainstItsOwnReceiptAndState()
    {
        var fixture = new ChainFixture { Operation = EvmCertificateOperationKind.Revoke };
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        var result = await verifier.VerifyRevocationAsync(ChainFixture.Digest, ChainFixture.TransactionHash);
        Assert.Equal(EvmCertificateProofStatus.Verified, result.Status);
        var evidence = Assert.IsType<EvmCertificateEvidence>(result.Evidence);
        Assert.Equal(EvmCertificateOperationKind.Revoke, evidence.Operation);
        Assert.Equal(160UL, evidence.TransactionBlock.Number);
        Assert.Equal(ChainFixture.Revoker, evidence.State.RevokeSigner);
        Assert.Equal(ChainFixture.Signer, evidence.State.AnchorSigner);
    }

    [Fact]
    public async Task RevocationProofCannotUseAnUnrevokedState()
    {
        var fixture = new ChainFixture { Operation = EvmCertificateOperationKind.Revoke, Mutation = ProofMutation.UnrevokedState };
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        Assert.Equal(EvmCertificateProofStatus.InvalidEvidence,
            (await verifier.VerifyRevocationAsync(ChainFixture.Digest, ChainFixture.TransactionHash)).Status);
    }

    [Theory]
    [InlineData("0x123")]
    [InlineData("not-a-digest")]
    [InlineData("0x0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task InvalidDigestDoesNotReachTheRpc(string digest)
    {
        var fixture = new ChainFixture();
        var verifier = new EvmCertificateProofVerifier(fixture, fixture.Options);
        await Assert.ThrowsAsync<ArgumentException>(() => verifier.VerifyAnchorAsync(digest, ChainFixture.TransactionHash));
        Assert.Empty(fixture.Calls);
    }

    public enum ProofMutation
    {
        None, PendingReceipt, Reverted, RevertedUnfinalized, FutureBlock, CanonicalMismatch, Revoked,
        WrongContract, WrongTransactionHash, WrongSigner, WrongInput, WrongChainInTransaction,
        MissingLog, DuplicateLog, WrongLogDigest, WrongLogIssuer, RemovedLog,
        WrongStateSigner, WrongStateBlock, MissingState, MalformedQuantity, InvalidAddressPadding,
        WrongNetwork, WrongCode, WrongIssuer, WrongProtocol, UnrevokedState
    }

    private sealed class ChainFixture : IEvmCertificateRpc
    {
        public static string Digest => "0x" + new string('3', 64);
        public static string TransactionHash => "0x" + new string('d', 64);
        public static string AnchorBlockHash => "0x" + new string('a', 64);
        public static string RevokeBlockHash => "0x" + new string('b', 64);
        public static string HeadHash => "0x" + new string('c', 64);
        public static string Signer => "0x" + new string('4', 40);
        public static string Revoker => "0x" + new string('5', 40);
        private static string OtherAddress => "0x" + new string('6', 40);
        public EvmCertificateNetworkOptions Options { get; } = CreateOptions();
        public ProofMutation Mutation { get; init; }
        public EvmCertificateOperationKind Operation { get; init; }
        public bool RejectFinalizedTag { get; init; }
        public List<RpcCall> Calls { get; } = [];

        private ulong OperationBlock => Operation == EvmCertificateOperationKind.Revoke ? 160UL : 100UL;
        private string OperationBlockHash => Operation == EvmCertificateOperationKind.Revoke ? RevokeBlockHash : AnchorBlockHash;
        private string OperationSigner => Operation == EvmCertificateOperationKind.Revoke ? Revoker : Signer;

        public Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters) =>
            RequestAsync(method, parameters, CancellationToken.None);

        public Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wire = JsonSerializer.SerializeToElement(parameters);
            Calls.Add(new RpcCall(method, wire));
            object? result = method switch
            {
                EvmCertificateRpcMethod.ChainId => Mutation == ProofMutation.WrongNetwork ? "0x1" : Quantity((ulong)Options.ChainId),
                EvmCertificateRpcMethod.GetCode => Mutation == ProofMutation.WrongCode ? "0x6001" : "0x60006000f3",
                EvmCertificateRpcMethod.Call => ContractCall(wire[0].GetProperty("data").GetString()!),
                EvmCertificateRpcMethod.GetBlockByNumber => Block(wire[0].GetString()!),
                EvmCertificateRpcMethod.GetTransactionByHash => Transaction(),
                EvmCertificateRpcMethod.GetTransactionReceipt => Mutation == ProofMutation.PendingReceipt ? null : Receipt(),
                _ => throw new InvalidOperationException("Unexpected fixture RPC operation")
            };
            return Task.FromResult(JsonSerializer.SerializeToElement(result));
        }

        private object Block(string tag)
        {
            if (tag == "finalized" && RejectFinalizedTag)
            {
                throw new EvmCertificateRpcException(EvmCertificateRpcFailure.RpcFailure, rpcErrorCode: -32602);
            }

            if (tag is "finalized" or "0xc8")
            {
                return new { number = "0xc8", hash = HeadHash };
            }
            if (tag == "latest")
            {
                return new { number = "0xd3", hash = "0x" + new string('e', 64) };
            }
            return new { number = Quantity(OperationBlock), hash = Mutation == ProofMutation.CanonicalMismatch ? HeadHash : OperationBlockHash };
        }

        private object Transaction() => new
        {
            hash = TransactionHash,
            from = Mutation == ProofMutation.WrongSigner ? OtherAddress : OperationSigner,
            to = Options.ContractAddress,
            input = Mutation == ProofMutation.WrongInput ? "0x" : (Operation == EvmCertificateOperationKind.Revoke ? "0xb75c7dc6" : "0xeecdf927") + Digest[2..],
            value = "0x0",
            chainId = Mutation == ProofMutation.WrongChainInTransaction ? "0x1" : Quantity((ulong)Options.ChainId),
            blockNumber = Quantity(OperationBlock),
            blockHash = OperationBlockHash
        };

        private object Receipt()
        {
            var logs = Mutation is ProofMutation.MissingLog or ProofMutation.Reverted or ProofMutation.RevertedUnfinalized
                ? Array.Empty<object>() : new[] { EventLog() };
            if (Mutation == ProofMutation.DuplicateLog)
            {
                logs = [EventLog(), EventLog()];
            }
            return new
            {
                transactionHash = Mutation == ProofMutation.WrongTransactionHash ? HeadHash : TransactionHash,
                from = OperationSigner,
                to = Mutation == ProofMutation.WrongContract ? OtherAddress : Options.ContractAddress,
                blockNumber = Mutation == ProofMutation.MalformedQuantity ? "0x064" : Quantity(Mutation is ProofMutation.FutureBlock or ProofMutation.RevertedUnfinalized ? 201UL : OperationBlock),
                blockHash = OperationBlockHash,
                status = Mutation is ProofMutation.Reverted or ProofMutation.RevertedUnfinalized ? "0x0" : "0x1",
                logs
            };
        }

        private object EventLog() => new
        {
            address = Options.ContractAddress,
            removed = Mutation == ProofMutation.RemovedLog,
            blockNumber = Quantity(OperationBlock),
            blockHash = OperationBlockHash,
            transactionHash = TransactionHash,
            topics = new[]
            {
                "0x" + Sha3Keccack.Current.CalculateHash(Operation == EvmCertificateOperationKind.Revoke
                    ? "CertificateRevoked(bytes32,bytes32,address,uint256)" : "CertificateAnchored(bytes32,bytes32,address,uint256)"),
                Mutation == ProofMutation.WrongLogDigest ? HeadHash : Digest,
                Mutation == ProofMutation.WrongLogIssuer ? HeadHash : Options.IssuerId,
                "0x" + AddressWord(OperationSigner)
            },
            data = "0x" + Word(OperationBlock)
        };

        private string ContractCall(string data)
        {
            if (data == "0xaa3aa460")
            {
                return "0x" + Word(Mutation == ProofMutation.WrongProtocol ? 2UL : 1UL);
            }
            if (data == "0x4f572d75")
            {
                return Mutation == ProofMutation.WrongIssuer ? HeadHash : Options.IssuerId;
            }
            if (data.StartsWith("0x5d2b6a4e", StringComparison.Ordinal))
            {
                return "0x" + Word(0);
            }
            if (data != "0x7feb51d9" + Digest[2..])
            {
                throw new InvalidOperationException("Unexpected fixture contract call");
            }
            if (Mutation == ProofMutation.MissingState)
            {
                return "0x" + new string('0', 256);
            }
            var isRevoked = (Operation == EvmCertificateOperationKind.Revoke || Mutation == ProofMutation.Revoked) && Mutation != ProofMutation.UnrevokedState;
            var anchorSigner = AddressWord(Mutation == ProofMutation.WrongStateSigner ? OtherAddress : Signer);
            if (Mutation == ProofMutation.InvalidAddressPadding)
            {
                anchorSigner = "1" + anchorSigner[1..];
            }
            return "0x" + anchorSigner + (isRevoked ? AddressWord(Revoker) : new string('0', 64)) +
                   Word(Mutation == ProofMutation.WrongStateBlock ? 99UL : 100UL) + Word(isRevoked ? 160UL : 0UL);
        }

        private static EvmCertificateNetworkOptions CreateOptions()
        {
            var options = EvmCertificateNetworkOptionsTests.ValidOptions();
            options.ExpectedRuntimeCodeSha256 = "10a958938acc6e15066f1e3d64f4871b196412a67d59ab1664f5d38f406206b6";
            return options;
        }

        private static string Word(ulong value) => value.ToString("x").PadLeft(64, '0');
        private static string AddressWord(string address) => address[2..].PadLeft(64, '0');
        private static string Quantity(ulong value) => "0x" + value.ToString("x");
    }

    private sealed record RpcCall(EvmCertificateRpcMethod Method, JsonElement Parameters);
}
