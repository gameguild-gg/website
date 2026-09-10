using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class AwsEconomySecurityAdaptersTests
{
    [Fact]
    public async Task KmsAdapterSignsAndVerifiesSha256DigestWithStoredAlgorithm()
    {
        SignRequest? signRequest = null;
        VerifyRequest? verifyRequest = null;
        var kms = new Mock<IAmazonKeyManagementService>();
        kms.Setup(client => client.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SignRequest, CancellationToken>((request, _) => signRequest = request)
            .ReturnsAsync(new SignResponse
            {
                KeyId = "arn:aws:kms:region:account:key/one",
                Signature = new MemoryStream([1, 2, 3]),
                SigningAlgorithm = SigningAlgorithmSpec.ECDSA_SHA_256
            });
        kms.Setup(client => client.VerifyAsync(It.IsAny<VerifyRequest>(), It.IsAny<CancellationToken>()))
            .Callback<VerifyRequest, CancellationToken>((request, _) => verifyRequest = request)
            .ReturnsAsync(new VerifyResponse { SignatureValid = true });
        var adapter = new AwsKmsEconomyCryptography(
            kms.Object,
            Options.Create(new EconomyKmsOptions
            {
                Enabled = true,
                KeyId = "arn:aws:kms:region:account:key/one",
                SigningAlgorithm = "ECDSA_SHA_256"
            }));

        var signature = await adapter.SignAsync("canonical-payload", CancellationToken.None);
        var verified = await adapter.VerifyAsync(
            "canonical-payload", signature.KeyId, signature.Signature, CancellationToken.None);

        signature.Signature.Should().Be("ECDSA_SHA_256:AQID");
        verified.Should().BeTrue();
        signRequest!.MessageType.Should().Be(MessageType.DIGEST);
        signRequest.Message.ToArray().Should().HaveCount(32);
        verifyRequest!.KeyId.Should().Be(signature.KeyId);
        verifyRequest.Signature.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task KmsAdapterFailsClosedForMissingConfigurationAndMalformedSignature()
    {
        var kms = new Mock<IAmazonKeyManagementService>();
        var disabled = new AwsKmsEconomyCryptography(kms.Object, Options.Create(new EconomyKmsOptions()));

        await FluentActions.Awaiting(() => disabled.SignAsync("payload", CancellationToken.None).AsTask())
            .Should().ThrowAsync<EconomyCryptographyUnavailableException>();
        (await disabled.VerifyAsync("payload", "key", "bad", CancellationToken.None)).Should().BeFalse();
        kms.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task S3WormAdapterRequiresObjectLockVersionAndExactReadBack()
    {
        PutObjectRequest? putRequest = null;
        var payload = "immutable-anchor";
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => putRequest = request)
            .ReturnsAsync(new PutObjectResponse { VersionId = "version-1", ETag = "etag-1" });
        s3.Setup(client => client.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse
            {
                VersionId = "version-1",
                ETag = "etag-1",
                ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(payload))
            });
        s3.Setup(client => client.GetObjectRetentionAsync(It.IsAny<GetObjectRetentionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectRetentionResponse
            {
                Retention = new ObjectLockRetention
                {
                    Mode = ObjectLockRetentionMode.Compliance,
                    RetainUntilDate = new DateTime(2027, 8, 24, 0, 0, 0, DateTimeKind.Utc)
                }
            });
        var store = new S3ObjectLockWormAnchorStore(
            s3.Object,
            Options.Create(new EconomyWormOptions
            {
                Enabled = true,
                BucketName = "economy-worm",
                KeyPrefix = "anchors",
                RetentionDays = 365
            }));
        var publishedAt = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);

        var result = await store.PublishAsync(
            new WormAnchorObject(Guid.NewGuid(), payload, publishedAt), CancellationToken.None);

        result.ObjectVersion.Should().Be("version-1");
        result.ETag.Should().Be("etag-1");
        result.ReadBackVerified.Should().BeTrue();
        putRequest!.ObjectLockMode.Should().Be(ObjectLockMode.Compliance);
        putRequest.ObjectLockRetainUntilDate.Should().Be(publishedAt.AddDays(365).UtcDateTime);
        putRequest.Key.Should().StartWith("anchors/");
    }

    [Fact]
    public async Task S3WormAdapterFailsClosedWithoutCredentialsOrVersionedReadBack()
    {
        var s3 = new Mock<IAmazonS3>();
        var disabled = new S3ObjectLockWormAnchorStore(s3.Object, Options.Create(new EconomyWormOptions()));
        await FluentActions.Awaiting(() => disabled.PublishAsync(
                new WormAnchorObject(Guid.NewGuid(), "payload", DateTimeOffset.UtcNow), CancellationToken.None).AsTask())
            .Should().ThrowAsync<WormAnchorUnavailableException>();

        s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { ETag = "etag-without-version" });
        var invalid = new S3ObjectLockWormAnchorStore(
            s3.Object,
            Options.Create(new EconomyWormOptions
            {
                Enabled = true, BucketName = "bucket", KeyPrefix = "anchors", RetentionDays = 1
            }));
        await FluentActions.Awaiting(() => invalid.PublishAsync(
                new WormAnchorObject(Guid.NewGuid(), "payload", DateTimeOffset.UtcNow), CancellationToken.None).AsTask())
            .Should().ThrowAsync<WormAnchorVerificationException>();
    }

    [Fact]
    public async Task UnavailableWormAdapterFailsClosedForPublishAndReadBack()
    {
        var store = new UnavailableWormAnchorStore();

        await FluentActions.Awaiting(() => store.PublishAsync(
                new WormAnchorObject(Guid.NewGuid(), "payload", DateTimeOffset.UtcNow),
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<WormAnchorUnavailableException>();
        await FluentActions.Awaiting(() => store.ReadAndVerifyAsync(
                new WormAnchorReference(
                    "key", "version", "etag", DateTimeOffset.UtcNow.AddDays(1), "hash"),
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<WormAnchorUnavailableException>();
    }
}
