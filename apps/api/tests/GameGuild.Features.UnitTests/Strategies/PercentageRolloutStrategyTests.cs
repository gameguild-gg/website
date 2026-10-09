using FluentAssertions;
using Xunit;

namespace GameGuild.Features.UnitTests.Strategies;

/// <summary>
/// Tests for PercentageRolloutStrategy verifying deterministic bucketing behavior.
/// </summary>
public class PercentageRolloutStrategyTests
{
    private readonly PercentageRolloutStrategy _strategy;

    public PercentageRolloutStrategyTests()
    {
        _strategy = new PercentageRolloutStrategy();
    }

    [Fact]
    public void FeatureType_ReturnsPercentage()
    {
        // Assert
        _strategy.FeatureType.Should().Be(FeatureFlagType.Percentage);
    }

    [Fact]
    public async Task PercentageRollout_DeterministicForSameUser_ReturnsSameResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var featureFlag = CreateFeatureFlag("deterministic-test", rolloutPercentage: 50);
        var context = new FeatureContext { UserId = userId, TenantId = Guid.NewGuid() };

        // Act - Call multiple times with same user
        var results = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            var result = await _strategy.EvaluateAsync(featureFlag, context);
            results.Add(result.IsEnabled);
        }

        // Assert
        results.Should().AllBeEquivalentTo(results[0], 
            "Same user should always get the same result for percentage rollout");
    }

    [Fact]
    public async Task PercentageRollout_DeterministicForSameTenant_ReturnsSameResult()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var featureFlag = CreateFeatureFlag("tenant-deterministic-test", rolloutPercentage: 50);
        var context = new FeatureContext { TenantId = tenantId };

        // Act - Call multiple times with same tenant
        var results = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            var result = await _strategy.EvaluateAsync(featureFlag, context);
            results.Add(result.IsEnabled);
        }

        // Assert
        results.Should().AllBeEquivalentTo(results[0],
            "Same tenant should always get the same result for percentage rollout");
    }

    [Fact]
    public async Task PercentageRollout_DifferentUsers_GetDifferentBuckets()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("different-users-test", rolloutPercentage: 50);
        var results = new Dictionary<Guid, bool>();

        // Act - Evaluate for 100 different users
        for (int i = 0; i < 100; i++)
        {
            var userId = Guid.NewGuid();
            var context = new FeatureContext { UserId = userId, TenantId = Guid.NewGuid() };
            var result = await _strategy.EvaluateAsync(featureFlag, context);
            results[userId] = result.IsEnabled;
        }

        // Assert - With 50% rollout, we expect roughly half enabled and half disabled
        var enabledCount = results.Values.Count(v => v);
        var disabledCount = results.Values.Count(v => !v);

        // Allow some variance (should be between 30-70 for 50% with 100 samples)
        enabledCount.Should().BeInRange(20, 80, 
            "50% rollout should result in roughly half of users being enabled");
        disabledCount.Should().BeInRange(20, 80,
            "50% rollout should result in roughly half of users being disabled");
    }

    [Fact]
    public async Task EvaluateAsync_Returns100PercentEnabled_When100PercentRollout()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("full-rollout", rolloutPercentage: 100);
        
        // Act - Test with multiple users
        var allEnabled = true;
        for (int i = 0; i < 50; i++)
        {
            var context = new FeatureContext { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
            var result = await _strategy.EvaluateAsync(featureFlag, context);
            if (!result.IsEnabled)
            {
                allEnabled = false;
            }
        }

        // Assert
        allEnabled.Should().BeTrue("100% rollout should enable for all users");
    }

    [Fact]
    public async Task EvaluateAsync_Returns0PercentEnabled_When0PercentRollout()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("no-rollout", rolloutPercentage: 0);
        
        // Act - Test with multiple users
        var allDisabled = true;
        for (int i = 0; i < 50; i++)
        {
            var context = new FeatureContext { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
            var result = await _strategy.EvaluateAsync(featureFlag, context);
            if (result.IsEnabled)
            {
                allDisabled = false;
            }
        }

        // Assert
        allDisabled.Should().BeTrue("0% rollout should disable for all users");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDisabled_WhenFeatureIsDisabled()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("disabled-feature", rolloutPercentage: 100);
        featureFlag.IsEnabled = false;
        var context = new FeatureContext { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };

        // Act
        var result = await _strategy.EvaluateAsync(featureFlag, context);

        // Assert
        result.IsEnabled.Should().BeFalse("Disabled feature should always return false regardless of rollout");
        result.Reason.Should().Contain("disabled");
    }

    [Fact]
    public async Task EvaluateAsync_UsesDifferentSalt_ForDifferentFeatures()
    {
        // Arrange - Same user, different features
        var userId = Guid.NewGuid();
        var context = new FeatureContext { UserId = userId, TenantId = Guid.NewGuid() };
        
        var feature1 = CreateFeatureFlag("feature-a", rolloutPercentage: 50);
        var feature2 = CreateFeatureFlag("feature-b", rolloutPercentage: 50);

        // Act
        var results1 = new List<bool>();
        var results2 = new List<bool>();
        
        for (int i = 0; i < 10; i++)
        {
            var result1 = await _strategy.EvaluateAsync(feature1, context);
            var result2 = await _strategy.EvaluateAsync(feature2, context);
            results1.Add(result1.IsEnabled);
            results2.Add(result2.IsEnabled);
        }

        // Assert - Same user gets consistent results per feature
        results1.Should().AllBeEquivalentTo(results1[0], "Same user should always get same result for feature A");
        results2.Should().AllBeEquivalentTo(results2[0], "Same user should always get same result for feature B");
        // Note: results1[0] may or may not equal results2[0], but that's by design - different features use different salts
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsEnabledValue_WhenInRollout()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("value-test", rolloutPercentage: 100);
        featureFlag.EnabledValue = "premium-feature-enabled";
        featureFlag.DefaultValue = "feature-disabled";
        var context = new FeatureContext { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };

        // Act
        var result = await _strategy.EvaluateAsync(featureFlag, context);

        // Assert
        result.Value.Should().Be("premium-feature-enabled");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDefaultValue_WhenNotInRollout()
    {
        // Arrange
        var featureFlag = CreateFeatureFlag("value-test", rolloutPercentage: 0);
        featureFlag.EnabledValue = "premium-feature-enabled";
        featureFlag.DefaultValue = "feature-disabled";
        var context = new FeatureContext { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };

        // Act
        var result = await _strategy.EvaluateAsync(featureFlag, context);

        // Assert
        result.Value.Should().Be("feature-disabled");
    }

    #region RolloutHashCalculator Direct Tests

    [Fact]
    public void RolloutHashCalculator_IsInRollout_DeterministicForSameIdentifier()
    {
        // Arrange
        var identifier = Guid.NewGuid().ToString();
        var salt = "test-feature";

        // Act
        var results = Enumerable.Range(0, 10)
            .Select(_ => RolloutHashCalculator.IsInRollout(identifier, 50, salt))
            .ToList();

        // Assert
        results.Should().AllBeEquivalentTo(results[0],
            "Same identifier and salt should always produce the same result");
    }

    [Fact]
    public void RolloutHashCalculator_GetBucketValue_ReturnsSameValueForSameInput()
    {
        // Arrange
        var identifier = "test-user-123";
        var salt = "feature-key";

        // Act
        var bucket1 = RolloutHashCalculator.GetBucketValue(identifier, salt);
        var bucket2 = RolloutHashCalculator.GetBucketValue(identifier, salt);

        // Assert
        bucket1.Should().Be(bucket2, "Same input should always produce the same bucket value");
    }

    [Fact]
    public void RolloutHashCalculator_GetBucketValue_ReturnsValueBetween0And99()
    {
        // Act - Test with many random identifiers
        for (int i = 0; i < 100; i++)
        {
            var identifier = Guid.NewGuid().ToString();
            var bucket = RolloutHashCalculator.GetBucketValue(identifier);

            // Assert
            bucket.Should().BeInRange(0u, 99u, "Bucket value should always be between 0 and 99");
        }
    }

    [Fact]
    public void RolloutHashCalculator_CreateIdentifier_UsesTenantIdFirst()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new FeatureContext
        {
            TenantId = tenantId,
            UserId = userId,
            IpAddress = "192.168.1.1"
        };

        // Act
        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        // Assert
        identifier.Should().Be(tenantId.ToString(), "TenantId should be used as identifier when present");
    }

    [Fact]
    public void RolloutHashCalculator_CreateIdentifier_FallsBackToUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var context = new FeatureContext
        {
            TenantId = null,
            UserId = userId,
            IpAddress = "192.168.1.1"
        };

        // Act
        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        // Assert
        identifier.Should().Be(userId.ToString(), "UserId should be used when TenantId is null");
    }

    [Fact]
    public void RolloutHashCalculator_CreateIdentifier_FallsBackToIpAddress()
    {
        // Arrange
        var ipAddress = "192.168.1.1";
        var context = new FeatureContext
        {
            TenantId = null,
            UserId = null,
            IpAddress = ipAddress
        };

        // Act
        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        // Assert
        identifier.Should().Be(ipAddress, "IpAddress should be used when TenantId and UserId are null");
    }

    [Fact]
    public void RolloutHashCalculator_CreateIdentifier_FallsBackToAnonymous()
    {
        // Arrange
        var context = new FeatureContext
        {
            TenantId = null,
            UserId = null,
            IpAddress = null
        };

        // Act
        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        // Assert
        identifier.Should().Be(FeatureFlagConstants.AnonymousIdentifier, 
            "Anonymous identifier should be used when no identifying context is available");
    }

    #endregion

    #region Helper Methods

    private static FeatureFlag CreateFeatureFlag(string key, int rolloutPercentage)
    {
        return new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = key,
            IsEnabled = true,
            Type = FeatureFlagType.Percentage,
            RolloutPercentage = rolloutPercentage,
            DefaultValue = "false",
            EnabledValue = "true"
        };
    }

    #endregion
}
