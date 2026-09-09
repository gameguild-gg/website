using GameGuild.Assets.Commands;
using GameGuild.Assets.Queries;

namespace GameGuild.Assets.UnitTests;

public sealed class M3DocumentCloseoutTests
{
    [Fact]
    public async Task GetAssetPreviewHandler_Image_ReturnsContentAndThumbnailUrls()
    {
        var assetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateReference(assetId, "image/png", AssetKind.Image);
        var referenceRepo = new Mock<IAssetReferenceRepository>();
        var access = new Mock<IAssetAccessService>();
        var extraction = new Mock<IAssetTextExtractionService>();

        referenceRepo.Setup(repo => repo.GetByIdWithContentAsync(assetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        access.Setup(service => service.ValidateAccessAsync(assetId, userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(true, null));
        access.Setup(service => service.GenerateAccessUrlAsync(assetId, userId, tenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessUrl("https://assets.local/original", "token", DateTimeOffset.UtcNow.AddHours(1), "image/png"));
        access.Setup(service => service.GenerateAccessUrlAsync(assetId, userId, tenantId, It.Is<TransformationSpec?>(spec => spec != null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessUrl("https://assets.local/thumb.webp", "thumb-token", DateTimeOffset.UtcNow.AddHours(1), "image/webp"));

        var handler = new GetAssetPreviewHandler(referenceRepo.Object, access.Object, extraction.Object);

        var result = await handler.Handle(new GetAssetPreviewQuery(assetId, userId, tenantId));

        result.Should().NotBeNull();
        result!.PreviewMode.Should().Be("image");
        result.CanInlinePreview.Should().BeTrue();
        result.ContentUrl.Should().Be("https://assets.local/original");
        result.ThumbnailUrl.Should().Be("https://assets.local/thumb.webp");
        extraction.Verify(service => service.ExtractAsync(It.IsAny<AssetReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAssetPreviewHandler_Text_ReturnsExtractedSnippet()
    {
        var assetId = Guid.NewGuid();
        var reference = CreateReference(assetId, "text/plain", AssetKind.Other);
        var referenceRepo = new Mock<IAssetReferenceRepository>();
        var access = new Mock<IAssetAccessService>();
        var extraction = new Mock<IAssetTextExtractionService>();

        referenceRepo.Setup(repo => repo.GetByIdWithContentAsync(assetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        access.Setup(service => service.ValidateAccessAsync(assetId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(true, null));
        access.Setup(service => service.GenerateAccessUrlAsync(assetId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessUrl("https://assets.local/readme.txt", "token", DateTimeOffset.UtcNow.AddHours(1), "text/plain"));
        extraction.Setup(service => service.ExtractAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedAssetTextResult("abcdef", "text/plain", "direct", false, false, []));

        var handler = new GetAssetPreviewHandler(referenceRepo.Object, access.Object, extraction.Object);

        var result = await handler.Handle(new GetAssetPreviewQuery(assetId, null, null, TextPreviewLength: 3));

        result.Should().NotBeNull();
        result!.PreviewMode.Should().Be("text");
        result.ExtractedTextPreview.Should().Be("abc");
        result.IsTextTruncated.Should().BeTrue();
    }

    [Fact]
    public async Task GetAssetPreviewHandler_BlockedContent_ReturnsBlockedPreviewWithoutUrls()
    {
        var assetId = Guid.NewGuid();
        var reference = CreateReference(assetId, "application/pdf", AssetKind.Document);
        reference.Content.SetVirusScanStatus(VirusScanStatus.Infected);
        var referenceRepo = new Mock<IAssetReferenceRepository>();
        var access = new Mock<IAssetAccessService>();
        var extraction = new Mock<IAssetTextExtractionService>();

        referenceRepo.Setup(repo => repo.GetByIdWithContentAsync(assetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        access.Setup(service => service.ValidateAccessAsync(assetId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(true, null));

        var handler = new GetAssetPreviewHandler(referenceRepo.Object, access.Object, extraction.Object);

        var result = await handler.Handle(new GetAssetPreviewQuery(assetId, null, null));

        result.Should().NotBeNull();
        result!.IsBlocked.Should().BeTrue();
        result.PreviewMode.Should().Be("blocked");
        result.ContentUrl.Should().BeNull();
        access.Verify(service => service.GenerateAccessUrlAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<TransformationSpec?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BulkGenerateAssetAccessUrlsHandler_ReturnsPerItemSuccessAndFailure()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var access = new Mock<IAssetAccessService>();
        access.Setup(service => service.GenerateAccessUrlAsync(first, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessUrl("https://assets.local/one", "one", DateTimeOffset.UtcNow.AddHours(1), "image/png"));
        access.Setup(service => service.GenerateAccessUrlAsync(second, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetAccessUrl?)null);

        var handler = new BulkGenerateAssetAccessUrlsHandler(access.Object);

        var result = await handler.Handle(new BulkGenerateAssetAccessUrlsQuery([first, second], null, null));

        result.TotalRequested.Should().Be(2);
        result.Successful.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Items.Single(item => item.AssetReferenceId == first).Url.Should().Be("https://assets.local/one");
        result.Items.Single(item => item.AssetReferenceId == second).Error.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkDeleteAssetsHandler_DeletesOwnedAssetsAndReportsFailures()
    {
        var ownedId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        var referenceRepo = new Mock<IAssetReferenceRepository>();
        var contentRepo = new Mock<IAssetContentRepository>();
        var reference = CreateReference(ownedId, "application/pdf", AssetKind.Document, contentId, userId);
        var content = reference.Content;
        content.MarkAsDeletable();

        referenceRepo.Setup(repo => repo.GetByIdAsync(ownedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        referenceRepo.Setup(repo => repo.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReference?)null);
        referenceRepo.Setup(repo => repo.IsOwnedByUserAsync(ownedId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        contentRepo.Setup(repo => repo.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var handler = new BulkDeleteAssetsHandler(referenceRepo.Object, contentRepo.Object);

        var result = await handler.Handle(new BulkDeleteAssetsCommand([ownedId, missingId], userId));

        result.Successful.Should().Be(1);
        result.Failed.Should().Be(1);
        referenceRepo.Verify(repo => repo.DeleteAsync(ownedId, It.IsAny<CancellationToken>()), Times.Once);
        contentRepo.Verify(repo => repo.DecrementReferenceCountAsync(contentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUploadAssetsHandler_ReturnsPerFileResults()
    {
        var upload = new Mock<ISecureUploadService>();
        var successId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        upload.Setup(service => service.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                "ok.pdf",
                "application/pdf",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecureUploadResult(true, successId, contentId, null));
        upload.Setup(service => service.UploadWithSecurityChecksAsync(
                It.IsAny<Stream>(),
                "bad.pdf",
                "application/pdf",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UploadAssetOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecureUploadResult(false, null, null, "Rejected"));

        var authorization = new Mock<IAssetUploadAuthorizationService>();
        authorization.Setup(service => service.CanUploadAsync(
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new BulkUploadAssetsHandler(upload.Object, authorization.Object);

        var result = await handler.Handle(new BulkUploadAssetsCommand(
            [
                new BulkUploadAssetInput(new MemoryStream([1]), "ok.pdf", "application/pdf"),
                new BulkUploadAssetInput(new MemoryStream([2]), "bad.pdf", "application/pdf")
            ],
            Guid.NewGuid(),
            Guid.NewGuid()));

        result.Successful.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Items.Single(item => item.FileName == "ok.pdf").AssetReferenceId.Should().Be(successId);
        result.Items.Single(item => item.FileName == "bad.pdf").Error.Should().Be("Rejected");
    }

    [Fact]
    public async Task BulkUploadAssetsHandler_MissingTenant_FailsAllFiles()
    {
        var upload = new Mock<ISecureUploadService>();
        var authorization = new Mock<IAssetUploadAuthorizationService>();
        authorization.Setup(service => service.CanUploadAsync(
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new BulkUploadAssetsHandler(upload.Object, authorization.Object);

        var result = await handler.Handle(new BulkUploadAssetsCommand(
            [new BulkUploadAssetInput(new MemoryStream([1]), "tenantless.pdf", "application/pdf")],
            Guid.NewGuid(),
            null));

        result.Successful.Should().Be(0);
        result.Failed.Should().Be(1);
        result.Items.Single().Error.Should().Be("Tenant context is required");
        upload.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAssetRetentionHandler_DryRunDoesNotDelete()
    {
        var content = CreateContent(Guid.NewGuid(), "application/pdf", AssetKind.Document);
        var contentRepo = new Mock<IAssetContentRepository>();
        var storage = new Mock<IAssetStorageService>();
        var transformed = new Mock<ITransformedAssetRepository>();
        contentRepo.Setup(repo => repo.GetGarbageCollectionCandidatesAsync(
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);

        var handler = new RunAssetRetentionHandler(contentRepo.Object, storage.Object, transformed.Object);

        var result = await handler.Handle(new RunAssetRetentionCommand(DryRun: true));

        result.CandidatesFound.Should().Be(1);
        result.Deleted.Should().Be(0);
        storage.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAssetRetentionHandler_DeletesStorageAndContent()
    {
        var content = CreateContent(Guid.NewGuid(), "application/pdf", AssetKind.Document);
        var contentRepo = new Mock<IAssetContentRepository>();
        var storage = new Mock<IAssetStorageService>();
        var transformed = new Mock<ITransformedAssetRepository>();
        contentRepo.Setup(repo => repo.GetGarbageCollectionCandidatesAsync(
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);

        var handler = new RunAssetRetentionHandler(contentRepo.Object, storage.Object, transformed.Object);

        var result = await handler.Handle(new RunAssetRetentionCommand());

        result.Deleted.Should().Be(1);
        transformed.Verify(repo => repo.DeleteBySourceAsync(content.Id, It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(service => service.DeleteAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);
        contentRepo.Verify(repo => repo.DeleteAsync(content.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAssetLegalHoldHandler_TogglesHoldState()
    {
        var contentId = Guid.NewGuid();
        var content = CreateContent(contentId, "application/pdf", AssetKind.Document);
        var contentRepo = new Mock<IAssetContentRepository>();
        contentRepo.Setup(repo => repo.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var handler = new SetAssetLegalHoldHandler(contentRepo.Object);

        var enabled = await handler.Handle(new SetAssetLegalHoldCommand(contentId, true, "Litigation"));
        var disabled = await handler.Handle(new SetAssetLegalHoldCommand(contentId, false));

        enabled.Should().NotBeNull();
        enabled!.LegalHoldEnabled.Should().BeTrue();
        disabled.Should().NotBeNull();
        disabled!.LegalHoldEnabled.Should().BeFalse();
        contentRepo.Verify(repo => repo.UpdateAsync(content, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static AssetReference CreateReference(
        Guid id,
        string mimeType,
        AssetKind kind,
        Guid? contentId = null,
        Guid? ownerId = null)
    {
        var finalContentId = contentId ?? Guid.NewGuid();
        var reference = new AssetReference(
            finalContentId,
            ownerId ?? Guid.NewGuid(),
            "Document",
            AssetAccessPolicy.Public,
            "Document",
            Guid.NewGuid());
        var content = CreateContent(finalContentId, mimeType, kind);
        typeof(AssetReference).GetProperty(nameof(AssetReference.Id))?.SetValue(reference, id);
        reference.Content = content;
        return reference;
    }

    private static AssetContent CreateContent(Guid id, string mimeType, AssetKind expectedKind)
    {
        var content = new AssetContent(
            "assets",
            $"documents/{id:N}",
            id.ToString("N"),
            mimeType,
            1024,
            expectedKind == AssetKind.Image ? 640 : null,
            expectedKind == AssetKind.Image ? 480 : null);
        typeof(AssetContent).GetProperty(nameof(AssetContent.Id))?.SetValue(content, id);
        content.SetVirusScanStatus(VirusScanStatus.Clean);
        content.SetModerationStatus(ModerationStatus.Approved);
        content.Kind.Should().Be(expectedKind);
        return content;
    }
}
