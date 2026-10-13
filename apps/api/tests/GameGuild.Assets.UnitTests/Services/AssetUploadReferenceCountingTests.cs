using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.UnitTests.Services;

/// <summary>
/// Reference-counting regression tests for the asset upload paths (T1: RED → GREEN).
/// A fresh upload must produce ReferenceCount == 1; a deduplicated upload must reuse
/// the existing content row and increment its count.
/// </summary>
public class AssetUploadReferenceCountingTests : IDisposable
{
    private readonly InMemoryAssetContentRepository _contentRepository = new();
    private readonly Mock<IAssetReferenceRepository> _referenceRepositoryMock = new();
    private readonly Mock<IAssetStorageService> _storageServiceMock = new();
    private readonly AssetUploadService _service;
    private readonly List<IDisposable> _disposables = [];

    public AssetUploadReferenceCountingTests()
    {
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReference candidate, CancellationToken _) => candidate);
        _storageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string contentHash, string mimeType, bool _, CancellationToken _) =>
                new StorageUploadResult("test-bucket", contentHash));
        _service = new AssetUploadService(
            _contentRepository,
            _referenceRepositoryMock.Object,
            _storageServiceMock.Object,
            Options.Create(new AssetUploadConfiguration()),
            NullLogger<AssetUploadService>.Instance);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    private MemoryStream Track(MemoryStream stream)
    {
        _disposables.Add(stream);
        return stream;
    }

    [Fact]
    public async Task UploadAsync_NewContent_SetsReferenceCountToOne()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var options = new UploadAssetOptions("fresh.bin", AssetAccessPolicy.Private, null, null);

        var result = await _service.UploadAsync(stream, "fresh.bin", "application/octet-stream", Guid.NewGuid(), options);

        result.Success.Should().BeTrue();
        _contentRepository.RowCount.Should().Be(1);
        var content = (await _contentRepository.GetByContentHashAsync(
            (await System.Security.Cryptography.SHA256.HashDataAsync(
                new MemoryStream([1, 2, 3], writable: false))).ToHexForTest()))!;
        content.Should().NotBeNull();
        content.ReferenceCount.Should().Be(1, "a fresh upload must be GC-visible from birth");
        content.MarkedForDeletionAt.Should().BeNull();
    }

    [Fact]
    public async Task UploadAsync_DuplicateContent_ReusesRowAndSetsReferenceCountToTwo()
    {
        using var first = new MemoryStream([1, 2, 3]);
        using var second = new MemoryStream([1, 2, 3]);
        var userId = Guid.NewGuid();
        var options = new UploadAssetOptions("fresh.bin", AssetAccessPolicy.Private, null, null);

        var firstResult = await _service.UploadAsync(first, "fresh.bin", "application/octet-stream", userId, options);
        var secondResult = await _service.UploadAsync(second, "fresh.bin", "application/octet-stream", userId, options);

        secondResult.Success.Should().BeTrue();
        secondResult.AssetContentId.Should().Be(firstResult.AssetContentId,
            "identical bytes must reuse the existing content row");
        _contentRepository.RowCount.Should().Be(1, "no duplicate content row may be created");
        var content = await _contentRepository.GetByIdAsync(firstResult.AssetContentId!.Value);
        content!.ReferenceCount.Should().Be(2);
        _storageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteChunkedUploadAsync_NewContent_SetsReferenceCountToOne()
    {
        var uploadId = await RunFullChunkedUploadAsync([1, 2, 3, 4], "application/octet-stream");

        var result = await _service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("chunked.bin", AssetAccessPolicy.Private, null, null));

        result.Success.Should().BeTrue();
        _contentRepository.RowCount.Should().Be(1);
        var content = await _contentRepository.GetByIdAsync(result.AssetContentId!.Value);
        content!.ReferenceCount.Should().Be(1, "a completed chunked upload must be GC-visible from birth");
    }

    [Fact]
    public async Task CompleteChunkedUploadAsync_WithExistingHash_ReusesContentRowAndIncrements()
    {
        // Seed an already-stored content row whose hash matches the chunked payload.
        var payload = new byte[] { 9, 9, 9 };
        var hash = await Sha256HexAsync(payload);
        var existing = _contentRepository.Seed(new AssetContent("test-bucket", "existing-key", hash, "application/octet-stream", payload.Length, null, null));

        var uploadId = await RunFullChunkedUploadAsync(payload, "application/octet-stream");

        var result = await _service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("chunked.bin", AssetAccessPolicy.Private, null, null));

        result.Success.Should().BeTrue();
        result.AssetContentId.Should().Be(existing.Id,
            "a chunked upload of already-stored bytes must reuse the content row instead of creating a duplicate");
        _contentRepository.RowCount.Should().Be(1);
        var content = await _contentRepository.GetByIdAsync(existing.Id);
        content!.ReferenceCount.Should().Be(1);
    }

    private async Task<string> RunFullChunkedUploadAsync(byte[] payload, string mimeType)
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

        await _service.InitiateChunkedUploadAsync("chunked.bin", mimeType, payload.Length, Guid.NewGuid());
        (await _service.UploadChunkAsync(uploadId, 0, new MemoryStream(payload))).Should().BeTrue();
        return uploadId;
    }

    private static async Task<string> Sha256HexAsync(byte[] payload)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(await sha256.ComputeHashAsync(new MemoryStream(payload))).ToLowerInvariant();
    }
}

internal static class AssetUploadResultTestExtensions
{
    public static string ToHexForTest(this byte[] hash) =>
        Convert.ToHexString(hash).ToLowerInvariant();
}
