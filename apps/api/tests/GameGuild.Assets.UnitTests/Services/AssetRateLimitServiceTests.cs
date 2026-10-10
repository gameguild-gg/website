using FluentAssertions;
using GameGuild.Assets.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Assets.UnitTests.Services;

public class AssetRateLimitServiceTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly AssetRateLimitOptions _options;
    private readonly AssetRateLimitService _service;

    public AssetRateLimitServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        Mock<ILogger<AssetRateLimitService>> loggerMock = new Mock<ILogger<AssetRateLimitService>>();
        _options = new AssetRateLimitOptions
        {
            Enabled = true,
            MaxAccessPerAssetPerHour = 1000,
            Max403PerIpPerHour = 50,
            BlockDurationMinutes = 60
        };
        var optionsMock = Options.Create(_options);
        _service = new AssetRateLimitService(_cacheMock.Object, optionsMock, loggerMock.Object);
    }

    [Fact]
    public async Task CheckAssetAccessRateAsync_AllowsRequest_WhenUnderLimit()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.CheckAssetAccessRateAsync(assetId);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(1);
        result.Limit.Should().Be(_options.MaxAccessPerAssetPerHour);
    }

    [Fact]
    public async Task CheckAssetAccessRateAsync_IncrementsCounter()
    {
        // Arrange
        var assetId = Guid.NewGuid();

        // Act
        await _service.CheckAssetAccessRateAsync(assetId);

        // Assert
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Record403ResponseAsync_RecordsFailedAttempt()
    {
        // Arrange
        var ipAddress = "192.168.1.1";

        // Act
        var result = await _service.Record403ResponseAsync(ipAddress);

        // Assert
        result.Should().NotBeNull();
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task IsIpBlockedAsync_ReturnsFalse_WhenNotBlocked()
    {
        // Arrange
        var ipAddress = "192.168.1.1";
        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.IsIpBlockedAsync(ipAddress);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAccessStatsAsync_ReturnsStats_WithZeroCount_WhenNoAccess()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var stats = await _service.GetAccessStatsAsync(assetId);

        // Assert
        stats.AssetReferenceId.Should().Be(assetId);
        stats.CurrentHourCount.Should().Be(0);
    }
}
