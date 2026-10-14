using GameGuild.Assets.BackgroundServices;
using GameGuild.Assets.Moderation;
using GameGuild.Assets.Openinary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.UnitTests.Moderation;

public sealed class ModerationClassificationProcessorTests
{
    private readonly Mock<IAssetContentRepository> _repository = new();
    private readonly Mock<IAssetModerationClassifier> _classifier = new();
    private readonly Mock<IAssetStorageService> _storage = new();
    private readonly Mock<IOpeninaryClient> _openinary = new();
    private readonly Mock<IAssetAccessService> _access = new();
    private readonly AutoModerationOptions _options = new()
    {
        AutoApprovalThreshold = 0.80,
        AutoRejectionThreshold = 0.75,
        MaxConsecutiveFailures = 3
    };
    private readonly ModerationClassificationProcessor _processor;

    public ModerationClassificationProcessorTests()
    {
        _processor = new ModerationClassificationProcessor(
            _repository.Object,
            _classifier.Object,
            _storage.Object,
            _openinary.Object,
            _access.Object,
            Microsoft.Extensions.Options.Options.Create(_options),
            Mock.Of<ILogger<ModerationClassificationProcessor>>());
    }

    [Fact]
    public async Task CleanImage_LowScores_ApprovesWithLabelsAndPrewarm()
    {
        var content = CreateImageContent();
        SetupCandidate(content);
        SetupDownload(content);
        _classifier.Setup(c => c.ClassifyAsync(content, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetModerationClassification(
                false, [new("safe", 0.10)], "qwen3.8-flash-next"));

        await _processor.ProcessPendingAsync(10);

        content.ModerationStatus.Should().Be(ModerationStatus.Approved);
        content.ModerationLabelsList.Should().Contain("safe");
        content.ModerationReviewNotes.Should().Contain("model=qwen3.8-flash-next");
        _access.Verify(a => a.PrewarmDefaultVariantsAsync(content, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HighAdultScore_RejectsPurgesOpeninaryAndRecordsModel()
    {
        var content = CreateImageContent(openinary: true);
        SetupCandidate(content);
        SetupDownload(content);
        _classifier.Setup(c => c.ClassifyAsync(content, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetModerationClassification(
                false, [new("adult_content", 0.90)], "qwen3.8-flash-next"));

        await _processor.ProcessPendingAsync(10);

        content.ModerationStatus.Should().Be(ModerationStatus.Rejected);
        content.ModerationReviewNotes.Should().Contain("model=qwen3.8-flash-next");
        _openinary.Verify(o => o.DownloadAsync(content.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);
        _openinary.Verify(o => o.DeleteAsync(content.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HighAdultScore_S3Row_PurgesViaStorageService()
    {
        var content = CreateImageContent();
        SetupCandidate(content);
        SetupDownload(content);
        _classifier.Setup(c => c.ClassifyAsync(content, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetModerationClassification(
                false, [new("adult_content", 0.90)], "qwen3.8-flash-next"));

        await _processor.ProcessPendingAsync(10);

        _storage.Verify(s => s.DeleteAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);
        _openinary.Verify(o => o.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClassifierThrows_FailureDoesNotAutoApprove()
    {
        var content = CreateImageContent();
        SetupCandidate(content);
        SetupDownload(content);
        _classifier.Setup(c => c.ClassifyAsync(content, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("proxy down"));

        await _processor.ProcessPendingAsync(10);

        content.ModerationStatus.Should().Be(ModerationStatus.Pending);
        _access.Verify(a => a.PrewarmDefaultVariantsAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClassifierThrows_ThreeTimes_DeadLettersToNeedsReview()
    {
        var content = CreateImageContent();
        SetupCandidate(content);
        SetupDownload(content);
        _classifier.Setup(c => c.ClassifyAsync(content, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("proxy down"));

        await _processor.ProcessPendingAsync(10);
        await _processor.ProcessPendingAsync(10);
        await _processor.ProcessPendingAsync(10);

        content.ModerationStatus.Should().Be(ModerationStatus.NeedsReview);
        content.ModerationLabelsList.Should().Contain("classifier_failure");
    }

    [Fact]
    public async Task VideoContent_ShortCircuitsToNeedsReviewWithManualLabel()
    {
        var content = new AssetContent("assets", "objects/v.mp4", new string('a', 64), "video/mp4", 10, null, null)
        {
            VirusScanStatus = VirusScanStatus.Clean,
            ModerationStatus = ModerationStatus.Pending
        };
        _repository.Setup(r => r.GetPendingModerationQueueAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);

        await _processor.ProcessPendingAsync(10);

        content.ModerationStatus.Should().Be(ModerationStatus.NeedsReview);
        content.ModerationLabelsList.Should().Contain("manual_review_media_type");
        _classifier.Verify(c => c.ClassifyAsync(It.IsAny<AssetContent>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotCleanVirusScan_IsSkipped()
    {
        var content = CreateImageContent();
        content.VirusScanStatus = VirusScanStatus.Pending;
        _repository.Setup(r => r.GetPendingModerationQueueAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);

        var processed = await _processor.ProcessPendingAsync(10);

        processed.Should().Be(0);
        _repository.Verify(r => r.TryBeginModerationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CasClaimFails_ItemSkipped()
    {
        var content = CreateImageContent();
        _repository.Setup(r => r.GetPendingModerationQueueAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);
        _repository.Setup(r => r.TryBeginModerationAsync(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processed = await _processor.ProcessPendingAsync(10);

        processed.Should().Be(0);
        _classifier.Verify(c => c.ClassifyAsync(It.IsAny<AssetContent>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnparseableClassification_RoutesToNeedsReviewNotApproval()
    {
        var content = CreateImageContent();
        SetupCandidate(content);
        SetupDownload(content);
        _classifier.Setup(c => c.ClassifyAsync(content, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ModerationClassificationException("no JSON in response"));

        await _processor.ProcessPendingAsync(10);

        content.ModerationStatus.Should().Be(ModerationStatus.NeedsReview);
        content.ModerationReviewNotes.Should().Contain("classifier_failure");
        _access.Verify(a => a.PrewarmDefaultVariantsAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupCandidate(AssetContent content)
    {
        _repository.Setup(r => r.GetPendingModerationQueueAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);
        _repository.Setup(r => r.TryBeginModerationAsync(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupDownload(AssetContent content)
    {
        _storage.Setup(s => s.DownloadAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        _openinary.Setup(o => o.DownloadAsync(content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
    }

    private static AssetContent CreateImageContent(bool openinary = false) =>
        new(
            openinary ? Openinary.OpeninaryOptions.StorageBackendName : "assets",
            "objects/img.png",
            new string('a', 64),
            "image/png",
            10,
            null,
            null)
        {
            VirusScanStatus = VirusScanStatus.Clean,
            ModerationStatus = ModerationStatus.Pending
        };
}
