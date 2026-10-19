using System.Numerics;
using GameGuild.Configuration.ApplicationLayer;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificatePublicationTests
{
    [Fact]
    public void NetworkSnapshotExcludesTheEndpointAndSurvivesConfigurationMutation()
    {
        var options = Options();
        var network = EvmCertificatePublicationNetwork.Capture("issuer-a", options);
        options.ChainId = 1;
        options.ContractAddress = "0x" + new string('a', 40);
        Assert.Equal(12345, network.ChainId);
        Assert.Equal("0x" + new string('1', 40), network.ContractAddress);
        Assert.DoesNotContain("Rpc", string.Join(',', typeof(EvmCertificatePublicationNetwork).GetProperties().Select(p => p.Name)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(20999, 1, 20999)]
    [InlineData(30000, 0, 30000)]
    [InlineData(30000, -1, 30000)]
    [InlineData(30000, 2, 59999)]
    public void FeeBudgetRejectsInvalidOrUnfundedUpperBounds(ulong gas, long feeCap, long totalCap)
    {
        Assert.Throws<ArgumentException>(() => EvmCertificateFeeBudget.Create(gas, feeCap, totalCap));
    }

    [Fact]
    public void FeeBudgetRetainsExactIntegerWeiBeyondFloatingPointPrecision()
    {
        var fee = BigInteger.Parse("900719925474099312345");
        var budget = EvmCertificateFeeBudget.Create(100000, fee, fee * 100000);
        Assert.Equal(fee, budget.MaximumFeePerGasWei);
        Assert.Equal(fee * 100000, budget.MaximumTransactionFeeWei);
    }

    [Theory]
    [InlineData("0x")]
    [InlineData("0x02")]
    [InlineData("0x02c0")]
    [InlineData("0x02c1c0")]
    [InlineData("0x0200")]
    [InlineData("0xgg")]
    public void SignedEnvelopeRejectsMalformedOrNestedRlpWithoutEchoingInput(string raw)
    {
        var prepared = Preparation(EthECKey.GenerateKey().GetPublicAddress());
        var error = Assert.Throws<EvmCertificateJournalException>(() => EvmCertificateSignedEnvelope.Validate(raw, prepared));
        Assert.Equal(EvmCertificateJournalFailure.InvalidTransaction, error.Failure);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(raw, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SignedEnvelopeBindsSignerChainNonceTargetDigestValueAndFees()
    {
        var key = EthECKey.GenerateKey();
        var prepared = Preparation(key.GetPublicAddress());
        var raw = Sign(key, prepared, TransactionMutation.None);
        var validated = EvmCertificateSignedEnvelope.Validate(raw, prepared);
        Assert.StartsWith("0x", validated.TransactionHash, StringComparison.Ordinal);
        Assert.Equal(66, validated.TransactionHash.Length);
        Assert.Equal(raw.ToLowerInvariant(), validated.RawTransaction);
        Assert.Equal(new BigInteger(2), validated.MaximumFeePerGasWei);
    }

    [Fact]
    public void SignedEnvelopeRejectsOversizedInputBeforeDecoding()
    {
        var prepared = Preparation(EthECKey.GenerateKey().GetPublicAddress());
        var error = Assert.Throws<EvmCertificateJournalException>(() => EvmCertificateSignedEnvelope.Validate("0x02" + new string('f', 10000), prepared));
        Assert.Equal(EvmCertificateJournalFailure.InvalidTransaction, error.Failure);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData(TransactionMutation.Chain)]
    [InlineData(TransactionMutation.Nonce)]
    [InlineData(TransactionMutation.Target)]
    [InlineData(TransactionMutation.Digest)]
    [InlineData(TransactionMutation.Value)]
    [InlineData(TransactionMutation.Gas)]
    [InlineData(TransactionMutation.Fee)]
    [InlineData(TransactionMutation.Priority)]
    [InlineData(TransactionMutation.Signer)]
    [InlineData(TransactionMutation.TrailingBytes)]
    [InlineData(TransactionMutation.HighS)]
    [InlineData(TransactionMutation.YParity)]
    public void SignedEnvelopeRejectsAValidSignatureForAnotherIntent(TransactionMutation mutation)
    {
        var key = EthECKey.GenerateKey();
        var prepared = Preparation(key.GetPublicAddress());
        var raw = Sign(mutation == TransactionMutation.Signer ? EthECKey.GenerateKey() : key, prepared, mutation);
        var error = Assert.Throws<EvmCertificateJournalException>(() => EvmCertificateSignedEnvelope.Validate(raw, prepared));
        Assert.Equal(EvmCertificateJournalFailure.InvalidTransaction, error.Failure);
    }

    internal static EvmCertificateNetworkOptions Options() => new()
    {
        ChainId = 12345,
        ContractAddress = "0x" + new string('1', 40),
        IssuerId = "0x" + new string('2', 64),
        ExpectedRuntimeCodeSha256 = new string('3', 64),
        RpcEndpoint = "https://rpc.invalid/",
        FinalityMode = EvmCertificateFinalityMode.Confirmations,
        MinimumConfirmations = 1
    };

    private static EvmCertificatePreparedTransaction Preparation(string signer) => new(
        EvmCertificatePublicationNetwork.Capture("issuer-a", Options()),
        EvmCertificateOperationKind.Anchor, "0x" + new string('4', 64), signer.ToLowerInvariant(),
        7, EvmCertificateFeeBudget.Create(100000, 10, 1000000));

    private static string Sign(EthECKey key, EvmCertificatePreparedTransaction prepared, TransactionMutation mutation)
    {
        var digest = mutation == TransactionMutation.Digest ? "0x" + new string('5', 64) : prepared.CertificateHash;
        var tx = new Transaction1559(
            prepared.Network.ChainId + (mutation == TransactionMutation.Chain ? 1 : 0),
            prepared.Nonce + (mutation == TransactionMutation.Nonce ? 1UL : 0UL),
            mutation == TransactionMutation.Priority ? 3 : 1,
            mutation == TransactionMutation.Fee ? 11 : 2,
            prepared.Budget.GasLimit + (mutation == TransactionMutation.Gas ? 1UL : 0UL),
            mutation == TransactionMutation.Target ? "0x" + new string('6', 40) : prepared.Network.ContractAddress,
            mutation == TransactionMutation.Value ? 1 : 0,
            "0xeecdf927" + digest[2..], []);
        var raw = "0x" + new Transaction1559Signer().SignTransaction(key, tx);
        if (mutation is TransactionMutation.HighS or TransactionMutation.YParity)
        {
            var s = new BigInteger(tx.Signature.S, isUnsigned: true, isBigEndian: true);
            var order = BigInteger.Parse("115792089237316195423570985008687907852837564279074904382605163141518161494337");
            tx.SetSignature(new Signature(tx.Signature.R,
                mutation == TransactionMutation.HighS ? (order - s).ToByteArray(isUnsigned: true, isBigEndian: true) : tx.Signature.S,
                [(byte)(mutation == TransactionMutation.HighS ? tx.Signature.V.LastOrDefault() ^ 1 : 27)]));
            raw = "0x" + Convert.ToHexString(tx.GetRLPEncoded());
        }
        return mutation == TransactionMutation.TrailingBytes ? raw + "00" : raw;
    }

    public enum TransactionMutation
    {
        None, Chain, Nonce, Target, Digest, Value, Gas, Fee, Priority, Signer, TrailingBytes, HighS, YParity
    }
}
