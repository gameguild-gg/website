using System.Net;
using GameGuild.Assets.Openinary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.UnitTests.Services;

/// <summary>
/// T5 dual-path tests: when Openinary is enabled (ApiKey configured), NEW content
/// uploads go to Openinary; dedup hits reuse existing rows on any store; cached
/// unsafe verdicts fast-fail; failures write zero rows. Flag-off is covered by the
/// existing suites (regression proof).
/// </summary>
public class AssetUploadOpeninaryDualPathTests : IDisposable
{
    private readonly InMemoryAssetContentRepository _contentRepository = new();
    private readonly Mock<IAssetReferenceRepository> _referenceRepositoryMock = new();
    private readonly Mock<IAssetStorageService> _storageServiceMock = new();
    private readonly FakeOpeninaryClient _openinary = new();
    private readonly AssetUploadConfiguration _config = new() { MaxFileSizeBytes = 100 * 1024 * 1024 };
    private readonly OpeninaryOptions _openinaryOptions = new() { ApiKey = "test-key", FolderPrefix = "assets/" };
    private readonly List<IDisposable> _disposables = [];

    public AssetUploadOpeninaryDualPathTests()
    {
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReference candidate, CancellationToken _) => candidate);
        _storageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string contentHash, string mimeType, bool _, CancellationToken _) =>
                new StorageUploadResult("test-bucket", contentHash));
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private AssetUploadService CreateService(AssetUploadConfiguration? config = null, OpeninaryOptions? openinaryOptions = null) => new(
        _contentRepository,
        _referenceRepositoryMock.Object,
        _storageServiceMock.Object,
        Options.Create(config ?? _config),
        NullLogger<AssetUploadService>.Instance,
        openinaryOptions: Options.Create(openinaryOptions ?? _openinaryOptions),
        openinaryClient: _openinary);

    private MemoryStream Track(MemoryStream stream)
    {
        _disposables.Add(stream);
        return stream;
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_NewContentStoresInOpeninary()
    {
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var options = new UploadAssetOptions("photo.png", AssetAccessPolicy.Private, TenantId: tenantId);
        using var stream = new MemoryStream([1, 2, 3, 4]);

        var result = await service.UploadAsync(stream, "photo.png", "image/png", Guid.NewGuid(), options);

        result.Success.Should().BeTrue();
        _openinary.UploadCalls.Should().ContainSingle();
        var call = _openinary.UploadCalls[0];
        call.FileName.Should().Be($"{await Sha256HexAsync([1, 2, 3, 4])}.png");
        call.Folder.Should().Be("assets/" + tenantId);
        call.PrewarmTransforms.Should().BeEmpty("prewarm is owned by T6/T7");
        var content = await _contentRepository.GetByContentHashAsync(call.FileName[..64]);
        content.Should().NotBeNull();
        content!.BucketName.Should().Be("openinary");
        content.ObjectKey.Should().Be(call.ReturnedPath);
        _storageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Never,
            "new content must not hit legacy S3 storage when Openinary is enabled");
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_PlatformTenantUsesPlatformFolder()
    {
        var service = CreateService();
        using var stream = new MemoryStream([5, 6]);

        var result = await service.UploadAsync(
            stream, "doc.pdf", "application/pdf", Guid.NewGuid(),
            new UploadAssetOptions("doc.pdf", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        _openinary.UploadCalls[0].Folder.Should().Be("assets/platform");
        _openinary.UploadCalls[0].FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_ExtensionFallsBackToBin()
    {
        var service = CreateService();
        using var stream = new MemoryStream([7]);

        var result = await service.UploadAsync(
            stream, "blob.xyz", "application/x-weird", Guid.NewGuid(),
            new UploadAssetOptions("blob.xyz", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        _openinary.UploadCalls[0].FileName.Should().EndWith(".bin");
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_ExistingPathSkipsUpload_Idempotency()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var hash = await Sha256HexAsync(payload);
        _openinary.ExistingPaths.Add($"assets/platform/{hash}.png");
        var service = CreateService();
        using var stream = new MemoryStream(payload);

        var result = await service.UploadAsync(
            stream, "photo.png", "image/png", Guid.NewGuid(),
            new UploadAssetOptions("photo.png", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        _openinary.UploadCalls.Should().BeEmpty("metadata hit means the bytes are already stored");
        var content = await _contentRepository.GetByContentHashAsync(hash);
        content!.ObjectKey.Should().Be($"assets/platform/{hash}.png");
        content.BucketName.Should().Be("openinary");
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_DedupHitOnS3Row_ReusesRowUntouched()
    {
        var payload = new byte[] { 9, 9, 9 };
        var hash = await Sha256HexAsync(payload);
        var existing = _contentRepository.Seed(
            new AssetContent("legacy-bucket", "legacy-key", hash, "application/octet-stream", payload.Length, null, null));

        var service = CreateService();
        using var stream = new MemoryStream(payload);

        var result = await service.UploadAsync(
            stream, "dupe.bin", "application/octet-stream", Guid.NewGuid(),
            new UploadAssetOptions("dupe.bin", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        result.AssetContentId.Should().Be(existing.Id);
        _openinary.UploadCalls.Should().BeEmpty("dedup hits never call Openinary");
        _openinary.MetadataCalls.Should().Be(0, "dedup resolves before the store is consulted");
        _contentRepository.RowCount.Should().Be(1);
        existing.BucketName.Should().Be("legacy-bucket", "S3 rows are never migrated");
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_DedupHitOnOpeninaryRow_ReusesRow()
    {
        var payload = new byte[] { 1, 2 };
        var hash = await Sha256HexAsync(payload);
        var existing = _contentRepository.Seed(
            new AssetContent("openinary", "old/path.bin", hash, "application/octet-stream", payload.Length, null, null));

        var service = CreateService();
        using var stream = new MemoryStream(payload);

        var result = await service.UploadAsync(
            stream, "dupe.bin", "application/octet-stream", Guid.NewGuid(),
            new UploadAssetOptions("dupe.bin", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        result.AssetContentId.Should().Be(existing.Id);
        _openinary.UploadCalls.Should().BeEmpty();
        _contentRepository.RowCount.Should().Be(1);
        (await _contentRepository.GetByIdAsync(existing.Id))!.ReferenceCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ModerationStatus.Rejected, VirusScanStatus.Clean)]
    [InlineData(ModerationStatus.Blocked, VirusScanStatus.Clean)]
    [InlineData(ModerationStatus.Approved, VirusScanStatus.Infected)]
    public async Task UploadAsync_OpeninaryEnabled_QuarantinedExistingContent_FailsFastWithoutStoreWrite(
        ModerationStatus moderation, VirusScanStatus virusScan)
    {
        var payload = new byte[] { 3, 3, 3 };
        var hash = await Sha256HexAsync(payload);
        var existing = _contentRepository.Seed(
            new AssetContent("legacy-bucket", "legacy-key", hash, "application/octet-stream", payload.Length, null, null));
        existing.ModerationStatus = moderation;
        existing.VirusScanStatus = virusScan;

        var service = CreateService();
        using var stream = new MemoryStream(payload);

        var result = await service.UploadAsync(
            stream, "bad.bin", "application/octet-stream", Guid.NewGuid(),
            new UploadAssetOptions("bad.bin", AssetAccessPolicy.Private));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("quarantined");
        _openinary.UploadCalls.Should().BeEmpty("cached verdicts must fast-fail before any store call");
        _storageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Never);
        _referenceRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
        existing.ReferenceCount.Should().Be(0, "a rejected fast-fail must not increment the existing row");
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_OpeninaryThrows_FailsWithZeroRowsWritten()
    {
        _openinary.ThrowOnUpload = true;
        var service = CreateService();
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.UploadAsync(
            stream, "fail.png", "image/png", Guid.NewGuid(),
            new UploadAssetOptions("fail.png", AssetAccessPolicy.Private));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Openinary");
        _contentRepository.RowCount.Should().Be(0, "no AssetContent row may be written when the store upload fails");
        _referenceRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadAsync_OpeninaryEnabled_SizeClampedToOpeninaryLimit()
    {
        var service = CreateService(openinaryOptions: new OpeninaryOptions
        {
            ApiKey = "test-key",
            FolderPrefix = "assets/",
            MaxFileMb = 50,
        });
        var sixtyMb = new byte[60 * 1024 * 1024];
        using var stream = new MemoryStream(sixtyMb);

        var result = await service.UploadAsync(
            stream, "big.png", "image/png", Guid.NewGuid(),
            new UploadAssetOptions("big.png", AssetAccessPolicy.Private));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("50");
        _openinary.UploadCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAsync_OpeninaryDisabled_SkipsClampAndOpeninary()
    {
        var service = CreateService(openinaryOptions: new OpeninaryOptions { ApiKey = "" });
        using var stream = new MemoryStream([1, 2]);

        var result = await service.UploadAsync(
            stream, "s3.bin", "application/octet-stream", Guid.NewGuid(),
            new UploadAssetOptions("s3.bin", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        _openinary.UploadCalls.Should().BeEmpty();
        _openinary.MetadataCalls.Should().Be(0);
        var content = await _contentRepository.GetByContentHashAsync(await Sha256HexAsync([1, 2]));
        content!.BucketName.Should().Be("test-bucket");
    }

    [Fact]
    public async Task CompleteChunkedUploadAsync_OpeninaryEnabled_NewContentStoresInOpeninary()
    {
        var service = CreateService();
        var payload = new byte[] { 4, 4, 4, 4 };
        var uploadId = await RunFullChunkedUploadAsync(service, payload, "image/png");

        var result = await service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("chunked.png", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        _openinary.UploadCalls.Should().ContainSingle();
        _openinary.UploadCalls[0].FileName.Should().Be($"{await Sha256HexAsync(payload)}.png");
        var content = await _contentRepository.GetByContentHashAsync(await Sha256HexAsync(payload));
        content!.BucketName.Should().Be("openinary");
        content.ObjectKey.Should().Be(_openinary.UploadCalls[0].ReturnedPath);
    }

    [Fact]
    public async Task CompleteChunkedUploadAsync_OpeninaryEnabled_DedupHitReusesExistingRow()
    {
        var payload = new byte[] { 8, 8 };
        var hash = await Sha256HexAsync(payload);
        var existing = _contentRepository.Seed(
            new AssetContent("legacy-bucket", "legacy-key", hash, "image/png", payload.Length, null, null));

        var service = CreateService();
        var uploadId = await RunFullChunkedUploadAsync(service, payload, "image/png");

        var result = await service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("chunked.png", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        result.AssetContentId.Should().Be(existing.Id);
        _openinary.UploadCalls.Should().BeEmpty();
        existing.BucketName.Should().Be("legacy-bucket");
    }

    [Fact]
    public async Task CompleteChunkedUploadAsync_OpeninaryEnabled_QuarantinedExistingContent_FailsFast()
    {
        var payload = new byte[] { 7, 7 };
        var hash = await Sha256HexAsync(payload);
        var existing = _contentRepository.Seed(
            new AssetContent("legacy-bucket", "legacy-key", hash, "image/png", payload.Length, null, null));
        existing.ModerationStatus = ModerationStatus.Rejected;

        var service = CreateService();
        var uploadId = await RunFullChunkedUploadAsync(service, payload, "image/png");

        var result = await service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("chunked.png", AssetAccessPolicy.Private));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("quarantined");
        _openinary.UploadCalls.Should().BeEmpty();
        existing.ReferenceCount.Should().Be(0);
    }

    [Fact]
    public void AssetStorageBackend_IsOpeninary_DetectsBackend()
    {
        var openinaryContent = new AssetContent("openinary", "p", new string('a', 64), "image/png", 1, null, null);
        var s3Content = new AssetContent("assets", "p", new string('b', 64), "image/png", 1, null, null);

        AssetStorageBackend.IsOpeninary(openinaryContent).Should().BeTrue();
        AssetStorageBackend.IsOpeninary(s3Content).Should().BeFalse();
        OpeninaryOptions.StorageBackendName.Should().Be("openinary");
    }

    private async Task<string> RunFullChunkedUploadAsync(
        AssetUploadService service, byte[] payload, string mimeType)
    {
        var uploadId = $"upload-{Guid.NewGuid():N}";
        _storageServiceMock
            .Setup(x => x.InitiateMultipartUploadAsync(mimeType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadId);
        _storageServiceMock
            .Setup(x => x.UploadPartAsync(uploadId, $"multipart/{uploadId}", It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int partNumber, Stream _, CancellationToken _) => $"etag-{partNumber}");
        _storageServiceMock
            .Setup(x => x.CompleteMultipartUploadAsync(uploadId, It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadResult("test-bucket", $"multipart/{uploadId}"));
        var downloadStream = Track(new MemoryStream(payload));
        _storageServiceMock
            .Setup(x => x.DownloadAsync("test-bucket", $"multipart/{uploadId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadStream);

        await service.InitiateChunkedUploadAsync("chunked.bin", mimeType, payload.Length, Guid.NewGuid());
        (await service.UploadChunkAsync(uploadId, 0, new MemoryStream(payload))).Should().BeTrue();
        return uploadId;
    }

    private static async Task<string> Sha256HexAsync(byte[] payload)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(await sha256.ComputeHashAsync(new MemoryStream(payload))).ToLowerInvariant();
    }
}

/// <summary>
/// Fake <see cref="IOpeninaryClient"/> capturing calls; optionally simulating an
/// existing object (idempotency skip) or a hard failure.
/// </summary>
internal sealed class FakeOpeninaryClient : IOpeninaryClient
{
    public sealed record UploadCall(string FileName, string Folder, IReadOnlyCollection<string> PrewarmTransforms, string ReturnedPath);

    public List<UploadCall> UploadCalls { get; } = [];

    public HashSet<string> ExistingPaths { get; } = [];

    public int MetadataCalls { get; private set; }

    public bool ThrowOnUpload { get; set; }

    private string NextPath { get; } = $"openinary-path-{Guid.NewGuid():N}";

    public Task<OpeninaryUploadResult> UploadAsync(
        Stream content, string fileName, string folder,
        IReadOnlyCollection<string> prewarmTransforms, CancellationToken cancellationToken)
    {
        if (ThrowOnUpload)
        {
            throw new OpeninaryException(HttpStatusCode.InternalServerError, "Openinary upload failed");
        }

        UploadCalls.Add(new UploadCall(fileName, folder, prewarmTransforms, NextPath));
        return Task.FromResult(new OpeninaryUploadResult(NextPath, $"https://cdn.test/{NextPath}", 42, []));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task InvalidateAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<OpeninaryFileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken)
    {
        MetadataCalls++;
        if (ExistingPaths.Contains(path))
        {
            return Task.FromResult(new OpeninaryFileMetadata(path, 42, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        throw new OpeninaryException(HttpStatusCode.NotFound, "not found");
    }
}
