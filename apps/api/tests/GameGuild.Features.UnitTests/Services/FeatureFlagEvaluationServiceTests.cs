using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Features.UnitTests.Services;

/// <summary>
/// Tests for FeatureFlagEvaluationService covering defensive logging, kill switch, and expiration handling.
/// </summary>
public class FeatureFlagEvaluationServiceTests
{
    private readonly Mock<IFeatureFlagQueryRepository> _repositoryMock;
    private readonly Mock<ILogger<FeatureFlagEvaluationService>> _loggerMock;
    private readonly List<IFeatureEvaluationStrategy> _strategies;
    private readonly FeatureFlagEvaluationService _service;

    public FeatureFlagEvaluationServiceTests()
    {
        _repositoryMock = new Mock<IFeatureFlagQueryRepository>();
        _loggerMock = new Mock<ILogger<FeatureFlagEvaluationService>>();
        Mock<IOptions<FeatureFlagOptions>> optionsMock = new Mock<IOptions<FeatureFlagOptions>>();
        optionsMock.Setup(x => x.Value).Returns(new FeatureFlagOptions { MaxBulkEvaluationSize = 100 });

        _strategies = new List<IFeatureEvaluationStrategy>
        {
            new SimpleToggleStrategy(),
            new PercentageRolloutStrategy()
        };

        _service = new FeatureFlagEvaluationService(
            _repositoryMock.Object,
            _strategies,
            _loggerMock.Object,
            optionsMock.Object
        );
    }

    #region EvaluateAsync_LogsWarningWithoutTenantId

    [Fact]
    public async Task EvaluateAsync_LogsDebugMessage_WhenNoTenantIdProvided()
    {
        // Arrange
        var featureKey = "test-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: true);
        var context = new FeatureContext { TenantId = null, UserId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        await _service.EvaluateAsync(featureKey, context);

        // Assert - Verify debug log is written when TenantId is missing
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("without TenantId")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when TenantId is not provided");
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotLogWarning_WhenTenantIdProvided()
    {
        // Arrange
        var featureKey = "test-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: true);
        var context = new FeatureContext { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        await _service.EvaluateAsync(featureKey, context);

        // Assert - Verify debug log is NOT written when TenantId is provided
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("without TenantId")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Should not log missing TenantId message when TenantId is provided");
    }

    #endregion

    #region KillSwitch_OverridesAllTargeting

    [Fact]
    public async Task EvaluateAsync_ReturnsDisabled_WhenKillSwitchIsEngaged()
    {
        // Arrange
        var featureKey = "kill-switch-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: false, isKillSwitch: true);
        var context = new FeatureContext { TenantId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        var result = await _service.EvaluateAsync(featureKey, context);

        // Assert
        result.IsEnabled.Should().BeFalse("Kill switch should override all targeting when disabled");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsEnabled_WhenKillSwitchIsNotEngaged()
    {
        // Arrange
        var featureKey = "kill-switch-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: true, isKillSwitch: true);
        var context = new FeatureContext { TenantId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        var result = await _service.EvaluateAsync(featureKey, context);

        // Assert
        result.IsEnabled.Should().BeTrue("Kill switch that is enabled should allow feature");
    }

    [Fact]
    public async Task EvaluateAsync_KillSwitchDisabled_IgnoresTargetingRules()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var featureKey = "emergency-shutoff";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: false, isKillSwitch: true);

        // Add targeting rules that would normally enable the feature
        featureFlag.Targets.Add(new FeatureFlagTarget
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = featureFlag.Id,
            TargetType = FeatureFlagConstants.TargetTypes.Tenant,
            TargetIdentifier = tenantId.ToString(),
            IsEnabled = true,
            RolloutPercentage = 100
        });

        var context = new FeatureContext { TenantId = tenantId };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        var result = await _service.EvaluateAsync(featureKey, context);

        // Assert
        result.IsEnabled.Should().BeFalse("Kill switch should override targeting rules");
    }

    #endregion

    #region ExpiredFlag_ReturnsDefaultValue

    [Fact]
    public async Task EvaluateAsync_ReturnsDefaultValue_WhenFeatureFlagIsExpired()
    {
        // Arrange
        var featureKey = "expired-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: true);
        featureFlag.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1); // Expired yesterday
        featureFlag.DefaultValue = "default-value";
        featureFlag.EnabledValue = "enabled-value";

        var context = new FeatureContext { TenantId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        var result = await _service.EvaluateAsync(featureKey, context);

        // Assert
        result.IsEnabled.Should().BeFalse("Expired feature flag should be disabled");
        result.Value.Should().Be("default-value", "Expired feature should return default value");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsEnabled_WhenFeatureFlagNotExpired()
    {
        // Arrange
        var featureKey = "valid-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: true);
        featureFlag.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30); // Expires in 30 days
        featureFlag.DefaultValue = "default-value";
        featureFlag.EnabledValue = "enabled-value";

        var context = new FeatureContext { TenantId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        var result = await _service.EvaluateAsync(featureKey, context);

        // Assert
        result.IsEnabled.Should().BeTrue("Non-expired feature flag should evaluate normally");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsEnabled_WhenNoExpirationSet()
    {
        // Arrange
        var featureKey = "permanent-feature";
        var featureFlag = CreateFeatureFlag(featureKey, isEnabled: true);
        featureFlag.ExpiresAt = null; // No expiration
        featureFlag.DefaultValue = "default-value";
        featureFlag.EnabledValue = "enabled-value";

        var context = new FeatureContext { TenantId = Guid.NewGuid() };

        _repositoryMock.Setup(x => x.GetByKeyAsync(featureKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureFlag);

        // Act
        var result = await _service.EvaluateAsync(featureKey, context);

        // Assert
        result.IsEnabled.Should().BeTrue("Feature flag without expiration should evaluate normally");
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpirationDatePassed()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("test", isEnabled: true);
        featureFlag.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        // Act
        var isExpired = featureFlag.IsExpired();

        // Assert
        isExpired.Should().BeTrue("Feature with past expiration should be expired");
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpirationDateNotPassed()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("test", isEnabled: true);
        featureFlag.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var isExpired = featureFlag.IsExpired();

        // Assert
        isExpired.Should().BeFalse("Feature with future expiration should not be expired");
    }

    #endregion

    #region Helper Methods

    private static FeatureFlag CreateFeatureFlag(string key, bool isEnabled, bool isKillSwitch = false)
    {
        return new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = key,
            IsEnabled = isEnabled,
            Type = FeatureFlagType.Toggle,
            DefaultValue = "false",
            EnabledValue = "true",
            IsKillSwitch = isKillSwitch
        };
    }

    #endregion
}

/// <summary>
/// Simple toggle strategy for testing.
/// </summary>
internal class SimpleToggleStrategy : IFeatureEvaluationStrategy
{
    public FeatureFlagType FeatureType => FeatureFlagType.Toggle;

    public Task<FeatureEvaluationResult> EvaluateAsync(FeatureFlag featureFlag, FeatureContext context, CancellationToken cancellationToken = default)
    {
        // Check for expiration first
        if (featureFlag.IsExpired())
        {
            return Task.FromResult(new FeatureEvaluationResult
            {
                IsEnabled = false,
                Value = featureFlag.DefaultValue,
                Reason = "Feature flag has expired"
            });
        }

        return Task.FromResult(new FeatureEvaluationResult
        {
            IsEnabled = featureFlag.IsEnabled,
            Value = featureFlag.IsEnabled ? featureFlag.EnabledValue : featureFlag.DefaultValue,
            Reason = featureFlag.IsEnabled ? "Simple toggle enabled" : "Simple toggle disabled"
        });
    }
}
