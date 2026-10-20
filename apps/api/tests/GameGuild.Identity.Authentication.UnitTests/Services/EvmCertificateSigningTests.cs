using System.Security.Cryptography;
using GameGuild.Configuration.ApplicationLayer;
using Nethereum.Signer;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificateSigningTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task AddressResolutionClearsTheTransferredPrivateKeyBuffer()
    {
        var key = EthECKey.GenerateKey();
        var keys = new StubKeys(_ => PrivateKeyCopy(key));
        var signer = new EvmCertificateSoftwareSigner(keys);
        Assert.Equal(key.GetPublicAddress().ToLowerInvariant(), await signer.GetAddressAsync("issuer-key-v1", Ct));
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
    }

    [Theory]
    [InlineData(EvmCertificateOperationKind.Anchor)]
    [InlineData(EvmCertificateOperationKind.Revoke)]
    public async Task SignsTheReservedTypeTwoIntentWithinItsOriginalFeeBudget(EvmCertificateOperationKind kind)
    {
        var key = EthECKey.GenerateKey();
        var keys = new StubKeys(_ => PrivateKeyCopy(key));
        var prepared = Preparation(key.GetPublicAddress()) with { Kind = kind };
        var raw = await new EvmCertificateSoftwareSigner(keys).SignAsync("issuer-key-v1", prepared, 2, 1, Ct);
        var envelope = EvmCertificateSignedEnvelope.Validate(raw, prepared);
        Assert.Equal(raw, envelope.RawTransaction);
        Assert.Equal(66, envelope.TransactionHash.Length);
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public async Task AChangedKeyCannotReplaceTheSignerBoundToTheReservedNonce()
    {
        var original = EthECKey.GenerateKey();
        var replacement = EthECKey.GenerateKey();
        var keys = new StubKeys(_ => PrivateKeyCopy(replacement));
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(() =>
            new EvmCertificateSoftwareSigner(keys).SignAsync("issuer-key-v1", Preparation(original.GetPublicAddress()), 2, 1, Ct));
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, error.Failure);
        Assert.Null(error.InnerException);
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(11, 1)]
    [InlineData(2, 3)]
    [InlineData(2, -1)]
    public async Task InvalidFeesAreRejectedBeforeResolvingAnyPrivateKey(int fee, int priority)
    {
        var key = EthECKey.GenerateKey();
        var keys = new StubKeys(_ => PrivateKeyCopy(key));
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(() =>
            new EvmCertificateSoftwareSigner(keys).SignAsync("issuer-key-v1", Preparation(key.GetPublicAddress()), fee, priority, Ct));
        Assert.Equal(EvmCertificateJournalFailure.InvalidTransaction, error.Failure);
        Assert.Equal(0, keys.Reads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public async Task InvalidPrivateKeysFailSafelyAndTheirBuffersAreCleared(int length)
    {
        var keys = new StubKeys(_ => new byte[length]);
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(() =>
            new EvmCertificateSoftwareSigner(keys).GetAddressAsync("issuer-key-v1", Ct));
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, error.Failure);
        Assert.Null(error.InnerException);
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public async Task PrivateScalarOutsideTheCurveOrderIsRejectedAndCleared()
    {
        var keys = new StubKeys(_ => Enumerable.Repeat(byte.MaxValue, 32).ToArray());
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(() =>
            new EvmCertificateSoftwareSigner(keys).GetAddressAsync("issuer-key-v1", Ct));
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, error.Failure);
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public async Task ProviderExceptionsCannotEchoCredentialValues()
    {
        var sentinel = "private-provider-value-" + Guid.NewGuid().ToString("N");
        var keys = new StubKeys(_ => throw new InvalidOperationException(sentinel));
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(() =>
            new EvmCertificateSoftwareSigner(keys).GetAddressAsync("issuer-key-v1", Ct));
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, error.Failure);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(sentinel, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationAfterResolutionClearsTheKeyAndPreservesTheCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        var key = EthECKey.GenerateKey();
        var keys = new StubKeys(_ =>
        {
            cancellation.Cancel();
            return PrivateKeyCopy(key);
        });
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new EvmCertificateSoftwareSigner(keys).GetAddressAsync("issuer-key-v1", cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public async Task AnUnlistedReferenceCannotReadAnArbitraryEnvironmentVariable()
    {
        var source = new EvmCertificateEnvironmentKeySource(new Dictionary<string, string>());
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(async () =>
        {
            await source.GetPrivateKeyAsync("unlisted-key", Ct);
        });
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, error.Failure);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task MissingConfiguredEnvironmentValueFailsWithoutEchoingItsName()
    {
        var variable = "GG_EVM_TEST_" + Guid.NewGuid().ToString("N");
        var source = new EvmCertificateEnvironmentKeySource(new Dictionary<string, string> { ["issuer-key-v1"] = variable });
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(async () =>
        {
            await source.GetPrivateKeyAsync("issuer-key-v1", Ct);
        });
        Assert.Equal(EvmCertificateJournalFailure.SignerUnavailable, error.Failure);
        Assert.DoesNotContain(variable, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnvironmentSourceCopiesTheReferenceMapAndReturnsOnlyCallerOwnedBytes()
    {
        var variable = "GG_EVM_TEST_" + Guid.NewGuid().ToString("N");
        var key = EthECKey.GenerateKey();
        var mapping = new Dictionary<string, string> { ["issuer-key-v1"] = variable };
        var source = new EvmCertificateEnvironmentKeySource(mapping);
        mapping.Clear();
        var bytes = PrivateKeyCopy(key);
        Environment.SetEnvironmentVariable(variable, Convert.ToHexString(bytes));
        CryptographicOperations.ZeroMemory(bytes);
        try
        {
            var signer = new EvmCertificateSoftwareSigner(source);
            Assert.Equal(key.GetPublicAddress().ToLowerInvariant(), await signer.GetAddressAsync("issuer-key-v1", Ct));
            Assert.Equal(key.GetPublicAddress().ToLowerInvariant(), await signer.GetAddressAsync("issuer-key-v1", Ct));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task LeadingZeroScalarsUseIndependentPaddedBuffersWithoutErasingTheSdkCache()
    {
        var scalar = RandomNumberGenerator.GetBytes(31);
        scalar[0] |= 1;
        var padded = new byte[32];
        scalar.CopyTo(padded, 1);
        var key = new EthECKey(padded, true);
        var keys = new StubKeys(_ => PrivateKeyCopy(key));
        var signer = new EvmCertificateSoftwareSigner(keys);
        var first = await signer.GetAddressAsync("issuer-key-v1", Ct);
        Assert.Equal(first, await signer.GetAddressAsync("issuer-key-v1", Ct));
        Assert.Equal(31, key.GetPrivateKeyAsBytes().Length);
        Assert.Contains(key.GetPrivateKeyAsBytes(), b => b != 0);
        Assert.All(keys.LastBuffer!, b => Assert.Equal((byte)0, b));
        CryptographicOperations.ZeroMemory(scalar);
        CryptographicOperations.ZeroMemory(padded);
    }

    private static byte[] PrivateKeyCopy(EthECKey key)
    {
        var sdkBytes = key.GetPrivateKeyAsBytes();
        var owned = new byte[32];
        sdkBytes.CopyTo(owned, owned.Length - sdkBytes.Length);
        return owned;
    }

    private static EvmCertificatePreparedTransaction Preparation(string signer) => new(
        EvmCertificatePublicationNetwork.Capture("issuer-a", new EvmCertificateNetworkOptions
        {
            ChainId = 12345, RpcEndpoint = "https://rpc.invalid/", ContractAddress = "0x" + new string('1', 40),
            IssuerId = "0x" + new string('2', 64), ExpectedRuntimeCodeSha256 = new string('3', 64),
            FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
        }), EvmCertificateOperationKind.Anchor, "0x" + new string('4', 64), signer.ToLowerInvariant(),
        7, EvmCertificateFeeBudget.Create(100000, 10, 1000000));

    private sealed class StubKeys(Func<CancellationToken, byte[]> resolve) : IEvmCertificatePrivateKeySource
    {
        internal byte[]? LastBuffer { get; private set; }
        internal int Reads { get; private set; }

        public ValueTask<byte[]> GetPrivateKeyAsync(string reference, CancellationToken cancellationToken)
        {
            if (reference != "issuer-key-v1")
            {
                throw new ArgumentException("Unknown test key reference.", nameof(reference));
            }
            Reads++;
            var bytes = resolve(cancellationToken);
            LastBuffer = bytes;
            return ValueTask.FromResult(bytes);
        }
    }
}
