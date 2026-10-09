using Microsoft.Extensions.Logging;

namespace GameGuild.Assets.UnitTests.Services;

public class AssetModerationServiceTests
{
    private readonly Mock<IAssetContentRepository> _contentRepositoryMock;
    private readonly Mock<IAssetReportRepository> _reportRepositoryMock;
    private readonly AssetModerationService _service;

    public AssetModerationServiceTests()
    {
        _contentRepositoryMock = new Mock<IAssetContentRepository>();
        _reportRepositoryMock = new Mock<IAssetReportRepository>();
        Mock<ILogger<AssetModerationService>> loggerMock = new Mock<ILogger<AssetModerationService>>();

        _service = new AssetModerationService(
            _contentRepositoryMock.Object,
            _reportRepositoryMock.Object,
            loggerMock.Object);
    }

    #region ModerateAsync Tests

    [Fact]
    public async Task ModerateAsync_AssetNotFound_ReturnsPendingFalse()
    {
        // Arrange
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[100]);

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        // Act
        var result = await _service.ModerateAsync(assetContentId, stream, "image/png");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Status.Should().Be(ModerationStatus.Pending);
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ModerateAsync_ImageContent_ReturnsApproved()
    {
        // Arrange
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A]);
        var content = CreateAssetContent(assetContentId, "image/png");

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _service.ModerateAsync(assetContentId, stream, "image/png");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Status.Should().Be(ModerationStatus.Approved);
        result.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task ModerateAsync_NonImageContent_ReturnsApproved()
    {
        // Arrange
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream("%PDF-1.7\ncontent"u8.ToArray());
        var content = CreateAssetContent(assetContentId, "application/pdf");

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _service.ModerateAsync(assetContentId, stream, "application/pdf");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Status.Should().Be(ModerationStatus.Approved);
    }

    [Fact]
    public async Task ModerateAsync_ImageHeaderMismatch_ReturnsNeedsReview()
    {
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream("not a png"u8.ToArray());
        var content = CreateAssetContent(assetContentId, "image/png");

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _service.ModerateAsync(assetContentId, stream, "image/png");

        result.IsApproved.Should().BeFalse();
        result.Status.Should().Be(ModerationStatus.NeedsReview);
        result.DetectedIssue.Should().Contain("Image header");
    }

    [Fact]
    public async Task ModerateAsync_ExecutableSignature_ReturnsBlocked()
    {
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream([(byte)'M', (byte)'Z', 0x90, 0x00]);
        var content = CreateAssetContent(assetContentId, "application/octet-stream");

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _service.ModerateAsync(assetContentId, stream, "application/octet-stream");

        result.IsApproved.Should().BeFalse();
        result.Status.Should().Be(ModerationStatus.Blocked);
        result.DetectedIssue.Should().Contain("Executable");
    }

    [Fact]
    public async Task ModerateAsync_UpdatesContentStatus()
    {
        // Arrange
        var assetContentId = Guid.NewGuid();
        using var stream = new MemoryStream([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A]);
        var content = CreateAssetContent(assetContentId, "image/png");

        _contentRepositoryMock
            .Setup(x => x.GetByIdAsync(assetContentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        await _service.ModerateAsync(assetContentId, stream, "image/png");

        // Assert
        _contentRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetPendingReportsAsync Tests

    [Fact]
    public async Task GetPendingReportsAsync_ReturnsReports()
    {
        // Arrange
        var reports = new List<AssetReport>
        {
            CreateAssetReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReportReason.Inappropriate),
            CreateAssetReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReportReason.Spam)
        };

        _reportRepositoryMock
            .Setup(x => x.GetPendingReportsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        // Act
        var result = await _service.GetPendingReportsAsync(100);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPendingReportsAsync_RespectsLimit()
    {
        // Arrange
        var limit = 50;

        // Act
        await _service.GetPendingReportsAsync(limit);

        // Assert
        _reportRepositoryMock.Verify(
            x => x.GetPendingReportsAsync(limit, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SubmitReviewAsync Tests

    [Fact]
    public async Task SubmitReviewAsync_ReportNotFound_ReturnsFalse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        _reportRepositoryMock
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReport?)null);

        // Act
        var result = await _service.SubmitReviewAsync(reportId, reviewerId, ReviewDecision.NoAction);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitReviewAsync_ValidReport_ReturnsTrue()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var report = CreateAssetReport(reportId, Guid.NewGuid(), Guid.NewGuid(), ReportReason.Inappropriate);

        _reportRepositoryMock
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        var result = await _service.SubmitReviewAsync(reportId, reviewerId, ReviewDecision.NoAction);

        // Assert
        result.Should().BeTrue();
        _reportRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<AssetReport>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitReviewAsync_BlockContent_UpdatesContentStatus()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        
        // Create a report with reference and content attached
        var content = CreateAssetContent(contentId, "image/png");
        var reference = CreateAssetReference(referenceId, contentId);
        typeof(AssetReference).GetProperty("Content")?.SetValue(reference, content);
        
        var report = CreateAssetReport(reportId, referenceId, Guid.NewGuid(), ReportReason.Inappropriate);
        typeof(AssetReport).GetProperty("Reference")?.SetValue(report, reference);

        _reportRepositoryMock
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        var result = await _service.SubmitReviewAsync(reportId, reviewerId, ReviewDecision.BlockContent);

        // Assert
        result.Should().BeTrue();
        _contentRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitReviewAsync_WithNotes_UpdatesReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var notes = "Reviewed and found no issue";
        var report = CreateAssetReport(reportId, Guid.NewGuid(), Guid.NewGuid(), ReportReason.Spam);

        _reportRepositoryMock
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        var result = await _service.SubmitReviewAsync(reportId, reviewerId, ReviewDecision.NoAction, notes);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CreateReportAsync Tests

    [Fact]
    public async Task CreateReportAsync_UserAlreadyReported_ReturnsNull()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reportedByUserId = Guid.NewGuid();

        _reportRepositoryMock
            .Setup(x => x.HasUserReportedAsync(assetReferenceId, reportedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CreateReportAsync(assetReferenceId, reportedByUserId, ReportReason.Spam);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateReportAsync_NewReport_CreatesAndReturns()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reportedByUserId = Guid.NewGuid();
        var reason = ReportReason.Violence;

        _reportRepositoryMock
            .Setup(x => x.HasUserReportedAsync(assetReferenceId, reportedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reportRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetReport r, CancellationToken _) =>
            {
                typeof(AssetReport).GetProperty("Id")?.SetValue(r, Guid.NewGuid());
                return r;
            });

        // Act
        var result = await _service.CreateReportAsync(assetReferenceId, reportedByUserId, reason, "Description");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReportAsync_WithDescription_IncludesDescription()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var reportedByUserId = Guid.NewGuid();
        var description = "This content contains inappropriate material";

        _reportRepositoryMock
            .Setup(x => x.HasUserReportedAsync(assetReferenceId, reportedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        AssetReport? capturedReport = null;
        _reportRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<AssetReport>(), It.IsAny<CancellationToken>()))
            .Callback<AssetReport, CancellationToken>((r, _) => capturedReport = r)
            .ReturnsAsync((AssetReport r, CancellationToken _) =>
            {
                typeof(AssetReport).GetProperty("Id")?.SetValue(r, Guid.NewGuid());
                return r;
            });

        // Act
        await _service.CreateReportAsync(assetReferenceId, reportedByUserId, ReportReason.Harassment, description);

        // Assert
        capturedReport.Should().NotBeNull();
        capturedReport!.Details.Should().Be(description);
    }

    #endregion

    #region Helper Methods

    private static AssetContent CreateAssetContent(Guid id, string mimeType)
    {
        var content = new AssetContent(
            "bucket",
            "key/object.png",
            "contenthash",
            mimeType,
            1024,
            100,
            100);
        typeof(AssetContent).GetProperty("Id")?.SetValue(content, id);
        return content;
    }

    private static AssetReference CreateAssetReference(Guid id, Guid contentId)
    {
        var reference = new AssetReference(
            contentId,
            Guid.NewGuid(),
            "Test Asset",
            AssetAccessPolicy.Public,
            null,
            null);
        typeof(AssetReference).GetProperty("Id")?.SetValue(reference, id);
        return reference;
    }

    private static AssetReport CreateAssetReport(Guid id, Guid referenceId, Guid reporterId, ReportReason reason)
    {
        var report = new AssetReport(referenceId, reporterId, reason, null);
        typeof(AssetReport).GetProperty("Id")?.SetValue(report, id);
        return report;
    }

    #endregion
}
