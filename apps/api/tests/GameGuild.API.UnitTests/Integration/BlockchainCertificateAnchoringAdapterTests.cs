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
            .ReturnsAsync((Guid userId, string data, string type) => BlockchainCertificateAnchorResult.Success(
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
}
