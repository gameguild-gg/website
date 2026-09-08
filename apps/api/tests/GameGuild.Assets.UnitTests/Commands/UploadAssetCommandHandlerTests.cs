using GameGuild.Assets.Commands;

namespace GameGuild.Assets.UnitTests.Commands;

public class UploadAssetCommandHandlerTests
{
    private readonly Mock<ISecureUploadService> _uploadServiceMock;
    private readonly Mock<IAssetContentRepository> _contentRepositoryMock;
    private readonly Mock<IAssetUploadAuthorizationService> _authorizationServiceMock;
    private readonly UploadAssetHandler _handler;

    public UploadAssetCommandHandlerTests()
    {
        _uploadServiceMock = new Mock<ISecureUploadService>();
        _contentRepositoryMock = new Mock<IAssetContentRepository>();
        _authorizationServiceMock = new Mock<IAssetUploadAuthorizationService>();
        _authorizationServiceMock.Setup(service => service.CanUploadAsync(
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handler = new UploadAssetHandler(
            _uploadServiceMock.Object,
            _contentRepositoryMock.Object,
            _authorizationServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ParentAuthorizationDenied_DoesNotUpload()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadAssetCommand(
            stream,
            "private-build.zip",
            "application/zip",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ParentResourceType: "Project",
            ParentResourceId: Guid.NewGuid());
        _authorizationServiceMock.Setup(service => service.CanUploadAsync(
                command.ParentResourceType,
                command.ParentResourceId,
                command.FolderId,
                command.UserId,
                command.TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Error.Should().Be("Forbidden");
        _uploadServiceMock.Verify(service => service.UploadWithSecurityChecksAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<UploadAssetOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingTenant_DoesNotUpload()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadAssetCommand(
            stream,
            "private-build.zip",
            "application/zip",
            Guid.NewGuid(),
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Error.Should().Be("Tenant context is required");
        _uploadServiceMock.Verify(service => service.UploadWithSecurityChecksAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<UploadAssetOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UploadSucceeds_ReturnsSuccessResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var assetReferenceId = Guid.NewGuid();
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        
        var command = new UploadAssetCommand(
            stream,
            "test.png",
            "image/png",
            userId,
            tenantId,
            DisplayName: "Test Image",
            AccessPolicy: AssetAccessPolicy.Private);

        var uploadResult = new SecureUploadResult(true, assetReferenceId, assetContentId, null);
        var content = CreateAssetContent(assetContentId, "abc123hash", referenceCount: 1);

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                stream,
                "test.png",
                "image/png",
                userId,
                tenantId,
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AssetReferenceId.Should().Be(assetReferenceId);
        result.AssetContentId.Should().Be(assetContentId);
        result.ContentHash.Should().Be("abc123hash");
        result.WasDeduped.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UploadFails_ReturnsErrorResponse()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadAssetCommand(
            stream,
            "test.png",
            "image/png",
            Guid.NewGuid(),
            Guid.NewGuid());

        var uploadResult = new SecureUploadResult(false, null, null, "Virus detected");

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AssetReferenceId.Should().Be(Guid.Empty);
        result.AssetContentId.Should().Be(Guid.Empty);
        result.Error.Should().Be("Virus detected");
    }

    [Fact]
    public async Task Handle_UploadFailsWithNoError_ReturnsDefaultError()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadAssetCommand(
            stream,
            "test.png",
            "image/png",
            Guid.NewGuid(),
            Guid.NewGuid());

        var uploadResult = new SecureUploadResult(false, null, null, null);

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be("Upload failed");
    }

    [Fact]
    public async Task Handle_ContentDeduped_ReturnsDedupedTrue()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadAssetCommand(
            stream,
            "duplicate.png",
            "image/png",
            Guid.NewGuid(),
            Guid.NewGuid());

        var uploadResult = new SecureUploadResult(true, assetReferenceId, assetContentId, null);
        var content = CreateAssetContent(assetContentId, "existinghash", referenceCount: 5);

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.WasDeduped.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ContentNotFound_ReturnsEmptyHash()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadAssetCommand(
            stream,
            "test.png",
            "image/png",
            Guid.NewGuid(),
            Guid.NewGuid());

        var uploadResult = new SecureUploadResult(true, assetReferenceId, assetContentId, null);

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ContentHash.Should().BeEmpty();
        result.WasDeduped.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PassesCorrectOptions()
    {
        // Arrange
        var parentResourceId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadAssetCommand(
            stream,
            "test.pdf",
            "application/pdf",
            Guid.NewGuid(),
            Guid.NewGuid(),
            DisplayName: "My Document",
            AccessPolicy: AssetAccessPolicy.Public,
            ParentResourceType: "Course",
            ParentResourceId: parentResourceId,
            FolderId: folderId);

        UploadAssetOptions? capturedOptions = null;
        var uploadResult = new SecureUploadResult(true, Guid.NewGuid(), Guid.NewGuid(), null);
        var content = CreateAssetContent(Guid.NewGuid(), "hash", referenceCount: 1);

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, Guid, Guid, UploadAssetOptions, CancellationToken>(
                (_, _, _, _, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(uploadResult);

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.DisplayName.Should().Be("My Document");
        capturedOptions.AccessPolicy.Should().Be(AssetAccessPolicy.Public);
        capturedOptions.ParentResourceType.Should().Be("Course");
        capturedOptions.ParentResourceId.Should().Be(parentResourceId);
        capturedOptions.FolderId.Should().Be(folderId);
        capturedOptions.TenantId.Should().Be(command.TenantId);
    }

    [Fact]
    public async Task Handle_NoDisplayName_UsesFileName()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadAssetCommand(
            stream,
            "original_filename.png",
            "image/png",
            Guid.NewGuid(),
            Guid.NewGuid(),
            DisplayName: null);

        UploadAssetOptions? capturedOptions = null;
        var uploadResult = new SecureUploadResult(true, Guid.NewGuid(), Guid.NewGuid(), null);
        var content = CreateAssetContent(Guid.NewGuid(), "hash", referenceCount: 1);

        _uploadServiceMock
            .Setup(x => x.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, Guid, Guid, UploadAssetOptions, CancellationToken>(
                (_, _, _, _, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(uploadResult);

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.DisplayName.Should().Be("original_filename.png");
    }

    private static AssetContent CreateAssetContent(Guid id, string contentHash, int referenceCount)
    {
        var content = new AssetContent(
            "test-bucket",
            "test/object.png",
            contentHash,
            "image/png",
            1024,
            100,
            100);
        
        typeof(AssetContent).GetProperty("Id")?.SetValue(content, id);
        typeof(AssetContent).GetProperty("ReferenceCount")?.SetValue(content, referenceCount);
        
        return content;
    }
}
