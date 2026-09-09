using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class EconomyWormOptions
{
    public const string SectionName = "Modules:Economy:Ledger:Worm";

    public bool Enabled { get; set; }
    public string BucketName { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
}

public sealed record WormAnchorObject(Guid AnchorId, string CanonicalPayload, DateTimeOffset PublishedAt);

public sealed record WormAnchorPublication(
    string ObjectKey,
    string ObjectVersion,
    string ETag,
    DateTimeOffset RetainUntil,
    string ObjectHash,
    bool ReadBackVerified);

public interface IWormAnchorStore
{
    ValueTask<WormAnchorPublication> PublishAsync(
        WormAnchorObject anchor,
        CancellationToken cancellationToken);
}

public sealed record WormAnchorReference(
    string ObjectKey,
    string ObjectVersion,
    string ETag,
    DateTimeOffset RetainUntil,
    string ObjectHash);

public sealed record WormAnchorReadBack(
    string CanonicalPayload,
    string ObjectVersion,
    string ETag,
    DateTimeOffset RetainUntil,
    string ObjectHash,
    bool ObjectMatches);

public interface IWormAnchorVerifier
{
    ValueTask<WormAnchorReadBack> ReadAndVerifyAsync(
        WormAnchorReference reference,
        CancellationToken cancellationToken);
}

public sealed class S3ObjectLockWormAnchorStore : IWormAnchorStore, IWormAnchorVerifier
{
    private readonly IAmazonS3 _s3;
    private readonly EconomyWormOptions _options;

    public S3ObjectLockWormAnchorStore(IAmazonS3 s3, IOptions<EconomyWormOptions> options)
    {
        ArgumentNullException.ThrowIfNull(s3);
        ArgumentNullException.ThrowIfNull(options);
        _s3 = s3;
        _options = options.Value;
    }

    public async ValueTask<WormAnchorPublication> PublishAsync(
        WormAnchorObject anchor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (anchor.AnchorId == Guid.Empty) throw new ArgumentException("Anchor ID cannot be empty.", nameof(anchor));
        ArgumentException.ThrowIfNullOrWhiteSpace(anchor.CanonicalPayload);
        EnsureConfigured();
        var bytes = Encoding.UTF8.GetBytes(anchor.CanonicalPayload);
        var objectHashBytes = SHA256.HashData(bytes);
        var objectHash = Convert.ToHexStringLower(objectHashBytes);
        var retainUntil = anchor.PublishedAt.AddDays(_options.RetentionDays);
        var prefix = _options.KeyPrefix.Trim().Trim('/');
        var objectKey = $"{prefix}/{anchor.PublishedAt:yyyy/MM/dd}/{anchor.AnchorId:N}.json";
        using var stream = new MemoryStream(bytes, writable: false);
        var response = await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName.Trim(),
            Key = objectKey,
            InputStream = stream,
            ContentType = "application/json",
            AutoCloseStream = false,
            ChecksumSHA256 = Convert.ToBase64String(objectHashBytes),
            ObjectLockMode = ObjectLockMode.Compliance,
            ObjectLockRetainUntilDate = retainUntil.UtcDateTime
        }, cancellationToken);
        if (string.IsNullOrWhiteSpace(response.VersionId) || string.IsNullOrWhiteSpace(response.ETag))
            throw new WormAnchorVerificationException(
                "S3 Object Lock publication did not return an immutable object version and ETag.");

        using var readBack = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.BucketName.Trim(), Key = objectKey, VersionId = response.VersionId
        }, cancellationToken);
        using var memory = new MemoryStream();
        await readBack.ResponseStream.CopyToAsync(memory, cancellationToken);
        var readBackMatches = CryptographicOperations.FixedTimeEquals(
            objectHashBytes,
            SHA256.HashData(memory.ToArray()));
        if (!readBackMatches || readBack.VersionId != response.VersionId || readBack.ETag != response.ETag)
            throw new WormAnchorVerificationException("S3 Object Lock read-back did not match the published anchor.");

        var retention = await _s3.GetObjectRetentionAsync(new GetObjectRetentionRequest
        {
            BucketName = _options.BucketName.Trim(), Key = objectKey, VersionId = response.VersionId
        }, cancellationToken);
        if (retention.Retention?.Mode != ObjectLockRetentionMode.Compliance ||
            retention.Retention.RetainUntilDate.ToUniversalTime() < retainUntil.UtcDateTime)
            throw new WormAnchorVerificationException("S3 Object Lock COMPLIANCE retention was not durably applied.");

        return new WormAnchorPublication(
            objectKey, response.VersionId, response.ETag, retainUntil, objectHash, true);
    }

    public async ValueTask<WormAnchorReadBack> ReadAndVerifyAsync(
        WormAnchorReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureConfigured();
        using var response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.BucketName.Trim(),
            Key = reference.ObjectKey,
            VersionId = reference.ObjectVersion
        }, cancellationToken);
        using var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var retention = await _s3.GetObjectRetentionAsync(new GetObjectRetentionRequest
        {
            BucketName = _options.BucketName.Trim(),
            Key = reference.ObjectKey,
            VersionId = reference.ObjectVersion
        }, cancellationToken);
        var retainUntil = new DateTimeOffset(retention.Retention.RetainUntilDate.ToUniversalTime());
        var matches = response.VersionId == reference.ObjectVersion && response.ETag == reference.ETag &&
                      string.Equals(hash, reference.ObjectHash, StringComparison.Ordinal) &&
                      retention.Retention.Mode == ObjectLockRetentionMode.Compliance &&
                      retainUntil >= reference.RetainUntil;
        return new WormAnchorReadBack(
            Encoding.UTF8.GetString(bytes), response.VersionId, response.ETag, retainUntil, hash, matches);
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BucketName) ||
            string.IsNullOrWhiteSpace(_options.KeyPrefix) || _options.RetentionDays <= 0)
            throw new WormAnchorUnavailableException(
                "Economy WORM anchoring is disabled until an S3 Object Lock bucket and retention policy are configured.");
    }
}

public sealed class WormAnchorUnavailableException(string message) : InvalidOperationException(message);
public sealed class WormAnchorVerificationException(string message) : InvalidOperationException(message);

public sealed class UnavailableWormAnchorStore : IWormAnchorStore, IWormAnchorVerifier
{
    public ValueTask<WormAnchorPublication> PublishAsync(
        WormAnchorObject anchor,
        CancellationToken cancellationToken) =>
        ValueTask.FromException<WormAnchorPublication>(new WormAnchorUnavailableException(
            "Economy WORM anchoring is disabled until S3 Object Lock is configured."));

    public ValueTask<WormAnchorReadBack> ReadAndVerifyAsync(
        WormAnchorReference reference,
        CancellationToken cancellationToken) =>
        ValueTask.FromException<WormAnchorReadBack>(new WormAnchorUnavailableException(
            "Economy WORM verification is disabled until S3 Object Lock is configured."));
}
