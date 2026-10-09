using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace GameGuild.Resources.UnitTests.Services;

public class ResourceThrottlingServiceTests
{
    private readonly Mock<IResourceThrottlingPolicyRepository> _policyRepositoryMock;
    private readonly Mock<IResourceQuotaRepository> _quotaRepositoryMock;
    private readonly ResourceThrottlingService _service;

    public ResourceThrottlingServiceTests()
    {
        _policyRepositoryMock = new Mock<IResourceThrottlingPolicyRepository>();
        _quotaRepositoryMock = new Mock<IResourceQuotaRepository>();
        Mock<ILogger<ResourceThrottlingService>> loggerMock = new Mock<ILogger<ResourceThrottlingService>>();

        _service = new ResourceThrottlingService(
            _policyRepositoryMock.Object,
            _quotaRepositoryMock.Object,
            loggerMock.Object);
    }

    #region SetPolicyAsync Tests

    [Fact]
    public async Task SetPolicyAsync_NoExistingPolicy_CreatesNewPolicy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var type = ResourceUsageType.ApiCalls;
        var strategy = ThrottlingStrategy.GradualDegradation;
        long threshold = 80;

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy?)null);

        _policyRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<ResourceThrottlingPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy policy, CancellationToken _) => policy);

        // Act
        var result = await _service.SetPolicyAsync(tenantId, type, strategy, threshold);

        // Assert
        result.Should().NotBeNull();
        result.ResourceType.Should().Be(type);
        result.Strategy.Should().Be(strategy);
        result.ThrottlingThresholdPercent.Should().Be((int)threshold);
        result.IsActive.Should().BeTrue();
        _policyRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<ResourceThrottlingPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPolicyAsync_ExistingPolicy_UpdatesPolicy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var type = ResourceUsageType.Storage;
        var existingPolicy = new ResourceThrottlingPolicy
        {
            Id = Guid.NewGuid(),
            ResourceType = type,
            Strategy = ThrottlingStrategy.None,
            ThrottlingThresholdPercent = 50,
            IsActive = true
        };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPolicy);

        _policyRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<ResourceThrottlingPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy policy, CancellationToken _) => policy);

        // Act
        var result = await _service.SetPolicyAsync(tenantId, type, ThrottlingStrategy.HardCutoff, 90);

        // Assert
        result.Strategy.Should().Be(ThrottlingStrategy.HardCutoff);
        result.ThrottlingThresholdPercent.Should().Be(90);
        _policyRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ResourceThrottlingPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPolicyAsync_WithConfiguration_StoresConfiguration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var configuration = "{\"maxDelay\": 5000}";

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy?)null);

        _policyRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<ResourceThrottlingPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy policy, CancellationToken _) => policy);

        // Act
        var result = await _service.SetPolicyAsync(tenantId, ResourceUsageType.Storage, ThrottlingStrategy.RateLimiting, 75, configuration);

        // Assert
        result.Configuration.Should().Be(configuration);
    }

    #endregion

    #region GetPolicyAsync Tests

    [Fact]
    public async Task GetPolicyAsync_PolicyExists_ReturnsPolicy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var type = ResourceUsageType.FeatureFlags;
        var expectedPolicy = new ResourceThrottlingPolicy { ResourceType = type };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPolicy);

        // Act
        var result = await _service.GetPolicyAsync(tenantId, type);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedPolicy);
    }

    [Fact]
    public async Task GetPolicyAsync_PolicyDoesNotExist_ReturnsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy?)null);

        // Act
        var result = await _service.GetPolicyAsync(tenantId, ResourceUsageType.Storage);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetTenantPoliciesAsync Tests

    [Fact]
    public async Task GetTenantPoliciesAsync_ReturnsPolicies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var policies = new List<ResourceThrottlingPolicy>
        {
            new() { ResourceType = ResourceUsageType.ApiCalls },
            new() { ResourceType = ResourceUsageType.Storage }
        };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        // Act
        var result = await _service.GetTenantPoliciesAsync(tenantId);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTenantPoliciesAsync_NoPolicies_ReturnsEmpty()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceThrottlingPolicy>());

        // Act
        var result = await _service.GetTenantPoliciesAsync(tenantId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region DeletePolicyAsync Tests

    [Fact]
    public async Task DeletePolicyAsync_PolicyExists_DeletesAndReturnsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var type = ResourceUsageType.ApiCalls;
        var policy = new ResourceThrottlingPolicy { Id = Guid.NewGuid() };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _policyRepositoryMock
            .Setup(x => x.DeleteAsync(policy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeletePolicyAsync(tenantId, type);

        // Assert
        result.Should().BeTrue();
        _policyRepositoryMock.Verify(x => x.DeleteAsync(policy.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePolicyAsync_PolicyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy?)null);

        // Act
        var result = await _service.DeletePolicyAsync(tenantId, ResourceUsageType.Storage);

        // Assert
        result.Should().BeFalse();
        _policyRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ShouldThrottleAsync Tests

    [Fact]
    public async Task ShouldThrottleAsync_NoPolicyOrInactive_ReturnsNoThrottle()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy?)null);

        // Act
        var (shouldBlock, delayMs) = await _service.ShouldThrottleAsync(tenantId, ResourceUsageType.ApiCalls, 100);

        // Assert
        shouldBlock.Should().BeFalse();
        delayMs.Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrottleAsync_InactivePolicy_ReturnsNoThrottle()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var policy = new ResourceThrottlingPolicy
        {
            ResourceType = ResourceUsageType.ApiCalls,
            IsActive = false
        };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        // Act
        var (shouldBlock, delayMs) = await _service.ShouldThrottleAsync(tenantId, ResourceUsageType.ApiCalls, 100);

        // Assert
        shouldBlock.Should().BeFalse();
        delayMs.Should().Be(0);
    }

    #endregion

    #region ApplyThrottlingAsync Tests

    [Fact]
    public async Task ApplyThrottlingAsync_NoActivePolicy_AllowsRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceThrottlingPolicy?)null);

        // Act
        var result = await _service.ApplyThrottlingAsync(tenantId, ResourceUsageType.ApiCalls);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.DelayMs.Should().Be(0);
        result.AppliedStrategy.Should().Be(ThrottlingStrategy.None);
        result.Reason.Should().Be("No throttling policy active");
    }

    [Fact]
    public async Task ApplyThrottlingAsync_ActivePolicyLowUsage_AllowsRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var policy = new ResourceThrottlingPolicy
        {
            ResourceType = ResourceUsageType.ApiCalls,
            Strategy = ThrottlingStrategy.GradualDegradation,
            ThrottlingThresholdPercent = 80,
            IsActive = true
        };

        var quota = new ResourceQuota
        {
            Type = ResourceUsageType.ApiCalls,
            CurrentUsage = 50,
            HardLimit = 100
        };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.ApiCalls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _quotaRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.ApiCalls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        // Act
        var result = await _service.ApplyThrottlingAsync(tenantId, ResourceUsageType.ApiCalls);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.AppliedStrategy.Should().Be(ThrottlingStrategy.GradualDegradation);
    }

    [Fact]
    public async Task ApplyThrottlingAsync_NoQuota_AllowsRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var policy = new ResourceThrottlingPolicy
        {
            ResourceType = ResourceUsageType.Storage,
            IsActive = true,
            Strategy = ThrottlingStrategy.RateLimiting
        };

        _policyRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.Storage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _quotaRepositoryMock
            .Setup(x => x.GetByTenantAndTypeAsync(tenantId, ResourceUsageType.Storage, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceQuota?)null);

        // Act
        var result = await _service.ApplyThrottlingAsync(tenantId, ResourceUsageType.Storage);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    #endregion
}
