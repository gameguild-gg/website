using FluentAssertions;
using GameGuild.API.Core.Integration;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Integration;

public class BlockchainCertificateAnchoringAdapterTests
{
    private static Certificate CreateCertificate()
    {
        return Certificate.Issue(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Ada Lovelace", "Foundations of Computing");
    }

    [Fact]
    public async Task AnchorIssuedCertificateAsync_PublishesOnlyCanonicalHashWithoutPII()
    {
        var blockchain = new Mock<IBlockchainCertificateService>();
        string? capturedPayload = null;
        string? capturedType = null;
        Guid? capturedUserId = null;
        blockchain
            .Setup(service => service.AnchorCertificateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Guid, string, string>((userId, data, type) =>
            {
                capturedUserId = userId;
                capturedPayload = data;
                capturedType = type;
            })
            .ReturnsAsync((Guid _, string data, string _) => BlockchainCertificateAnchorResult.Success(
                "0xtx", "local", BlockchainCertificateCanonicalizer.ComputeSha256Hex(data), 1L, DateTime.UtcNow));

        var certificate = CreateCertificate();
        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        await adapter.AnchorIssuedCertificateAsync(certificate);

        capturedUserId.Should().Be(certificate.UserId);
        capturedType.Should().Be(BlockchainCertificateAnchoringAdapter.CertificateType);
        capturedPayload.Should().NotBeNull();
        capturedPayload.Should().NotContain("Ada Lovelace");
        capturedPayload.Should().NotContain("Foundations of Computing");
        // The canonical payload contains exactly the five whitelisted keys, with a hashed recipient.
        capturedPayload.Should().Contain(BlockchainCertificateCanonicalizer.RecipientHashKey);
        capturedPayload.Should().Contain(BlockchainCertificateCanonicalizer.ComputeSha256Hex(certificate.RecipientName));
        using var parsed = System.Text.Json.JsonDocument.Parse(capturedPayload!);
        var keys = parsed.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToList();
        keys.Should().BeEquivalentTo(new[]
        {
            BlockchainCertificateCanonicalizer.IdKey,
            BlockchainCertificateCanonicalizer.IssuedAtKey,
            BlockchainCertificateCanonicalizer.IssuerKey,
            BlockchainCertificateCanonicalizer.RecipientHashKey,
            BlockchainCertificateCanonicalizer.VersionKey
        });
    }

    [Fact]
    public async Task AnchorIssuedCertificateAsync_SwallowsDisabledProviderFailure()
    {
        var adapter = new BlockchainCertificateAnchoringAdapter(
            new DisabledBlockchainCertificateService(),
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var act = () => adapter.AnchorIssuedCertificateAsync(CreateCertificate());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordRevocationAsync_RevokesTheRecomputedIssuanceHash()
    {
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.AnchorCertificateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Guid _, string data, string _) => BlockchainCertificateAnchorResult.Success(
                "0xanchor", "local", BlockchainCertificateCanonicalizer.ComputeSha256Hex(data), 1L, DateTime.UtcNow));
        blockchain
            .Setup(service => service.RevokeCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("0xrevoke");

        var options = BlockchainCertificateOptions.CreateDefault();
        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object, options, NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);
        var certificate = CreateCertificate();
        await adapter.AnchorIssuedCertificateAsync(certificate);

        await adapter.RecordRevocationAsync(certificate, "compromised issuance");

        var expectedHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex(
            BlockchainCertificateCanonicalizer.BuildIssuancePayload(
                certificate.Id,
                BlockchainCertificateCanonicalizer.ComputeSha256Hex(certificate.RecipientName),
                certificate.IssuedAt,
                options.Issuer,
                1));
        blockchain.Verify(service => service.RevokeCertificateAsync(expectedHash, "compromised issuance"), Times.Once);
    }

    [Fact]
    public async Task RecordRevocationAsync_SkipsCertificatesThatWereNeverAnchored()
    {
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.RevokeCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(
                "No anchored certificate matches hash 'abc'. Only anchored certificates can be revoked."));

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var act = () => adapter.RecordRevocationAsync(CreateCertificate(), "reason");

        await act.Should().NotThrowAsync();
    }

    private static string BuildExpectedPayload(Certificate certificate, string recipientName)
    {
        return BlockchainCertificateCanonicalizer.BuildIssuancePayload(
            certificate.Id,
            BlockchainCertificateCanonicalizer.ComputeSha256Hex(recipientName),
            certificate.IssuedAt,
            BlockchainCertificateOptions.CreateDefault().Issuer,
            1);
    }

    private static BlockchainCertificateAnchor CreateAnchor(string payload, string transactionHash, bool isRevoked = false)
    {
        return new BlockchainCertificateAnchor
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CertificateType = BlockchainCertificateAnchoringAdapter.CertificateType,
            CertificateHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex(payload),
            CertificateData = payload,
            TransactionHash = transactionHash,
            BlockchainNetwork = "local",
            BlockNumber = 42,
            AnchoredAt = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            IsRevoked = isRevoked
        };
    }

    [Fact]
    public async Task VerifyAnchoringAsync_ConfirmsAnchoredDigestWithChainEvidence()
    {
        var certificate = CreateCertificate();
        var payload = BuildExpectedPayload(certificate, certificate.RecipientName);
        var expectedDigest = BlockchainCertificateCanonicalizer.ComputeSha256Hex(payload);
        var anchor = CreateAnchor(payload, "0xanchor-tx");
        anchor.UserId = certificate.UserId;
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(certificate.UserId))
            .ReturnsAsync([anchor]);
        blockchain
            .Setup(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.Verified);
        verification.IsVerified.Should().BeTrue();
        verification.Digest.Should().Be(expectedDigest);
        verification.AnchoredDigest.Should().Be(expectedDigest);
        verification.TransactionHash.Should().Be("0xanchor-tx");
        verification.Network.Should().Be("local");
        verification.BlockNumber.Should().Be(42);
        verification.AnchoredAt.Should().Be(anchor.AnchoredAt);
        blockchain.Verify(service => service.VerifyCertificateAsync(expectedDigest, "0xanchor-tx"), Times.Once);
    }

    [Fact]
    public async Task VerifyAnchoringAsync_ReportsNotAnchored_WhenNoAnchorMatchesTheDigest()
    {
        var certificate = CreateCertificate();
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(It.IsAny<Guid>()))
            .ReturnsAsync([]);

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.NotAnchored);
        verification.IsVerified.Should().BeFalse();
        verification.Digest.Should().NotBeNullOrWhiteSpace();
        blockchain.Verify(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAnchoringAsync_ReportsDigestMismatch_WhenAnchorCoversDifferentData()
    {
        // The certificate record changed after anchoring (recipient edited): the recomputed digest
        // no longer matches the anchored digest for the same certificate id.
        var certificate = CreateCertificate();
        var stalePayload = BuildExpectedPayload(certificate, "Grace Hopper");
        var staleAnchor = CreateAnchor(stalePayload, "0xstale-tx");
        staleAnchor.UserId = certificate.UserId;
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(certificate.UserId))
            .ReturnsAsync([staleAnchor]);

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.DigestMismatch);
        verification.IsVerified.Should().BeFalse();
        verification.AnchoredDigest.Should().Be(staleAnchor.CertificateHash);
        verification.Digest.Should().NotBe(verification.AnchoredDigest);
        blockchain.Verify(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAnchoringAsync_ReportsDigestMismatch_WhenChainFailsToCorroborate()
    {
        var certificate = CreateCertificate();
        var payload = BuildExpectedPayload(certificate, certificate.RecipientName);
        var anchor = CreateAnchor(payload, "0xtampered");
        anchor.UserId = certificate.UserId;
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(certificate.UserId))
            .ReturnsAsync([anchor]);
        blockchain
            .Setup(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.DigestMismatch);
        verification.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAnchoringAsync_ReportsRevoked_WhenAnchorHasRevocationRecord()
    {
        var certificate = CreateCertificate();
        var payload = BuildExpectedPayload(certificate, certificate.RecipientName);
        var anchor = CreateAnchor(payload, "0xrevoked-tx", isRevoked: true);
        anchor.UserId = certificate.UserId;
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(certificate.UserId))
            .ReturnsAsync([anchor]);
        blockchain
            .Setup(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.Revoked);
        verification.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAnchoringAsync_FailsClosedAsUnreachable_WhenAnchorQueryThrows()
    {
        var certificate = CreateCertificate();
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("chain rpc unreachable"));

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.Unreachable);
        verification.IsVerified.Should().BeFalse();
        verification.Digest.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VerifyAnchoringAsync_FailsClosedAsUnreachable_WhenChainConfirmationThrows()
    {
        var certificate = CreateCertificate();
        var payload = BuildExpectedPayload(certificate, certificate.RecipientName);
        var anchor = CreateAnchor(payload, "0xanchor-tx");
        anchor.UserId = certificate.UserId;
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(certificate.UserId))
            .ReturnsAsync([anchor]);
        blockchain
            .Setup(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new TimeoutException("chain confirmation timed out"));

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.Unreachable);
        verification.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAnchoringAsync_UsesTheSameDigestTheAnchoringPathPublished()
    {
        // Digest recomputation must be identical to the anchoring path: capture the anchored
        // payload, then assert verification queries the chain for that payload's digest.
        var certificate = CreateCertificate();
        string? anchoredPayload = null;
        var blockchain = new Mock<IBlockchainCertificateService>();
        blockchain
            .Setup(service => service.AnchorCertificateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Guid, string, string>((_, data, _) => anchoredPayload = data)
            .ReturnsAsync((Guid _, string data, string _) => BlockchainCertificateAnchorResult.Success(
                "0xroundtrip", "local", BlockchainCertificateCanonicalizer.ComputeSha256Hex(data), 1L, DateTime.UtcNow));
        blockchain
            .Setup(service => service.GetUserCertificatesAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid userId) => anchoredPayload is null
                ? []
                : [CreateAnchor(anchoredPayload, "0xroundtrip")]);
        blockchain
            .Setup(service => service.VerifyCertificateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var adapter = new BlockchainCertificateAnchoringAdapter(
            blockchain.Object,
            BlockchainCertificateOptions.CreateDefault(),
            NullLogger<BlockchainCertificateAnchoringAdapter>.Instance);

        await adapter.AnchorIssuedCertificateAsync(certificate);
        var verification = await adapter.VerifyAnchoringAsync(certificate);

        verification.Status.Should().Be(CertificateAnchorVerificationStatus.Verified);
        blockchain.Verify(
            service => service.VerifyCertificateAsync(
                BlockchainCertificateCanonicalizer.ComputeSha256Hex(anchoredPayload!),
                "0xroundtrip"),
            Times.Once);
    }
}
