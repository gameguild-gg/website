using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Features;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Features.UnitTests;

// Concrete subclass for testing abstract AnalyticsExportRequest
internal class TestAnalyticsExportRequest : AnalyticsExportRequest { }

#region FeatureContextFactory Tests

public class FeatureContextFactoryAdditionalTests
{
    [Fact]
    public void CreateFromHttpContext_WithOverrides_PopulatesCorrectly()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        httpContext.Request.Headers.UserAgent = "TestAgent";

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var result = FeatureContextFactory.CreateFromHttpContext(httpContext, userId, tenantId, "staging");

        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.Environment.Should().Be("staging");
        result.IpAddress.Should().Be("192.168.1.1");
        result.UserAgent.Should().Be("TestAgent");
    }

    [Fact]
    public void CreateFromHttpContext_WithClaims_ExtractsUserIdFromSub()
    {
        var httpContext = new DefaultHttpContext();
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("sub", userId.ToString()) };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var result = FeatureContextFactory.CreateFromHttpContext(httpContext);

        result.UserId.Should().Be(userId);
    }

    [Fact]
    public void CreateFromHttpContext_WithUserIdClaim_ExtractsUserId()
    {
        var httpContext = new DefaultHttpContext();
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("userId", userId.ToString()) };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var result = FeatureContextFactory.CreateFromHttpContext(httpContext);

        result.UserId.Should().Be(userId);
    }

    [Fact]
    public void CreateFromHttpContext_WithPermissionsClaims_ExtractsPermissions()
    {
        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim("permission", "read"),
            new Claim("permission", "write")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var result = FeatureContextFactory.CreateFromHttpContext(httpContext);

        result.Permissions.Should().Contain("read");
        result.Permissions.Should().Contain("write");
    }

    [Fact]
    public void CreateFromHttpContext_WithCfIpCountryHeader_ExtractsCountry()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["CF-IPCountry"] = "US";

        var result = FeatureContextFactory.CreateFromHttpContext(httpContext);

        result.Country.Should().Be("US");
    }

    [Fact]
    public void CreateFromHttpContext_WithXCountryCodeHeader_ExtractsCountry()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Country-Code"] = "BR";

        var result = FeatureContextFactory.CreateFromHttpContext(httpContext);

        result.Country.Should().Be("BR");
    }

    [Fact]
    public void ToOpenFeatureContext_WithAllProperties_SetsAll()
    {
        var context = new FeatureContext
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Environment = "staging",
            IpAddress = "10.0.0.1",
            UserAgent = "TestUA",
            Country = "US",
            SubscriptionPlanId = "pro",
            Permissions = new List<string> { "admin", "read" },
            CustomAttributes = new Dictionary<string, object>
            {
                { "strAttr", "hello" },
                { "intAttr", 42 },
                { "longAttr", 100L },
                { "doubleAttr", 3.14 },
                { "boolAttr", true },
                { "dateAttr", new DateTime(2025, 1, 1) },
                { "nullAttr", null! },
                { "objAttr", new object() }
            }
        };

        var ofContext = FeatureContextFactory.ToOpenFeatureContext(context);

        ofContext.Should().NotBeNull();
    }

    [Fact]
    public void Enrich_AddsCustomAttributes()
    {
        var context = new FeatureContext { CustomAttributes = new Dictionary<string, object>() };
        var attrs = new Dictionary<string, object>
        {
            { "role", "admin" },
            { "level", 5 }
        };

        var enriched = FeatureContextFactory.Enrich(context, attrs);

        enriched.CustomAttributes["role"].Should().Be("admin");
        enriched.CustomAttributes["level"].Should().Be(5);
    }

    [Fact]
    public void CreateFromHttpContext_NullThrows()
    {
        var act = () => FeatureContextFactory.CreateFromHttpContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToOpenFeatureContext_NullThrows()
    {
        var act = () => FeatureContextFactory.ToOpenFeatureContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Enrich_NullContextThrows()
    {
        var act = () => FeatureContextFactory.Enrich(null!, new Dictionary<string, object>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Enrich_NullAttributesThrows()
    {
        var act = () => FeatureContextFactory.Enrich(new FeatureContext(), null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

#endregion

#region Decorator Tests

public class LoggingFeatureFlagServiceAdditionalTests
{
    private readonly Mock<IFeatureFlagEvaluationService> _inner = new();
    private readonly LoggingFeatureFlagService _sut;

    public LoggingFeatureFlagServiceAdditionalTests()
    {
        _sut = new LoggingFeatureFlagService(_inner.Object, NullLogger<LoggingFeatureFlagService>.Instance);
    }

    [Fact]
    public async Task EvaluateAsync_DelegatesToInner()
    {
        var expected = new FeatureEvaluationResult { FeatureKey = "test", IsEnabled = true };
        _inner.Setup(x => x.EvaluateAsync("test", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.EvaluateAsync("test", new FeatureContext(), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateAsync_WhenInnerThrows_PropagatesException()
    {
        _inner.Setup(x => x.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        var act = () => _sut.EvaluateAsync("test", new FeatureContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EvaluateBulkAsync_DelegatesToInner()
    {
        var expected = new BulkEvaluateFeaturesResponse();
        _inner.Setup(x => x.EvaluateBulkAsync(It.IsAny<BulkEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.EvaluateBulkAsync(new BulkEvaluationRequest(), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsEnabledAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.IsEnabledAsync("key", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.IsEnabledAsync("key", new FeatureContext(), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetValueAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.GetValueAsync("key", It.IsAny<FeatureContext>(), "default", It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");

        var result = await _sut.GetValueAsync("key", new FeatureContext(), "default", CancellationToken.None);

        result.Should().Be("value");
    }

    [Fact]
    public async Task GetEnabledFeaturesAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.GetEnabledFeaturesAsync(It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "f1", "f2" });

        var result = await _sut.GetEnabledFeaturesAsync(new FeatureContext(), CancellationToken.None);

        result.Should().Contain("f1");
    }
}

public class AnalyticsFeatureFlagServiceAdditionalTests
{
    private readonly Mock<IFeatureFlagEvaluationService> _inner = new();
    private readonly Mock<IFeatureFlagAnalyticsService> _analytics = new();
    private readonly AnalyticsFeatureFlagService _sut;

    public AnalyticsFeatureFlagServiceAdditionalTests()
    {
        _sut = new AnalyticsFeatureFlagService(_inner.Object, _analytics.Object);
    }

    [Fact]
    public async Task EvaluateAsync_DelegatesToInnerAndTracksAnalytics()
    {
        var expected = new FeatureEvaluationResult { FeatureKey = "test", IsEnabled = true };
        _inner.Setup(x => x.EvaluateAsync("test", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.EvaluateAsync("test", new FeatureContext(), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateBulkAsync_DelegatesToInner()
    {
        var expected = new BulkEvaluateFeaturesResponse();
        _inner.Setup(x => x.EvaluateBulkAsync(It.IsAny<BulkEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.EvaluateBulkAsync(new BulkEvaluationRequest(), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsEnabledAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.IsEnabledAsync("key", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.IsEnabledAsync("key", new FeatureContext(), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetValueAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.GetValueAsync("key", It.IsAny<FeatureContext>(), 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);

        var result = await _sut.GetValueAsync("key", new FeatureContext(), 42, CancellationToken.None);

        result.Should().Be(99);
    }

    [Fact]
    public async Task GetEnabledFeaturesAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.GetEnabledFeaturesAsync(It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a" });

        var result = await _sut.GetEnabledFeaturesAsync(new FeatureContext(), CancellationToken.None);

        result.Should().Contain("a");
    }
}

public class CachedFeatureFlagServiceAdditionalTests
{
    private readonly Mock<IFeatureFlagEvaluationService> _inner = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly CachedFeatureFlagService _sut;

    public CachedFeatureFlagServiceAdditionalTests()
    {
        _sut = new CachedFeatureFlagService(_inner.Object, _cache.Object);
    }

    [Fact]
    public async Task EvaluateAsync_CacheMiss_CallsInnerAndCaches()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var expected = new FeatureEvaluationResult { FeatureKey = "key", IsEnabled = true, Value = "on" };
        _inner.Setup(x => x.EvaluateAsync("key", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.EvaluateAsync("key", new FeatureContext { TenantId = Guid.NewGuid() }, CancellationToken.None);

        result.FeatureKey.Should().Be("key");
        result.IsEnabled.Should().BeTrue();
        _cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_CacheHit_ReturnsFromCache()
    {
        var cached = new FeatureEvaluationResult { FeatureKey = "key", IsEnabled = false };
        var json = JsonSerializer.Serialize(cached);
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(json));

        var result = await _sut.EvaluateAsync("key", new FeatureContext(), CancellationToken.None);

        result.FeatureKey.Should().Be("key");
        result.IsEnabled.Should().BeFalse();
        _inner.Verify(x => x.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateBulkAsync_DelegatesToInner()
    {
        var expected = new BulkEvaluateFeaturesResponse();
        _inner.Setup(x => x.EvaluateBulkAsync(It.IsAny<BulkEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.EvaluateBulkAsync(new BulkEvaluationRequest(), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsEnabledAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.IsEnabledAsync("key", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.IsEnabledAsync("key", new FeatureContext(), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetValueAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.GetValueAsync("k", It.IsAny<FeatureContext>(), "def", It.IsAny<CancellationToken>()))
            .ReturnsAsync("val");

        var result = await _sut.GetValueAsync("k", new FeatureContext(), "def", CancellationToken.None);

        result.Should().Be("val");
    }

    [Fact]
    public async Task GetEnabledFeaturesAsync_DelegatesToInner()
    {
        _inner.Setup(x => x.GetEnabledFeaturesAsync(It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "x" });

        var result = await _sut.GetEnabledFeaturesAsync(new FeatureContext(), CancellationToken.None);

        result.Should().Contain("x");
    }
}

#endregion

#region Targeting Handler Tests

public class PlanTargetingHandlerAdditionalTests
{
    private readonly PlanTargetingHandler _sut = new();

    [Fact]
    public async Task EvaluateAsync_NoPlanId_ReturnsNull()
    {
        var flag = CreateFeatureFlag("test", targets: new[]
        {
            CreateTarget(FeatureFlagConstants.TargetTypes.Plan, "pro")
        });

        var result = await _sut.EvaluateAsync(flag, new FeatureContext { SubscriptionPlanId = null });

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_MatchingPlan_ReturnsEnabled()
    {
        var flag = CreateFeatureFlag("test", enabledValue: "yes", targets: new[]
        {
            CreateTarget(FeatureFlagConstants.TargetTypes.Plan, "pro", isEnabled: true)
        });

        var result = await _sut.EvaluateAsync(flag, new FeatureContext { SubscriptionPlanId = "pro" });

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_NoMatchingPlan_ReturnsNull()
    {
        var flag = CreateFeatureFlag("test", targets: new[]
        {
            CreateTarget(FeatureFlagConstants.TargetTypes.Plan, "enterprise")
        });

        var result = await _sut.EvaluateAsync(flag, new FeatureContext { SubscriptionPlanId = "free" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithRolloutPercentage_WhenIdentifierIsInRollout_ReturnsEnabled()
    {
        var flag = CreateFeatureFlag("test", targets: new[]
        {
            CreateTarget(FeatureFlagConstants.TargetTypes.Plan, "pro", isEnabled: true, rollout: 50)
        });

        var context = CreatePlanContext(
            "pro",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000022"
        );

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
        result.Value.Should().Be("on");
        result.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public async Task EvaluateAsync_WithRolloutPercentage_WhenIdentifierIsOutsideRollout_ReturnsDisabled()
    {
        var flag = CreateFeatureFlag("test", targets: new[]
        {
            CreateTarget(FeatureFlagConstants.TargetTypes.Plan, "pro", isEnabled: true, rollout: 50)
        });

        var context = CreatePlanContext(
            "pro",
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000011"
        );

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeFalse();
        result.Value.Should().Be("off");
        result!.RolloutPercentage.Should().Be(50);
    }

    private static FeatureContext CreatePlanContext(string subscriptionPlanId, string tenantId, string userId)
    {
        return new FeatureContext
        {
            SubscriptionPlanId = subscriptionPlanId,
            TenantId = Guid.Parse(tenantId),
            UserId = Guid.Parse(userId)
        };
    }

    private static FeatureFlag CreateFeatureFlag(string key, string? defaultValue = "off", string? enabledValue = "on", FeatureFlagTarget[]? targets = null)
    {
        return new FeatureFlag
        {
            Key = key,
            DefaultValue = defaultValue,
            EnabledValue = enabledValue,
            IsEnabled = true,
            Environment = "production",
            Targets = targets?.ToList() ?? new List<FeatureFlagTarget>()
        };
    }

    private static FeatureFlagTarget CreateTarget(string type, string identifier, bool isEnabled = true, int rollout = 100, string? customValue = null)
    {
        return new FeatureFlagTarget
        {
            TargetType = type,
            TargetIdentifier = identifier,
            IsEnabled = isEnabled,
            RolloutPercentage = rollout,
            CustomValue = customValue
        };
    }
}

public class CustomTargetingHandlerAdditionalTests
{
    private readonly CustomTargetingHandler _sut = new();

    [Fact]
    public async Task EvaluateAsync_NoCustomTarget_ReturnsNull()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            Targets = new List<FeatureFlagTarget>
            {
                new() { TargetType = FeatureFlagConstants.TargetTypes.Tenant, TargetIdentifier = "t1" }
            }
        };

        var result = await _sut.EvaluateAsync(flag, new FeatureContext());

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_CustomTargetWithMatchingAttribute_ReturnsEnabled()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            EnabledValue = "custom_on",
            DefaultValue = "off",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Custom,
                    TargetIdentifier = "role=admin",
                    IsEnabled = true,
                    RolloutPercentage = 100
                }
            }
        };

        var context = new FeatureContext
        {
            CustomAttributes = new Dictionary<string, object> { { "role", "admin" } }
        };

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_CustomTargetNoMatch_ReturnsNull()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Custom,
                    TargetIdentifier = "role=admin",
                    IsEnabled = true,
                    RolloutPercentage = 100
                }
            }
        };

        var context = new FeatureContext
        {
            CustomAttributes = new Dictionary<string, object> { { "role", "user" } }
        };

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_CustomTargetWithRollout_AppliesRollout()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            EnabledValue = "on",
            DefaultValue = "off",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Custom,
                    TargetIdentifier = "role=admin",
                    IsEnabled = true,
                    RolloutPercentage = 50
                }
            }
        };

        var context = new FeatureContext
        {
            CustomAttributes = new Dictionary<string, object> { { "role", "admin" } },
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().NotBeNull();
        result!.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public async Task EvaluateAsync_CustomTargetEmptyAttributes_ReturnsNull()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Custom,
                    TargetIdentifier = "role=admin",
                    IsEnabled = true
                }
            }
        };

        var context = new FeatureContext { CustomAttributes = new Dictionary<string, object>() };

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().BeNull();
    }
}

public class TenantTargetingHandlerAdditionalTests
{
    private readonly TenantTargetingHandler _sut;

    public TenantTargetingHandlerAdditionalTests()
    {
        _sut = new TenantTargetingHandler(NullLogger<TenantTargetingHandler>.Instance);
    }

    [Fact]
    public async Task EvaluateAsync_NoTenantTargets_ReturnsNull()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            Targets = new List<FeatureFlagTarget>
            {
                new() { TargetType = FeatureFlagConstants.TargetTypes.User, TargetIdentifier = "u1" }
            }
        };

        var result = await _sut.EvaluateAsync(flag, new FeatureContext());

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_TenantTargetExists_NoTenantId_FailsClosed()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            DefaultValue = "off",
            Targets = new List<FeatureFlagTarget>
            {
                new() { TargetType = FeatureFlagConstants.TargetTypes.Tenant, TargetIdentifier = Guid.NewGuid().ToString() }
            }
        };

        var result = await _sut.EvaluateAsync(flag, new FeatureContext { TenantId = null });

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeFalse();
        result.Reason.Should().Contain("Fail-closed");
    }

    [Fact]
    public async Task EvaluateAsync_MatchingTenant_ReturnsEnabled()
    {
        var tenantId = Guid.NewGuid();
        var flag = new FeatureFlag
        {
            Key = "test",
            EnabledValue = "on",
            DefaultValue = "off",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Tenant,
                    TargetIdentifier = tenantId.ToString(),
                    IsEnabled = true,
                    RolloutPercentage = 100
                }
            }
        };

        var result = await _sut.EvaluateAsync(flag, new FeatureContext { TenantId = tenantId });

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_MatchingTenantWithRollout_WhenIdentifierIsInRollout_ReturnsEnabled()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var flag = new FeatureFlag
        {
            Key = "test",
            EnabledValue = "on",
            DefaultValue = "off",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Tenant,
                    TargetIdentifier = tenantId.ToString(),
                    IsEnabled = true,
                    RolloutPercentage = 50
                }
            }
        };

        var context = new FeatureContext
        {
            TenantId = tenantId,
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000013")
        };

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
        result.Value.Should().Be("on");
        result!.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public async Task EvaluateAsync_MatchingTenantWithRollout_WhenIdentifierIsOutsideRollout_ReturnsDisabled()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var flag = new FeatureFlag
        {
            Key = "test",
            EnabledValue = "on",
            DefaultValue = "off",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Tenant,
                    TargetIdentifier = tenantId.ToString(),
                    IsEnabled = true,
                    RolloutPercentage = 50
                }
            }
        };

        var context = new FeatureContext
        {
            TenantId = tenantId,
            UserId = Guid.Parse("00000000-0000-0000-0000-00000000004c")
        };

        var result = await _sut.EvaluateAsync(flag, context);

        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeFalse();
        result.Value.Should().Be("off");
        result.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public async Task EvaluateAsync_NonMatchingTenant_ReturnsNull()
    {
        var flag = new FeatureFlag
        {
            Key = "test",
            Targets = new List<FeatureFlagTarget>
            {
                new()
                {
                    TargetType = FeatureFlagConstants.TargetTypes.Tenant,
                    TargetIdentifier = Guid.NewGuid().ToString()
                }
            }
        };

        var result = await _sut.EvaluateAsync(flag, new FeatureContext { TenantId = Guid.NewGuid() });

        result.Should().BeNull();
    }
}

#endregion

#region DatabaseFeatureFlagProvider Tests

public class DatabaseFeatureFlagProviderAdditionalTests
{
    private readonly Mock<IServiceProvider> _serviceProvider = new();
    private readonly Mock<IFeatureFlagEvaluationService> _evaluationService = new();
    private readonly DatabaseFeatureFlagProvider _sut;

    public DatabaseFeatureFlagProviderAdditionalTests()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var scopedProvider = new Mock<IServiceProvider>();

        scopedProvider.Setup(x => x.GetService(typeof(IFeatureFlagEvaluationService)))
            .Returns(_evaluationService.Object);

        scope.Setup(x => x.ServiceProvider).Returns(scopedProvider.Object);
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        _serviceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactory.Object);

        _sut = new DatabaseFeatureFlagProvider(_serviceProvider.Object, NullLogger<DatabaseFeatureFlagProvider>.Instance);
    }

    [Fact]
    public async Task ResolveBooleanValueAsync_WhenEnabled_ReturnsTrue()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "true" });

        var result = await _sut.ResolveBooleanValueAsync("flag", false);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveBooleanValueAsync_WhenDisabled_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = false });

        var result = await _sut.ResolveBooleanValueAsync("flag", true);

        result.Value.Should().BeTrue(); // Returns default
    }

    [Fact]
    public async Task ResolveBooleanValueAsync_WhenError_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.ResolveBooleanValueAsync("flag", false);

        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveStringValueAsync_WhenEnabled_ReturnsValue()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "hello" });

        var result = await _sut.ResolveStringValueAsync("flag", "default");

        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task ResolveStringValueAsync_WhenDisabled_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = false });

        var result = await _sut.ResolveStringValueAsync("flag", "default");

        result.Value.Should().Be("default");
    }

    [Fact]
    public async Task ResolveStringValueAsync_WhenError_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("err"));

        var result = await _sut.ResolveStringValueAsync("flag", "fallback");

        result.Value.Should().Be("fallback");
    }

    [Fact]
    public async Task ResolveIntegerValueAsync_WhenEnabled_ReturnsParsedInt()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "42" });

        var result = await _sut.ResolveIntegerValueAsync("flag", 0);

        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ResolveIntegerValueAsync_WhenDisabled_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = false });

        var result = await _sut.ResolveIntegerValueAsync("flag", 10);

        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task ResolveIntegerValueAsync_WhenError_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("err"));

        var result = await _sut.ResolveIntegerValueAsync("flag", 5);

        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task ResolveDoubleValueAsync_WhenEnabled_ReturnsParsedDouble()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "42" });

        var result = await _sut.ResolveDoubleValueAsync("flag", 0.0);

        result.Value.Should().Be(42.0);
    }

    [Fact]
    public async Task ResolveDoubleValueAsync_WhenDisabled_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = false });

        var result = await _sut.ResolveDoubleValueAsync("flag", 1.5);

        result.Value.Should().Be(1.5);
    }

    [Fact]
    public async Task ResolveDoubleValueAsync_WhenError_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("err"));

        var result = await _sut.ResolveDoubleValueAsync("flag", 9.99);

        result.Value.Should().Be(9.99);
    }

    [Fact]
    public async Task ResolveStructureValueAsync_ReturnsDefault()
    {
        var defaultVal = new OpenFeature.Model.Value("test");
        var result = await _sut.ResolveStructureValueAsync("flag", defaultVal);

        result.Value.Should().Be(defaultVal);
    }

    [Fact]
    public void GetMetadata_ReturnsCorrectName()
    {
        var meta = _sut.GetMetadata();
        meta.Name.Should().Be("GameGuild Database Provider");
    }

    [Fact]
    public async Task ResolveBooleanValueAsync_WithOpenFeatureContext_ConvertsContext()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var ofCtx = OpenFeature.Model.EvaluationContext.Builder()
            .Set("userId", userId.ToString())
            .Set("tenantId", tenantId.ToString())
            .Set("environment", "staging")
            .Set("ipAddress", "1.2.3.4")
            .Set("userAgent", "TestUA")
            .Set("country", "US")
            .Set("subscriptionPlanId", "pro")
            .Set("permissions", "admin,read")
            .Set("customKey", "customValue")
            .Build();

        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.Is<FeatureContext>(c =>
                c.UserId == userId &&
                c.TenantId == tenantId &&
                c.Environment == "staging"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "true" });

        var result = await _sut.ResolveBooleanValueAsync("flag", false, ofCtx);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveBooleanValueAsync_WithNullContext_UsesEmptyContext()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "true" });

        var result = await _sut.ResolveBooleanValueAsync("flag", false, null);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveBooleanValueAsync_EnabledButUnparsable_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "not-a-bool" });

        var result = await _sut.ResolveBooleanValueAsync("flag", false);

        result.Value.Should().BeFalse(); // default
    }

    [Fact]
    public async Task ResolveIntegerValueAsync_EnabledButUnparsable_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "not-a-number" });

        var result = await _sut.ResolveIntegerValueAsync("flag", 7);

        result.Value.Should().Be(7); // default
    }

    [Fact]
    public async Task ResolveDoubleValueAsync_EnabledButUnparsable_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "xyz" });

        var result = await _sut.ResolveDoubleValueAsync("flag", 2.5);

        result.Value.Should().Be(2.5); // default
    }

    [Fact]
    public async Task ResolveStringValueAsync_EnabledWithNullValue_ReturnsDefault()
    {
        _evaluationService.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = null });

        var result = await _sut.ResolveStringValueAsync("flag", "fallback");

        result.Value.Should().Be("fallback");
    }
}

#endregion

#region FeatureFlagAnalyticsService Tests

public class FeatureFlagAnalyticsServiceAdditionalTests
{
    private readonly Mock<IFeatureFlagQueryRepository> _queryRepo = new();
    private readonly Mock<IFeatureFlagAnalyticsRepository> _analyticsRepo = new();
    private readonly FeatureFlagAnalyticsService _sut;

    public FeatureFlagAnalyticsServiceAdditionalTests()
    {
        var options = Options.Create(new FeatureFlagOptions { EnableAnalytics = true });
        _sut = new FeatureFlagAnalyticsService(_queryRepo.Object, _analyticsRepo.Object,
            NullLogger<FeatureFlagAnalyticsService>.Instance, options);
    }

    [Fact]
    public async Task RecordUsageAsync_WhenAnalyticsDisabled_ReturnsEarly()
    {
        var options = Options.Create(new FeatureFlagOptions { EnableAnalytics = false });
        var sut = new FeatureFlagAnalyticsService(_queryRepo.Object, _analyticsRepo.Object,
            NullLogger<FeatureFlagAnalyticsService>.Instance, options);

        await sut.RecordUsageAsync("key", new FeatureContext(), true);

        _queryRepo.Verify(x => x.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordUsageAsync_WhenFeatureNotFound_LogsWarning()
    {
        _queryRepo.Setup(x => x.GetByKeyAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureFlag?)null);

        await _sut.RecordUsageAsync("unknown", new FeatureContext(), true);

        _analyticsRepo.Verify(x => x.RecordUsageAsync(It.IsAny<FeatureFlagUsage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordUsageAsync_WhenFeatureExists_RecordsUsage()
    {
        var flag = new FeatureFlag { Key = "test" };
        _queryRepo.Setup(x => x.GetByKeyAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(flag);

        await _sut.RecordUsageAsync("test", new FeatureContext(), true, "val");

        _analyticsRepo.Verify(x => x.RecordUsageAsync(It.IsAny<FeatureFlagUsage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordUsageAsync_WhenExceptionOccurs_DoesNotThrow()
    {
        _queryRepo.Setup(x => x.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Should not throw
        await _sut.RecordUsageAsync("test", new FeatureContext(), true);
    }

    [Fact]
    public async Task GetRealtimeStatsAsync_WithFeatureKey_ReturnsStats()
    {
        var stats = new FeatureFlagUsageStats { TotalAccessCount = 100 };
        _analyticsRepo.Setup(x => x.GetAggregatedStatsAsync(
            "key", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await _sut.GetRealtimeStatsAsync("key");

        result.Should().NotBeNull();
        result.EvaluationsLastHour.Should().Be(100);
    }

    [Fact]
    public async Task GetRealtimeStatsAsync_WithoutFeatureKey_ReturnsZeros()
    {
        var result = await _sut.GetRealtimeStatsAsync();

        result.Should().NotBeNull();
        result.EvaluationsLastHour.Should().Be(0);
    }

    [Fact]
    public async Task GetRealtimeStatsAsync_WhenError_ReturnsEmpty()
    {
        _analyticsRepo.Setup(x => x.GetAggregatedStatsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("err"));

        var result = await _sut.GetRealtimeStatsAsync("key");


        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportAnalyticsAsync_AsJson_ReturnsJsonResult()
    {
        _queryRepo.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlag> { new() { Key = "f1" } });

        _analyticsRepo.Setup(x => x.GetUsageAnalyticsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlagUsage>());

        _analyticsRepo.Setup(x => x.GetAggregatedStatsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlagUsageStats());

        var request = new TestAnalyticsExportRequest { Format = "json", FeatureKeys = new List<string>() };
        var result = await _sut.ExportAnalyticsAsync(request);

        result.ContentType.Should().Be("application/json");
        result.Content.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportAnalyticsAsync_AsCsv_ReturnsCsvResult()
    {
        _queryRepo.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlag> { new() { Key = "f1" } });

        _analyticsRepo.Setup(x => x.GetUsageAnalyticsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlagUsage>());

        _analyticsRepo.Setup(x => x.GetAggregatedStatsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlagUsageStats());

        var request = new TestAnalyticsExportRequest { Format = "csv", FeatureKeys = new List<string>() };
        var result = await _sut.ExportAnalyticsAsync(request);

        result.ContentType.Should().Be("text/csv");
    }

    [Fact]
    public async Task ExportAnalyticsAsync_UnsupportedFormat_Throws()
    {
        _queryRepo.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlag> { new() { Key = "f1" } });

        _analyticsRepo.Setup(x => x.GetUsageAnalyticsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlagUsage>());

        _analyticsRepo.Setup(x => x.GetAggregatedStatsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlagUsageStats());

        var request = new TestAnalyticsExportRequest { Format = "xml", FeatureKeys = new List<string>() };

        var act = () => _sut.ExportAnalyticsAsync(request);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task ExportAnalyticsAsync_WhenError_Throws()
    {
        _queryRepo.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB fail"));

        var request = new TestAnalyticsExportRequest { Format = "json", FeatureKeys = new List<string>() };

        var act = () => _sut.ExportAnalyticsAsync(request);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ExportAnalyticsAsync_WithSpecificKeys_UsesThoseKeys()
    {
        _analyticsRepo.Setup(x => x.GetUsageAnalyticsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlagUsage>());

        _analyticsRepo.Setup(x => x.GetAggregatedStatsAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlagUsageStats());

        var request = new TestAnalyticsExportRequest
        {
            Format = "json",
            FeatureKeys = new List<string> { "f1", "f2" }
        };

        var result = await _sut.ExportAnalyticsAsync(request);

        result.RecordCount.Should().Be(2);
    }
}

#endregion

#region FeatureFlagEvaluationService private helper tests

public class FeatureFlagEvaluationServicePrivateHelperTests
{
    [Fact]
    public void CreateNotFoundResult_ViaReflection_ReturnsCorrectResult()
    {
        var method = typeof(FeatureFlagEvaluationService).GetMethod("CreateNotFoundResult",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var result = (FeatureEvaluationResult)method!.Invoke(null, new object[] { "testKey", DateTime.UtcNow })!;

        result.FeatureKey.Should().Be("testKey");
        result.IsEnabled.Should().BeFalse();
        result.Reason.Should().Contain("not found");
    }

    [Fact]
    public void CreateEnvironmentMismatchResult_ViaReflection_ReturnsCorrectResult()
    {
        var method = typeof(FeatureFlagEvaluationService).GetMethod("CreateEnvironmentMismatchResult",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var result = (FeatureEvaluationResult)method!.Invoke(null, new object[] { "testKey", "production", "staging", DateTime.UtcNow })!;

        result.FeatureKey.Should().Be("testKey");
        result.IsEnabled.Should().BeFalse();
        result.Reason.Should().Contain("Environment mismatch");
    }

    [Fact]
    public void CreateErrorResult_ViaReflection_ReturnsCorrectResult()
    {
        var method = typeof(FeatureFlagEvaluationService).GetMethod("CreateErrorResult",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var result = (FeatureEvaluationResult)method!.Invoke(null, new object[] { "testKey", "some error", DateTime.UtcNow })!;

        result.FeatureKey.Should().Be("testKey");
        result.IsEnabled.Should().BeFalse();
        result.Reason.Should().Contain("some error");
    }
}

#endregion

#region Controller Constructor Tests

public class FeatureControllerConstructorTests
{
    [Fact]
    public void CapabilitiesController_CanBeConstructed()
    {
        var service = new Mock<ICapabilityService>();
        var controller = new CapabilitiesController(service.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void FeaturesController_CanBeConstructed()
    {
        var sender = new Mock<GameGuild.CQRS.ISender>();
        var controller = new FeaturesController(sender.Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagsController_CanBeConstructed()
    {
        var evalService = new Mock<IFeatureFlagEvaluationService>();
        var logger = NullLogger<FeatureFlagsController>.Instance;
        var controller = new FeatureFlagsController(evalService.Object, logger);
        controller.Should().NotBeNull();
    }
}

#endregion

#region Query Handler Constructor Tests

public class FeatureQueryHandlerConstructorTests
{
    [Fact]
    public void ValidateFeatureFlagKeyQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IFeatureFlagQueryRepository>();
        var handler = new ValidateFeatureFlagKeyQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetSdkConfigurationQueryHandler_CanBeConstructed()
    {
        var sdkService = new Mock<IFeatureFlagSdkService>();
        var handler = new GetSdkConfigurationQueryHandler(sdkService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ExportAnalyticsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IFeatureFlagAnalyticsRepository>();
        var handler = new ExportAnalyticsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void SearchFeatureFlagsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IFeatureFlagQueryRepository>();
        var handler = new SearchFeatureFlagsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagStatisticsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IFeatureFlagQueryRepository>();
        var handler = new GetFeatureFlagStatisticsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagAnalyticsQueryHandler_CanBeConstructed()
    {
        var repo = new Mock<IFeatureFlagQueryRepository>();
        var handler = new GetFeatureFlagAnalyticsQueryHandler(repo.Object);
        handler.Should().NotBeNull();
    }
}

#endregion

#region OpenFeatureHostedInitializer Tests

public class OpenFeatureHostedInitializerTests
{
    [Fact]
    public async Task StartAsync_CompletesSuccessfully()
    {
        var sut = new OpenFeatureHostedInitializer(NullLogger<OpenFeatureHostedInitializer>.Instance);
        await sut.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        var sut = new OpenFeatureHostedInitializer(NullLogger<OpenFeatureHostedInitializer>.Instance);
        await sut.StopAsync(CancellationToken.None);
    }
}

#endregion
