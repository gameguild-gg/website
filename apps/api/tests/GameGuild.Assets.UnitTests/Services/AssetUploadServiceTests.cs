using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild;
using GameGuild.Assets;
using GameGuild.Assets.Commands;

namespace GameGuild.Assets.UnitTests.Services;

public class AssetUploadServiceTests
{
    private readonly Mock<IAssetContentRepository> _contentRepositoryMock;
    private readonly Mock<IAssetReferenceRepository> _referenceRepositoryMock;
    private readonly Mock<IAssetStorageService> _storageServiceMock;
    private readonly Mock<ILogger<AssetUploadService>> _loggerMock;
    private readonly AssetUploadConfiguration _config;
    private readonly AssetUploadService _service;

    public AssetUploadServiceTests()
    {
        _contentRepositoryMock = new Mock<IAssetContentRepository>();
        _referenceRepositoryMock = new Mock<IAssetReferenceRepository>();
        _storageServiceMock = new Mock<IAssetStorageService>();
        _loggerMock = new Mock<ILogger<AssetUploadService>>();
        _config = new AssetUploadConfiguration
        {
            MaxFileSizeBytes = 10 * 1024 * 1024, // 10 MB
            ChunkedUploadThreshold = 5 * 1024 * 1024, // 5 MB
            ChunkSizeBytes = 1024 * 1024, // 1 MB
            AllowedMimeTypes = new[] { "image/png", "image/jpeg", "image/gif" },
            ChunkedUploadExpiryMinutes = 60
        };

        _service = new AssetUploadService(
            _contentRepositoryMock.Object,
            _referenceRepositoryMock.Object,
            _storageServiceMock.Object,
            Options.Create(_config),
            _loggerMock.Object);
    }

    #region UploadAsync Tests

    [Fact]
    public async Task UploadAsync_FileTooLarge_ReturnsError()
    {
        // Arrange
        var largeContent = new byte[_config.MaxFileSizeBytes + 1];
        using var stream = new MemoryStream(largeContent);
        var options = new UploadAssetOptions("test.png", AssetAccessPolicy.Public, null, null);

        // Act
        var result = await _service.UploadAsync(
            stream, "test.png", "image/png", Guid.NewGuid(), options);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("size exceeds maximum");
    }

    [Fact]
    public async Task UploadAsync_DisallowedMimeType_ReturnsError()
    {
        // Arrange
        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var options = new UploadAssetOptions("test.exe", AssetAccessPolicy.Public, null, null);

        // Act
        var result = await _service.UploadAsync(
            stream, "test.exe", "application/x-executable", Guid.NewGuid(), options);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task UploadAsync_EmptyAllowedTypes_AllowsAnyType()
    {
        // Arrange
        var configWithNoRestriction = new AssetUploadConfiguration
        {
            MaxFileSizeBytes = 10 * 1024 * 1024,
            AllowedMimeTypes = Array.Empty<string>()
        };
        var service = new AssetUploadService(
            _contentRepositoryMock.Object,
            _referenceRepositoryMock.Object,
            _storageServiceMock.Object,
            Options.Create(configWithNoRestriction),
            _loggerMock.Object);

        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var userId = Guid.NewGuid();
        var options = new UploadAssetOptions("test.bin", AssetAccessPolicy.Public, null, null);

        _contentRepositoryMock
            .Setup(x => x.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        _storageServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadResult("bucket", "key/test.bin"));

        var contentResult = new AssetContent("bucket", "key", "hash", "application/octet-stream", 1024, null, null);
        typeof(AssetContent).GetProperty("Id")?.SetValue(contentResult, Guid.NewGuid());
        _contentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contentResult);

        var referenceResult = new AssetReference(contentResult.Id, userId, "test.bin", AssetAccessPolicy.Public, null, null);
        typeof(AssetReference).GetProperty("Id")?.SetValue(referenceResult, Guid.NewGuid());
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(referenceResult);

        // Act
        var result = await service.UploadAsync(
            stream, "test.bin", "application/octet-stream", userId, options);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_DuplicateContent_ReusesExisting()
    {
        // Arrange
        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var userId = Guid.NewGuid();
        var options = new UploadAssetOptions("test.png", AssetAccessPolicy.Public, null, null);

        var existingContent = new AssetContent("bucket", "key/existing.png", "hash", "image/png", 1024, 100, 100);
        typeof(AssetContent).GetProperty("Id")?.SetValue(existingContent, Guid.NewGuid());

        _contentRepositoryMock
            .Setup(x => x.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContent);

        var reference = new AssetReference(existingContent.Id, userId, "test.png", AssetAccessPolicy.Public, null, null);
        typeof(AssetReference).GetProperty("Id")?.SetValue(reference, Guid.NewGuid());
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.UploadAsync(
            stream, "test.png", "image/png", userId, options);

        // Assert
        result.Success.Should().BeTrue();
        result.AssetContentId.Should().Be(existingContent.Id);

        // Verify deduplication
        _storageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _contentRepositoryMock.Verify(
            x => x.IncrementReferenceCountAsync(existingContent.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_NewContent_UploadsToStorage()
    {
        // Arrange
        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var operationAccessor = new UseCaseOperationContextAccessor();
        using var operation = operationAccessor.Begin(new UseCaseOperationContext(
            "assets.upload-asset",
            nameof(UploadAssetCommand),
            tenantId,
            userId,
            correlationId,
            null));
        var options = new UploadAssetOptions("test.png", AssetAccessPolicy.Private, null, null);

        _contentRepositoryMock
            .Setup(x => x.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        _storageServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                "image/png",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadResult("test-bucket", "uploads/test.png"));

        var newContent = new AssetContent("test-bucket", "uploads/test.png", "newhash", "image/png", 1024, null, null);
        typeof(AssetContent).GetProperty("Id")?.SetValue(newContent, Guid.NewGuid());
        _contentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newContent);

        AssetReference? reference = null;
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .Callback<AssetReference, CancellationToken>((candidate, _) => reference = candidate)
            .ReturnsAsync((AssetReference candidate, CancellationToken _) => candidate);

        // Act
        var service = new AssetUploadService(
            _contentRepositoryMock.Object,
            _referenceRepositoryMock.Object,
            _storageServiceMock.Object,
            Options.Create(_config),
            _loggerMock.Object,
            operationAccessor);

        var result = await service.UploadAsync(
            stream, "test.png", "image/png", userId, options);

        // Assert
        result.Success.Should().BeTrue();
        reference.Should().NotBeNull();
        result.AssetReferenceId.Should().Be(reference!.Id);
        result.AssetContentId.Should().Be(newContent.Id);
        reference.IntegrationEvents.Should().ContainSingle(candidate =>
            candidate is AssetReferenceCreatedEvent && candidate.CorrelationId == correlationId);

        _storageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/png", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WithParentResource_CreatesReferenceWithParent()
    {
        // Arrange
        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var userId = Guid.NewGuid();
        var parentType = "Course";
        var parentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var options = new UploadAssetOptions("test.png", AssetAccessPolicy.Inherited, parentType, parentId, TenantId: tenantId);

        _contentRepositoryMock
            .Setup(x => x.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        _storageServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadResult("bucket", "key"));

        var newContent = new AssetContent("bucket", "key", "hash", "image/png", 1024, null, null);
        typeof(AssetContent).GetProperty("Id")?.SetValue(newContent, Guid.NewGuid());
        _contentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newContent);

        AssetReference? capturedReference = null;
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .Callback<AssetReference, CancellationToken>((r, _) => capturedReference = r)
            .ReturnsAsync((AssetReference r, CancellationToken _) =>
            {
                typeof(AssetReference).GetProperty("Id")?.SetValue(r, Guid.NewGuid());
                return r;
            });

        // Act
        var result = await _service.UploadAsync(
            stream, "test.png", "image/png", userId, options);

        // Assert
        result.Success.Should().BeTrue();
        capturedReference.Should().NotBeNull();
        capturedReference!.ParentResourceType.Should().Be(parentType);
        capturedReference.ParentResourceId.Should().Be(parentId);
        capturedReference.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task UploadAsync_UsesDisplayNameFromOptions()
    {
        // Arrange
        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var userId = Guid.NewGuid();
        var displayName = "My Custom Display Name";
        var options = new UploadAssetOptions(displayName, AssetAccessPolicy.Public, null, null);

        _contentRepositoryMock
            .Setup(x => x.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        _storageServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadResult("bucket", "key"));

        var newContent = new AssetContent("bucket", "key", "hash", "image/png", 1024, null, null);
        typeof(AssetContent).GetProperty("Id")?.SetValue(newContent, Guid.NewGuid());
        _contentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newContent);

        AssetReference? capturedReference = null;
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .Callback<AssetReference, CancellationToken>((r, _) => capturedReference = r)
            .ReturnsAsync((AssetReference r, CancellationToken _) =>
            {
                typeof(AssetReference).GetProperty("Id")?.SetValue(r, Guid.NewGuid());
                return r;
            });

        // Act
        await _service.UploadAsync(
            stream, "original-filename.png", "image/png", userId, options);

        // Assert
        capturedReference.Should().NotBeNull();
        capturedReference!.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public async Task UploadAsync_NullDisplayName_UsesFileName()
    {
        // Arrange
        var content = new byte[1024];
        using var stream = new MemoryStream(content);
        var userId = Guid.NewGuid();
        var fileName = "my-image.png";
        var options = new UploadAssetOptions(null, AssetAccessPolicy.Public, null, null);

        _contentRepositoryMock
            .Setup(x => x.GetByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        _storageServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadResult("bucket", "key"));

        var newContent = new AssetContent("bucket", "key", "hash", "image/png", 1024, null, null);
        typeof(AssetContent).GetProperty("Id")?.SetValue(newContent, Guid.NewGuid());
        _contentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newContent);

        AssetReference? capturedReference = null;
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .Callback<AssetReference, CancellationToken>((r, _) => capturedReference = r)
            .ReturnsAsync((AssetReference r, CancellationToken _) =>
            {
                typeof(AssetReference).GetProperty("Id")?.SetValue(r, Guid.NewGuid());
                return r;
            });

        // Act
        await _service.UploadAsync(
            stream, fileName, "image/png", userId, options);

        // Assert
        capturedReference.Should().NotBeNull();
        capturedReference!.DisplayName.Should().Be(fileName);
    }

    #endregion

    #region InitiateChunkedUploadAsync Tests

    [Fact]
    public async Task InitiateChunkedUploadAsync_ReturnsSession()
    {
        // Arrange
        var fileName = "large-file.zip";
        var mimeType = "application/zip";
        var totalSize = 50 * 1024 * 1024L; // 50 MB
        var userId = Guid.NewGuid();
        var uploadId = "multipart-upload-id";

        _storageServiceMock
            .Setup(x => x.InitiateMultipartUploadAsync(mimeType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadId);

        // Act
        var session = await _service.InitiateChunkedUploadAsync(
            fileName, mimeType, totalSize, userId);

        // Assert
        session.Should().NotBeNull();
        session.UploadId.Should().Be(uploadId);
        session.UserId.Should().Be(userId);
        session.FileName.Should().Be(fileName);
        session.MimeType.Should().Be(mimeType);
        session.TotalSize.Should().Be(totalSize);
        session.TotalChunks.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InitiateChunkedUploadAsync_CalculatesCorrectChunkCount()
    {
        // Arrange
        var totalSize = 10 * 1024 * 1024L; // 10 MB
        var expectedChunks = (int)Math.Ceiling((double)totalSize / _config.ChunkSizeBytes);

        _storageServiceMock
            .Setup(x => x.InitiateMultipartUploadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("upload-id");

        // Act
        var session = await _service.InitiateChunkedUploadAsync(
            "file.bin", "application/octet-stream", totalSize, Guid.NewGuid());

        // Assert
        session.TotalChunks.Should().Be(expectedChunks);
    }

    #endregion

    #region Chunked upload completion tests

    [Fact]
    public async Task CompleteChunkedUploadAsync_UsesTrackedPartEtagsInPartOrder()
    {
        var uploadId = $"upload-{Guid.NewGuid():N}";
        var userId = Guid.NewGuid();
        var totalSize = 2L * _config.ChunkSizeBytes;
        IReadOnlyList<string>? capturedPartETags = null;

        _storageServiceMock
            .Setup(x => x.InitiateMultipartUploadAsync("image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadId);
        _storageServiceMock
            .Setup(x => x.UploadPartAsync(
                uploadId,
                $"multipart/{uploadId}",
                It.IsAny<int>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int partNumber, Stream _, CancellationToken _) => $"etag-{partNumber}");
        _storageServiceMock
            .Setup(x => x.CompleteMultipartUploadAsync(
                uploadId,
                $"multipart/{uploadId}",
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, IReadOnlyList<string>, CancellationToken>((_, _, partETags, _) => capturedPartETags = partETags)
            .ReturnsAsync(new StorageUploadResult("bucket", "multipart/object"));
        _storageServiceMock
            .Setup(x => x.DownloadAsync("bucket", "multipart/object", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3, 4]));

        var assetContent = new AssetContent("bucket", "multipart/object", "hash", "image/png", totalSize, null, null);
        typeof(AssetContent).GetProperty("Id")?.SetValue(assetContent, Guid.NewGuid());
        _contentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assetContent);

        var reference = new AssetReference(assetContent.Id, userId, "file.png", AssetAccessPolicy.Private, null, null);
        typeof(AssetReference).GetProperty("Id")?.SetValue(reference, Guid.NewGuid());
        _referenceRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        await _service.InitiateChunkedUploadAsync("file.png", "image/png", totalSize, userId);
        (await _service.UploadChunkAsync(uploadId, 1, new MemoryStream([2]))).Should().BeTrue();
        (await _service.UploadChunkAsync(uploadId, 0, new MemoryStream([1]))).Should().BeTrue();

        var result = await _service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("file.png", AssetAccessPolicy.Private));

        result.Success.Should().BeTrue();
        capturedPartETags.Should().Equal("etag-1", "etag-2");
    }

    [Fact]
    public async Task CompleteChunkedUploadAsync_ReturnsError_WhenChunksAreMissing()
    {
        var uploadId = $"upload-{Guid.NewGuid():N}";
        var userId = Guid.NewGuid();
        var totalSize = 2L * _config.ChunkSizeBytes;

        _storageServiceMock
            .Setup(x => x.InitiateMultipartUploadAsync("image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadId);
        _storageServiceMock
            .Setup(x => x.UploadPartAsync(
                uploadId,
                $"multipart/{uploadId}",
                1,
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("etag-1");

        await _service.InitiateChunkedUploadAsync("file.png", "image/png", totalSize, userId);
        (await _service.UploadChunkAsync(uploadId, 0, new MemoryStream([1]))).Should().BeTrue();

        var result = await _service.CompleteChunkedUploadAsync(
            uploadId,
            new UploadAssetOptions("file.png", AssetAccessPolicy.Private));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Missing chunks: 2");
        _storageServiceMock.Verify(
            x => x.CompleteMultipartUploadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
