using GameGuild.Assets.BackgroundServices;
using GameGuild.Assets.VirusScan;
using Microsoft.Extensions.Logging;

namespace GameGuild.Assets.UnitTests;

public sealed class AssetVirusScanProcessorTests
{
    private readonly Mock<IAssetContentRepository> _repository = new();
    private readonly Mock<IAssetStorageService> _storage = new();
    private readonly Mock<IVirusScanService> _scanner = new();
    private readonly AssetVirusScanProcessor _processor;

    public AssetVirusScanProcessorTests()
    {
        _processor = new AssetVirusScanProcessor(
            _repository.Object,
            _storage.Object,
            _scanner.Object,
            Mock.Of<ILogger<AssetVirusScanProcessor>>());
    }

    [Fact]
    public async Task ProcessPendingAsync_CleanContent_CompletesScan()
    {
        var content = CreateContent();
        SetupCandidate(content);
        _storage.Setup(service => service.DownloadAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        _scanner.Setup(service => service.ScanAsync(It.IsAny<Stream>(), content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirusScanResult(true, "Clean"));

        var processed = await _processor.ProcessPendingAsync(10);

        processed.Should().Be(1);
        content.VirusScanStatus.Should().Be(VirusScanStatus.Clean);
        _repository.Verify(service => service.UpdateAsync(content, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_Malware_MarksContentInfected()
    {
        var content = CreateContent();
        SetupCandidate(content);
        _storage.Setup(service => service.DownloadAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        _scanner.Setup(service => service.ScanAsync(It.IsAny<Stream>(), content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirusScanResult(false, "Malware signature detected", "EICAR", "Virus"));

        await _processor.ProcessPendingAsync(10);

        content.VirusScanStatus.Should().Be(VirusScanStatus.Infected);
    }

    [Fact]
    public async Task ProcessPendingAsync_ScannerUnavailable_MarksScanFailed()
    {
        var content = CreateContent();
        SetupCandidate(content);
        _storage.Setup(service => service.DownloadAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        _scanner.Setup(service => service.ScanAsync(It.IsAny<Stream>(), content.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirusScanResult(false, "Scanner unavailable", "SCANNER_UNAVAILABLE", "Configuration"));

        await _processor.ProcessPendingAsync(10);

        content.VirusScanStatus.Should().Be(VirusScanStatus.ScanFailed);
    }

    [Fact]
    public async Task ProcessPendingAsync_UnclaimedContent_IsSkipped()
    {
        var content = CreateContent();
        _repository.Setup(service => service.GetPendingVirusScanAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);
        _repository.Setup(service => service.TryBeginVirusScanAsync(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processed = await _processor.ProcessPendingAsync(10);

        processed.Should().Be(0);
        _storage.Verify(
            service => service.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            service => service.UpdateAsync(It.IsAny<AssetContent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPendingAsync_DownloadFailure_MarksScanFailed()
    {
        var content = CreateContent();
        SetupCandidate(content);
        _storage.Setup(service => service.DownloadAsync(content.BucketName, content.ObjectKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage unavailable"));

        var processed = await _processor.ProcessPendingAsync(10);

        processed.Should().Be(1);
        content.VirusScanStatus.Should().Be(VirusScanStatus.ScanFailed);
    }

    private void SetupCandidate(AssetContent content)
    {
        _repository.Setup(service => service.GetPendingVirusScanAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);
        _repository.Setup(service => service.TryBeginVirusScanAsync(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static AssetContent CreateContent()
    {
        return new AssetContent("assets", "objects/test.bin", new string('a', 64), "application/octet-stream", 3, null, null);
    }
}
