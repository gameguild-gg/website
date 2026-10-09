using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Resources.UnitTests.Services;

/// <summary>
/// Tests for CachedResourceQuotaService to verify cache behavior.
/// </summary>
public class CachedResourceQuotaServiceTests
{
    private readonly Mock<IResourceQuotaService> _innerServiceMock;
    private readonly Mock<ILogger<CachedResourceQuotaService>> _loggerMock;
    private readonly CachedResourceQuotaService _cachedService;

    public CachedResourceQuotaServiceTests()
    {
        _innerServiceMock = new Mock<IResourceQuotaService>();
        MemoryCache cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        _loggerMock = new Mock<ILogger<CachedResourceQuotaService>>();
        _cachedService = new CachedResourceQuotaService(
            _innerServiceMock.Object,
            cache,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetQuotaAsync_CachesResult_AndReturnsFromCacheOnSecondCall()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var resourceType = ResourceUsageType.Users;
        var quota = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 50);

        _innerServiceMock
            .Setup(x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        // Act - First call should hit the inner service
        var result1 = await _cachedService.GetQuotaAsync(tenantId, resourceType);

        // Act - Second call should hit the cache
        var result2 = await _cachedService.GetQuotaAsync(tenantId, resourceType);

        // Assert
        result1.Should().Be(quota);
        result2.Should().Be(quota);
        
        // Inner service should only be called once
        _innerServiceMock.Verify(
            x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetQuotaAsync_InvalidatesCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var resourceType = ResourceUsageType.Users;
        var quota1 = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 50);
        var quota2 = CreateQuota(tenantId, resourceType, hardLimit: 200, currentUsage: 50);

        _innerServiceMock
            .SetupSequence(x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota1)
            .ReturnsAsync(quota2);

        _innerServiceMock
            .Setup(x => x.SetQuotaAsync(tenantId, resourceType, 80, 200, It.IsAny<ResourceQuotaPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota2);

        // Act - Cache the first value
        var initialResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);
        initialResult!.HardLimit.Should().Be(100);

        // Act - Set new quota (should invalidate cache)
        await _cachedService.SetQuotaAsync(tenantId, resourceType, 80, 200);

        // Act - Get quota again (should get fresh data from inner service)
        var afterSetResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);

        // Assert
        afterSetResult!.HardLimit.Should().Be(200);
        
        // Inner service should be called twice for GetQuotaAsync (initial + after invalidation)
        _innerServiceMock.Verify(
            x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TryAtomicConsumeAsync_InvalidatesCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var resourceType = ResourceUsageType.Users;
        var quotaBefore = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 50);
        var quotaAfter = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 51);

        _innerServiceMock
            .SetupSequence(x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotaBefore)
            .ReturnsAsync(quotaAfter);

        _innerServiceMock
            .Setup(x => x.TryAtomicConsumeAsync(tenantId, resourceType, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 51L, 100L));

        // Act - Cache the initial value
        var initialResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);
        initialResult!.CurrentUsage.Should().Be(50);

        // Act - Consume (should invalidate cache)
        await _cachedService.TryAtomicConsumeAsync(tenantId, resourceType, 1);

        // Act - Get quota again (should get fresh data)
        var afterConsumeResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);

        // Assert
        afterConsumeResult!.CurrentUsage.Should().Be(51);
        
        // Inner service should be called twice for GetQuotaAsync
        _innerServiceMock.Verify(
            x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DecrementUsageAsync_InvalidatesCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var resourceType = ResourceUsageType.Users;
        var quotaBefore = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 50);
        var quotaAfter = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 49);

        _innerServiceMock
            .SetupSequence(x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotaBefore)
            .ReturnsAsync(quotaAfter);

        _innerServiceMock
            .Setup(x => x.DecrementUsageAsync(tenantId, resourceType, 1, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Cache the initial value
        var initialResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);
        initialResult!.CurrentUsage.Should().Be(50);

        // Act - Decrement (should invalidate cache)
        await _cachedService.DecrementUsageAsync(tenantId, resourceType, 1);

        // Act - Get quota again (should get fresh data)
        var afterDecrementResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);

        // Assert
        afterDecrementResult!.CurrentUsage.Should().Be(49);
        
        // Inner service should be called twice for GetQuotaAsync
        _innerServiceMock.Verify(
            x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DeleteQuotaAsync_InvalidatesCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var resourceType = ResourceUsageType.Users;
        var quota = CreateQuota(tenantId, resourceType, hardLimit: 100, currentUsage: 50);

        _innerServiceMock
            .SetupSequence(x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota)
            .ReturnsAsync((ResourceQuota?)null);

        _innerServiceMock
            .Setup(x => x.DeleteQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Cache the initial value
        var initialResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);
        initialResult.Should().NotBeNull();

        // Act - Delete (should invalidate cache)
        await _cachedService.DeleteQuotaAsync(tenantId, resourceType);

        // Act - Get quota again (should get null from inner service)
        var afterDeleteResult = await _cachedService.GetQuotaAsync(tenantId, resourceType);

        // Assert
        afterDeleteResult.Should().BeNull();
        
        // Inner service should be called twice for GetQuotaAsync
        _innerServiceMock.Verify(
            x => x.GetQuotaAsync(tenantId, resourceType, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetTenantQuotasAsync_CachesResult()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var quotas = new List<ResourceQuota>
        {
            CreateQuota(tenantId, ResourceUsageType.Users, hardLimit: 100, currentUsage: 50),
            CreateQuota(tenantId, ResourceUsageType.Projects, hardLimit: 50, currentUsage: 10)
        };

        _innerServiceMock
            .Setup(x => x.GetTenantQuotasAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotas);

        // Act - First call
        var result1 = await _cachedService.GetTenantQuotasAsync(tenantId);

        // Act - Second call (should use cache)
        var result2 = await _cachedService.GetTenantQuotasAsync(tenantId);

        // Assert
        result1.Should().BeEquivalentTo(quotas);
        result2.Should().BeEquivalentTo(quotas);
        
        // Inner service should only be called once
        _innerServiceMock.Verify(
            x => x.GetTenantQuotasAsync(tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cache_IsTenantIsolated_NoCrossTenanLeakage()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var resourceType = ResourceUsageType.Users;
        
        var quota1 = CreateQuota(tenantId1, resourceType, hardLimit: 100, currentUsage: 10);
        var quota2 = CreateQuota(tenantId2, resourceType, hardLimit: 200, currentUsage: 20);

        _innerServiceMock
            .Setup(x => x.GetQuotaAsync(tenantId1, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota1);
        
        _innerServiceMock
            .Setup(x => x.GetQuotaAsync(tenantId2, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota2);

        // Act
        var result1 = await _cachedService.GetQuotaAsync(tenantId1, resourceType);
        var result2 = await _cachedService.GetQuotaAsync(tenantId2, resourceType);

        // Assert - each tenant gets their own quota
        result1!.TenantId.Should().Be(tenantId1);
        result1.HardLimit.Should().Be(100);
        result1.CurrentUsage.Should().Be(10);
        
        result2!.TenantId.Should().Be(tenantId2);
        result2.HardLimit.Should().Be(200);
        result2.CurrentUsage.Should().Be(20);
    }

    private static ResourceQuota CreateQuota(
        Guid tenantId,
        ResourceUsageType type,
        long? hardLimit,
        long currentUsage)
    {
        var quota = new ResourceQuota
        {
            Id = Guid.NewGuid(),
            Type = type,
            HardLimit = hardLimit,
            SoftLimit = hardLimit.HasValue ? (long?)(hardLimit.Value * 0.8) : null,
            CurrentUsage = currentUsage,
            IsActive = true,
            Period = ResourceQuotaPeriod.Monthly,
            LastReset = DateTime.UtcNow.AddDays(-1)
        };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });
        return quota;
    }
}
