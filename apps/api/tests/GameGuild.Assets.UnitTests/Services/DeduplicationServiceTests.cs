using FluentAssertions;
using GameGuild.Assets.Deduplication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Assets.UnitTests.Services;

public class DeduplicationServiceTests
{
    private readonly Mock<IAssetContentRepository> _contentRepositoryMock;
    private readonly DeduplicationOptions _options;
    private readonly DeduplicationService _service;

    public DeduplicationServiceTests()
    {
        _contentRepositoryMock = new Mock<IAssetContentRepository>();
        Mock<ILogger<DeduplicationService>> loggerMock = new Mock<ILogger<DeduplicationService>>();
        _options = new DeduplicationOptions { Enabled = true, EnablePerceptualHashing = true };
        var optionsMock = Options.Create(_options);
        _service = new DeduplicationService(_contentRepositoryMock.Object, optionsMock, loggerMock.Object);
    }

    [Fact]
    public async Task ComputeContentHashAsync_ComputesSHA256Hash()
    {
        // Arrange
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        // Act
        var hash = await _service.ComputeContentHashAsync(content);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64); // SHA-256 produces 64 hex characters
        hash.Should().MatchRegex("^[a-f0-9]+$"); // Only lowercase hex
    }

    [Fact]
    public async Task ComputeContentHashAsync_ResetsStreamPosition()
    {
        // Arrange
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        // Act
        await _service.ComputeContentHashAsync(content);

        // Assert
        content.Position.Should().Be(0);
    }

    [Fact]
    public async Task ComputeContentHashAsync_ProducesSameHashForSameContent()
    {
        // Arrange
        var content1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("identical content"));
        var content2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("identical content"));

        // Act
        var hash1 = await _service.ComputeContentHashAsync(content1);
        var hash2 = await _service.ComputeContentHashAsync(content2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task ComputeContentHashAsync_ProducesDifferentHashForDifferentContent()
    {
        // Arrange
        var content1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content one"));
        var content2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content two"));

        // Act
        var hash1 = await _service.ComputeContentHashAsync(content1);
        var hash2 = await _service.ComputeContentHashAsync(content2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_ReturnsNull_WhenDisabled()
    {
        // Arrange
        _options.EnablePerceptualHashing = false;
        var content = new MemoryStream();

        // Act
        var hash = await _service.ComputePerceptualHashAsync(content, "image/png");

        // Assert
        hash.Should().BeNull();
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_ReturnsNull_ForNonImageMimeType()
    {
        // Arrange
        var content = new MemoryStream();

        // Act
        var hash = await _service.ComputePerceptualHashAsync(content, "application/pdf");

        // Assert
        hash.Should().BeNull();
    }

    [Fact]
    public async Task FindExistingContentAsync_ReturnsNull_WhenNoMatchFound()
    {
        // Arrange
        var contentHash = "abc123";
        _contentRepositoryMock.Setup(r => r.GetByContentHashAsync(contentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        // Act
        var result = await _service.FindExistingContentAsync(contentHash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindExistingContentAsync_ReturnsAssetId_WhenMatchFound()
    {
        // Arrange
        var contentHash = "abc123";
        var assetContent = new AssetContent(
            "test-bucket",
            "test/key.jpg",
            contentHash,
            "image/jpeg",
            1024,
            800,
            600);
        
        _contentRepositoryMock.Setup(r => r.GetByContentHashAsync(contentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assetContent);

        // Act
        var result = await _service.FindExistingContentAsync(contentHash);

        // Assert
        result.Should().Be(assetContent.Id);
    }
}
