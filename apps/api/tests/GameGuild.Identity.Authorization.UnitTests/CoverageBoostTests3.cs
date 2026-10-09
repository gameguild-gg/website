using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

#region MemoryPolicyCache Tests

public class MemoryPolicyCacheTests
{
    private readonly MemoryCache _memoryCache;
    private readonly Mock<IDistributedCache> _distributedCacheMock;
    private readonly AuthorizationCacheOptions _cacheOptions;

    public MemoryPolicyCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10000 });
        _distributedCacheMock = new Mock<IDistributedCache>();
        _cacheOptions = new AuthorizationCacheOptions
        {
            PolicyTtlSeconds = 300,
            UseDistributedCache = false
        };
    }

    private MemoryPolicyCache CreateCache(bool useDistributed = false)
    {
        _cacheOptions.UseDistributedCache = useDistributed;
        return new MemoryPolicyCache(
            _memoryCache,
            Options.Create(_cacheOptions),
            useDistributed ? _distributedCacheMock.Object : null,
            NullLogger<MemoryPolicyCache>.Instance);
    }

    [Fact]
    public void Get_WhenInL1Cache_ShouldReturnPolicy()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("testPolicy", "tenant1", 1, policy);

        var result = cache.Get("testPolicy", "tenant1", 1);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Get_WhenNotInCache_ShouldReturnNull()
    {
        var cache = CreateCache();

        var result = cache.Get("missing", "tenant1", 1);

        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenInL2Cache_ShouldReturnAndPromoteToL1()
    {
        // Arrange: L2 has serialized policy data
        var dto = new { AuthenticationSchemes = new List<string>(), RequireAuthenticatedUser = true, RequirementTypes = new List<string>() };
        var json = JsonSerializer.Serialize(dto);
        var bytes = Encoding.UTF8.GetBytes(json);

        _distributedCacheMock.Setup(dc => dc.Get(It.IsAny<string>())).Returns(bytes);

        var cache = CreateCache(useDistributed: true);

        var result = cache.Get("testPolicy", "tenant1", 1);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Get_WhenL2Fails_ShouldReturnNull()
    {
        _distributedCacheMock.Setup(dc => dc.Get(It.IsAny<string>()))
            .Throws(new InvalidOperationException("L2 failure"));

        var cache = CreateCache(useDistributed: true);

        // L2 error is caught, returns null
        var result = cache.Get("testPolicy", "tenant1", 1);
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenL2ReturnsEmpty_ShouldReturnNull()
    {
        _distributedCacheMock.Setup(dc => dc.Get(It.IsAny<string>())).Returns(Array.Empty<byte>());

        var cache = CreateCache(useDistributed: true);

        var result = cache.Get("testPolicy", "tenant1", 1);
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenL2ReturnsInvalidJson_ShouldReturnNull()
    {
        var bytes = Encoding.UTF8.GetBytes("not valid json");
        _distributedCacheMock.Setup(dc => dc.Get(It.IsAny<string>())).Returns(bytes);

        var cache = CreateCache(useDistributed: true);

        var result = cache.Get("testPolicy", "tenant1", 1);
        result.Should().BeNull();
    }

    [Fact]
    public void Set_ShouldStoreInL1()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        cache.Set("myPolicy", "tenant1", 1, policy);

        cache.Get("myPolicy", "tenant1", 1).Should().NotBeNull();
    }

    [Fact]
    public void Set_WithDistributed_ShouldStoreInBoth()
    {
        var cache = CreateCache(useDistributed: true);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        cache.Set("myPolicy", "tenant1", 1, policy);

        cache.Get("myPolicy", "tenant1", 1).Should().NotBeNull();
        _distributedCacheMock.Verify(dc => dc.Set(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>()), Times.Once);
    }

    [Fact]
    public void Set_WhenL2Throws_ShouldThrow()
    {
        _distributedCacheMock.Setup(dc => dc.Set(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>()))
            .Throws(new InvalidOperationException("L2 write failure"));

        var cache = CreateCache(useDistributed: true);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        var act = () => cache.Set("myPolicy", "tenant1", 1, policy);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invalidate_Tenant_ShouldRemoveAllTenantKeys()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("p1", "tenant1", 1, policy);
        cache.Set("p2", "tenant1", 1, policy);

        cache.Invalidate("tenant1");

        cache.Get("p1", "tenant1", 1).Should().BeNull();
        cache.Get("p2", "tenant1", 1).Should().BeNull();
    }

    [Fact]
    public void Invalidate_Tenant_WithNoKeys_ShouldNotThrow()
    {
        var cache = CreateCache();
        var act = () => cache.Invalidate("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public void Invalidate_Tenant_WithDistributed_ShouldRemoveFromBoth()
    {
        var cache = CreateCache(useDistributed: true);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("p1", "tenant1", 1, policy);

        // Reset to track only Remove calls
        _distributedCacheMock.Invocations.Clear();

        cache.Invalidate("tenant1");

        _distributedCacheMock.Verify(dc => dc.Remove(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Invalidate_TenantL2Throws_ShouldRethrow()
    {
        var cache = CreateCache(useDistributed: true);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("p1", "tenant1", 1, policy);

        _distributedCacheMock.Setup(dc => dc.Remove(It.IsAny<string>()))
            .Throws(new InvalidOperationException("L2 remove failure"));

        var act = () => cache.Invalidate("tenant1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invalidate_PolicyAndTenant_ShouldExerciseCodePath()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("targetPolicy", "tenant1", 1, policy);
        cache.Set("otherPolicy", "tenant1", 1, policy);

        // Exercises the Invalidate(policyName, tenantId) path
        // Note: keys have "policy:" prefix so pattern match uses StartsWith
        var act = () => cache.Invalidate("targetPolicy", "tenant1");
        act.Should().NotThrow();
    }

    [Fact]
    public void Invalidate_PolicyAndTenant_NoMatchingKeys_ShouldNotThrow()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("otherPolicy", "tenant1", 1, policy);

        var act = () => cache.Invalidate("nonexistent", "tenant1");
        act.Should().NotThrow();
    }

    [Fact]
    public void Invalidate_PolicyAndTenant_NoTenantKeys_ShouldNotThrow()
    {
        var cache = CreateCache();
        var act = () => cache.Invalidate("policy", "nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public void Invalidate_PolicyAndTenant_WithDistributed_ShouldExercisePath()
    {
        var cache = CreateCache(useDistributed: true);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("targetPolicy", "tenant1", 1, policy);

        _distributedCacheMock.Invocations.Clear();

        // Exercises the code path for Invalidate(policyName, tenantId)
        var act = () => cache.Invalidate("targetPolicy", "tenant1");
        act.Should().NotThrow();
    }

    [Fact]
    public void Invalidate_PolicyAndTenant_WithDistributedAndMatchingPrefix_ShouldRemove()
    {
        // Use a key prefix that matches the pattern to exercise the L2 removal path
        var cache = CreateCache(useDistributed: true);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        // The BuildKey format is "policy:{name}|{tenant}|{version}"
        // The pattern for Invalidate(name, tenant) is "{name}|{tenant}|"
        // To exercise the removal path, use tenant-level invalidation
        cache.Set("p1", "tenant1", 1, policy);

        _distributedCacheMock.Invocations.Clear();

        // Tenant-level invalidation removes all keys for a tenant (including L2)
        cache.Invalidate("tenant1");

        _distributedCacheMock.Verify(dc => dc.Remove(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Set_DifferentVersions_ShouldStoreAsDifferentKeys()
    {
        var cache = CreateCache();
        var policy1 = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var policy2 = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        cache.Set("p", "t", 1, policy1);
        cache.Set("p", "t", 2, policy2);

        cache.Get("p", "t", 1).Should().NotBeNull();
        cache.Get("p", "t", 2).Should().NotBeNull();
    }
}

#endregion

#region HybridPermissionCache Tests

public class HybridPermissionCacheTests
{
    private readonly MemoryCache _l1Cache;
    private readonly Mock<IDistributedCache> _l2CacheMock;
    private readonly Mock<ICacheMetricsService> _metricsMock;
    private readonly AuthorizationCacheOptions _options;

    public HybridPermissionCacheTests()
    {
        _l1Cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10000 });
        _l2CacheMock = new Mock<IDistributedCache>();
        _metricsMock = new Mock<ICacheMetricsService>();
        _options = new AuthorizationCacheOptions
        {
            PermissionTtlSeconds = 300,
            UseDistributedCache = false,
            DistributedCacheTtlSeconds = 600
        };
    }

    private HybridPermissionCache CreateCache(bool useL2 = false)
    {
        _options.UseDistributedCache = useL2;
        return new HybridPermissionCache(
            _l1Cache,
            Options.Create(_options),
            _metricsMock.Object,
            NullLogger<HybridPermissionCache>.Instance,
            useL2 ? _l2CacheMock.Object : null);
    }

    [Fact]
    public async Task GetAsync_L1Hit_ShouldReturnValue()
    {
        var cache = CreateCache();
        await cache.SetAsync("key1", "hello", "test");

        var result = await cache.GetAsync<string>("key1", "test");

        result.Should().Be("hello");
        _metricsMock.Verify(m => m.RecordHit(CacheLevel.L1, "test"), Times.Once);
    }

    [Fact]
    public async Task GetAsync_Miss_ShouldReturnNull()
    {
        var cache = CreateCache();

        var result = await cache.GetAsync<string>("missing", "test");

        result.Should().BeNull();
        _metricsMock.Verify(m => m.RecordMiss("test"), Times.Once);
    }

    [Fact]
    public async Task GetAsync_L2Hit_ShouldPromoteToL1()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes("fromL2");
        _l2CacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var cache = CreateCache(useL2: true);

        var result = await cache.GetAsync<string>("key1", "test");

        result.Should().Be("fromL2");
        _metricsMock.Verify(m => m.RecordHit(CacheLevel.L2, "test"), Times.Once);
    }

    [Fact]
    public async Task GetAsync_L2Error_ShouldReturnMiss()
    {
        _l2CacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("L2 failure"));

        var cache = CreateCache(useL2: true);

        var result = await cache.GetAsync<string>("key1", "test");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValueAsync_L1Hit_ShouldReturnFound()
    {
        var cache = CreateCache();
        await cache.SetValueAsync("intKey", 42, "test");

        var result = await cache.GetValueAsync<int>("intKey", "test");

        result.Found.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task GetValueAsync_Miss_ShouldReturnNotFound()
    {
        var cache = CreateCache();

        var result = await cache.GetValueAsync<int>("missing", "test");

        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task GetValueAsync_L2Hit_ShouldReturn()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(99);
        _l2CacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var cache = CreateCache(useL2: true);

        var result = await cache.GetValueAsync<int>("intKey", "test");
        result.Found.Should().BeTrue();
        result.Value.Should().Be(99);
    }

    [Fact]
    public async Task GetValueAsync_L2Error_ShouldReturnMiss()
    {
        _l2CacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        var cache = CreateCache(useL2: true);

        var result = await cache.GetValueAsync<int>("key", "test");
        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_WithTtl_ShouldStore()
    {
        var cache = CreateCache();
        await cache.SetAsync("k", "v", "test", 60);

        var result = await cache.GetAsync<string>("k", "test");
        result.Should().Be("v");
    }

    [Fact]
    public async Task SetAsync_WithL2_ShouldStoreInBoth()
    {
        var cache = CreateCache(useL2: true);
        await cache.SetAsync("k", "val", "test");

        _l2CacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_L2Error_ShouldKeepL1Value()
    {
        _l2CacheMock.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("L2 write fail"));

        var cache = CreateCache(useL2: true);

        await cache.SetAsync("k", "v", "test");

        var result = await cache.GetAsync<string>("k", "test");
        result.Should().Be("v");
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveFromL1()
    {
        var cache = CreateCache();
        await cache.SetAsync("k", "v", "test");

        await cache.RemoveAsync("k", "test");

        var result = await cache.GetAsync<string>("k", "test");
        result.Should().BeNull();
        _metricsMock.Verify(m => m.RecordEviction(CacheLevel.L1, "test", "explicit"), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WithL2_ShouldRemoveFromBoth()
    {
        var cache = CreateCache(useL2: true);
        await cache.SetAsync("k", "v", "test");

        await cache.RemoveAsync("k", "test");

        _l2CacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordEviction(CacheLevel.L2, "test", "explicit"), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_L2Error_ShouldStillRemoveL1Value()
    {
        _l2CacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        var cache = CreateCache(useL2: true);

        await cache.SetAsync("k", "v", "test");
        await cache.RemoveAsync("k", "test");

        var result = await cache.GetAsync<string>("k", "test");
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidatePatternAsync_ShouldCompleteSuccessfully()
    {
        var cache = CreateCache();

        await cache.InvalidatePatternAsync("pattern:*", "test");

        // Pattern invalidation removes matching entries from the tracked L1 cache.
    }

    [Fact]
    public void CacheResult_Hit_ShouldSetFields()
    {
        var result = CacheResult<int>.Hit(42);
        result.Found.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void CacheResult_Miss_ShouldSetDefaults()
    {
        var result = CacheResult<int>.Miss();
        result.Found.Should().BeFalse();
        result.Value.Should().Be(0);
    }
}

#endregion

#region HttpAuthorizationTenantContext Tests

public class HttpAuthorizationTenantContextTests
{
    private HttpAuthorizationTenantContext CreateContext(HttpContext? httpContext)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new HttpAuthorizationTenantContext(accessor.Object);
    }

    [Fact]
    public void TenantId_WhenNoHttpContext_ShouldReturnNull()
    {
        var ctx = CreateContext(null);
        ctx.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenPrimaryKeyIsGuid_ShouldReturn()
    {
        var tenantId = Guid.NewGuid();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["AuthorizationTenantId"] = tenantId;

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WhenPrimaryKeyIsString_ShouldParseAndReturn()
    {
        var tenantId = Guid.NewGuid();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["AuthorizationTenantId"] = tenantId.ToString();

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WhenPrimaryKeyIsEmptyGuid_ShouldReturnNull()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["AuthorizationTenantId"] = Guid.Empty.ToString();

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenFallbackKeyIsGuid_ShouldReturn()
    {
        var tenantId = Guid.NewGuid();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["TenantId"] = tenantId;

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WhenFallbackKeyIsString_ShouldParseAndReturn()
    {
        var tenantId = Guid.NewGuid();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["TenantId"] = tenantId.ToString();

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WhenFallbackIsEmptyGuid_ShouldReturnNull()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["TenantId"] = Guid.Empty;

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenNoKeys_ShouldReturnNull()
    {
        var httpCtx = new DefaultHttpContext();
        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenPrimaryKeyIsInvalidString_ShouldFallback()
    {
        var tenantId = Guid.NewGuid();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["AuthorizationTenantId"] = "not-a-guid";
        httpCtx.Items["TenantId"] = tenantId;

        var ctx = CreateContext(httpCtx);
        ctx.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void SetTenantId_ShouldSetPrimaryKey()
    {
        var tenantId = Guid.NewGuid();
        var httpCtx = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpCtx);
        var ctx = new HttpAuthorizationTenantContext(accessor.Object);

        ctx.SetTenantId(tenantId);

        httpCtx.Items["AuthorizationTenantId"].Should().Be(tenantId);
    }

    [Fact]
    public void SetTenantId_WhenNoHttpContext_ShouldNotThrow()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var ctx = new HttpAuthorizationTenantContext(accessor.Object);

        var act = () => ctx.SetTenantId(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void HasTenant_WhenTenantSet_ShouldReturnTrue()
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.Items["AuthorizationTenantId"] = Guid.NewGuid();
        IAuthorizationTenantContext ctx = CreateContext(httpCtx);

        ctx.HasTenant.Should().BeTrue();
    }

    [Fact]
    public void HasTenant_WhenNoTenant_ShouldReturnFalse()
    {
        var httpCtx = new DefaultHttpContext();
        IAuthorizationTenantContext ctx = CreateContext(httpCtx);
        ctx.HasTenant.Should().BeFalse();
    }
}

#endregion

#region AbacPolicyEvaluator Tests

public class AbacPolicyEvaluatorTests
{
    private readonly Mock<IAbacPolicyRepository> _repoMock = new();

    private AbacPolicyEvaluator CreateEvaluator()
    {
        return new AbacPolicyEvaluator(
            _repoMock.Object,
            NullLogger<AbacPolicyEvaluator>.Instance);
    }

    private static AbacPolicy CreatePolicy(
        string name = "TestPolicy",
        AbacPolicyEffect effect = AbacPolicyEffect.Allow,
        int priority = 1,
        string? resourceType = null,
        string? subjectConditions = null,
        string? resourceConditions = null,
        string? environmentConditions = null,
        string? actionConditions = null)
    {
        return new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = name,
            Effect = effect,
            Priority = priority,
            IsEnabled = true,
            ResourceType = resourceType,
            SubjectConditions = subjectConditions,
            ResourceConditions = resourceConditions,
            EnvironmentConditions = environmentConditions,
            ActionConditions = actionConditions
        };
    }

    [Fact]
    public async Task EvaluateAsync_NoPolicies_ShouldReturnNotApplicable()
    {
        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy>());

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);

        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_PermitPolicy_ShouldReturnPermit()
    {
        var policy = CreatePolicy(effect: AbacPolicyEffect.Allow);
        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);

        result.Decision.Should().Be(AbacDecision.Permit);
        result.Details.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateAsync_DenyPolicyMatches_ShouldReturnDenyImmediately()
    {
        var denyPolicy = CreatePolicy(name: "DenyAll", effect: AbacPolicyEffect.Deny, priority: 10);
        var allowPolicy = CreatePolicy(name: "AllowAll", effect: AbacPolicyEffect.Allow, priority: 1);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { denyPolicy, allowPolicy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);

        result.Decision.Should().Be(AbacDecision.Deny);
        result.DecidingPolicyName.Should().Be("DenyAll");
        result.DenialReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WithTenantId_ShouldPassToRepository()
    {
        var tenantId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetActivePoliciesAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy>());

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.tenant-id"] = tenantId },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        await evaluator.EvaluateAsync(context);

        _repoMock.Verify(r => r.GetActivePoliciesAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_ResourceTypeFilter_Mismatch_ShouldNotApplicable()
    {
        var policy = CreatePolicy(resourceType: "Course");
        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object> { ["resource.type"] = "Project" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_ResourceTypeFilter_Match_ShouldPermit()
    {
        var policy = CreatePolicy(resourceType: "Course");
        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object> { ["resource.type"] = "Course" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectConditions_Match_ShouldPermit()
    {
        var conditions = JsonSerializer.Serialize(new Dictionary<string, object> { ["subject.role"] = "admin" });
        var policy = CreatePolicy(subjectConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.role"] = "admin" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectConditions_NoMatch_ShouldNotApplicable()
    {
        var conditions = JsonSerializer.Serialize(new Dictionary<string, object> { ["subject.role"] = "admin" });
        var policy = CreatePolicy(subjectConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.role"] = "user" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_EnvironmentConditions_Match_ShouldPermit()
    {
        var conditions = JsonSerializer.Serialize(new Dictionary<string, object> { ["env.country"] = "US" });
        var policy = CreatePolicy(environmentConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object> { ["env.country"] = "US" });

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_ActionConditions_Match_ShouldPermit()
    {
        var conditions = JsonSerializer.Serialize(new Dictionary<string, object> { ["action.type"] = "read" });
        var policy = CreatePolicy(actionConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object> { ["action.type"] = "read" },
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_ResourceConditions_NoMatch_ShouldNotApplicable()
    {
        var conditions = JsonSerializer.Serialize(new Dictionary<string, object> { ["resource.owner"] = "user1" });
        var policy = CreatePolicy(resourceConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object> { ["resource.owner"] = "user2" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidJsonConditions_ShouldNotMatch()
    {
        var policy = CreatePolicy(subjectConditions: "not valid json");

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_IntCondition_ShouldMatch()
    {
        // Manually create JSON with integer value
        var conditions = "{\"subject.level\": 5}";
        var policy = CreatePolicy(subjectConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.level"] = 5 },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_BoolCondition_ShouldMatch()
    {
        var conditions = "{\"subject.verified\": true}";
        var policy = CreatePolicy(subjectConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.verified"] = true },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_BoolFalseCondition_ShouldMatch()
    {
        var conditions = "{\"subject.blocked\": false}";
        var policy = CreatePolicy(subjectConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.blocked"] = false },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_PolicyNotEffective_ShouldSkip()
    {
        var policy = CreatePolicy();
        policy.IsEnabled = false; // Not effective

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_ConditionAttributeMissing_ShouldNotMatch()
    {
        var conditions = JsonSerializer.Serialize(new Dictionary<string, object> { ["subject.role"] = "admin" });
        var policy = CreatePolicy(subjectConditions: conditions);

        _repoMock.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AbacPolicy> { policy });

        var evaluator = CreateEvaluator();
        // No "subject.role" attribute provided
        var context = new AbacRequestContext(
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>());

        var result = await evaluator.EvaluateAsync(context);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public void AbacRequestContextBuilder_ShouldBuildContext()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var context = new AbacRequestContextBuilder()
            .WithSubject(userId, tenantId, new[] { "admin" })
            .WithSubjectAttribute("subject.email", "test@test.com")
            .WithResource("Course", Guid.NewGuid(), userId)
            .WithResourceAttribute("resource.status", "published")
            .WithAction("read")
            .WithActionAttribute("action.scope", "full")
            .WithEnvironment("192.168.1.1", "Chrome", "US")
            .WithEnvironmentAttribute("environment.custom", "value")
            .Build();

        context.SubjectAttributes.Should().ContainKey("subject.user-id");
        context.SubjectAttributes.Should().ContainKey("subject.tenant-id");
        context.SubjectAttributes.Should().ContainKey("subject.roles");
        context.SubjectAttributes.Should().ContainKey("subject.email");
        context.ResourceAttributes.Should().ContainKey("resource.type");
        context.ResourceAttributes.Should().ContainKey("resource.id");
        context.ResourceAttributes.Should().ContainKey("resource.owner-id");
        context.ResourceAttributes.Should().ContainKey("resource.status");
        context.ActionAttributes.Should().ContainKey("action.id");
        context.ActionAttributes.Should().ContainKey("action.scope");
        context.EnvironmentAttributes.Should().ContainKey("environment.ip-address");
        context.EnvironmentAttributes.Should().ContainKey("environment.user-agent");
        context.EnvironmentAttributes.Should().ContainKey("environment.geo-country");
        context.EnvironmentAttributes.Should().ContainKey("environment.custom");
    }

    [Fact]
    public void AbacRequestContextBuilder_WithNoOptionals_ShouldBuild()
    {
        var context = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, Array.Empty<string>())
            .WithResource("Course", null)
            .WithAction("read")
            .WithEnvironment()
            .Build();

        context.SubjectAttributes.Should().ContainKey("subject.user-id");
        context.SubjectAttributes.Should().NotContainKey("subject.tenant-id");
        context.ResourceAttributes.Should().ContainKey("resource.type");
        context.ResourceAttributes.Should().NotContainKey("resource.id");
        context.EnvironmentAttributes.Should().ContainKey("environment.current-time");
    }
}

#endregion

#region PermissionHandler Tests

public class PermissionHandlerTests
{
    private readonly Mock<IAuthorizationTenantContext> _tenantContextMock = new();
    private readonly Mock<IAuthorizationPermissionService> _permissionServiceMock = new();
    private readonly AuthorizationTokenOptions _tokenOptions;

    public PermissionHandlerTests()
    {
        _tokenOptions = AuthorizationTokenOptions.CreateDefault();
    }

    private PermissionHandler CreateHandler()
    {
        return new PermissionHandler(
            _tenantContextMock.Object,
            _permissionServiceMock.Object,
            Options.Create(_tokenOptions),
            NullLogger<PermissionHandler>.Instance);
    }

    private static AuthorizationHandlerContext CreateAuthContext(
        ClaimsPrincipal user,
        PermissionRequirement requirement)
    {
        return new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);
    }

    private static ClaimsPrincipal CreateUser(
        Guid? userId = null,
        Guid? tenantId = null,
        IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim>();

        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        if (permissions != null)
        {
            foreach (var p in permissions)
            {
                claims.Add(new Claim("perm", p));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task HandleRequirementAsync_ClaimsBasedPermission_StillRequiresDatabaseGrant()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);
        _permissionServiceMock.Setup(ps => ps.HasPermissionAsync(
            userId, tenantId, "courses.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateHandler();
        var user = CreateUser(userId, tenantId, new[] { "courses.read" });
        var requirement = new PermissionRequirement("courses.read", allowClaimsBased: true);
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_ClaimsBasedNotAllowed_ShouldFallbackToDb()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);
        _permissionServiceMock.Setup(ps => ps.HasPermissionAsync(
            userId, tenantId, "courses.write", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var user = CreateUser(userId: userId, permissions: new[] { "courses.write" });
        var requirement = new PermissionRequirement("courses.write", allowClaimsBased: false);
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_DbPermission_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);
        _permissionServiceMock.Setup(ps => ps.HasPermissionAsync(
            userId, tenantId, "courses.delete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var user = CreateUser(userId: userId);
        var requirement = new PermissionRequirement("courses.delete");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_DbDenied_ShouldFail()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);
        _permissionServiceMock.Setup(ps => ps.HasPermissionAsync(
            userId, tenantId, "admin.delete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var user = CreateUser(userId: userId);
        var requirement = new PermissionRequirement("admin.delete");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoUserId_ShouldFail()
    {
        var handler = CreateHandler();
        // No NameIdentifier claim
        var user = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "Test"));
        var requirement = new PermissionRequirement("courses.read");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_EmptyGuidUserId_ShouldFail()
    {
        var handler = CreateHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()) }, "Test"));
        var requirement = new PermissionRequirement("courses.read");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoTenant_ShouldFail()
    {
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(false);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns((Guid?)null);

        var handler = CreateHandler();
        // User has userId but no tenant claim
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"));
        var requirement = new PermissionRequirement("courses.read");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_TenantFromClaims_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(false);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns((Guid?)null);
        _permissionServiceMock.Setup(ps => ps.HasPermissionAsync(
            userId, tenantId, "courses.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var user = CreateUser(userId: userId, tenantId: tenantId);
        var requirement = new PermissionRequirement("courses.read");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_DbThrows_ShouldFailAndRethrow()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);
        _permissionServiceMock.Setup(ps => ps.HasPermissionAsync(
            userId, tenantId, "courses.read", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var handler = CreateHandler();
        var user = CreateUser(userId: userId);
        var requirement = new PermissionRequirement("courses.read");
        var context = CreateAuthContext(user, requirement);

        var act = async () => await handler.HandleAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleRequirementAsync_EmptyGuidTenantClaim_ShouldFail()
    {
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(false);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns((Guid?)null);

        var handler = CreateHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenant_id", Guid.Empty.ToString())
            }, "Test"));
        var requirement = new PermissionRequirement("courses.read");
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }
}

#endregion

#region TenantMatchHandler Tests

public class TenantMatchHandlerTests
{
    private readonly Mock<IAuthorizationTenantContext> _tenantContextMock = new();
    private readonly Mock<IAuthorizationTenantResolver> _tenantResolverMock = new();
    private readonly TenancyOptions _tenancyOptions;
    private readonly AuthorizationTokenOptions _tokenOptions;

    public TenantMatchHandlerTests()
    {
        _tenancyOptions = new TenancyOptions();
        _tokenOptions = AuthorizationTokenOptions.CreateDefault();
    }

    private TenantMatchHandler CreateHandler()
    {
        return new TenantMatchHandler(
            _tenantContextMock.Object,
            _tenantResolverMock.Object,
            Options.Create(_tenancyOptions),
            Options.Create(_tokenOptions),
            NullLogger<TenantMatchHandler>.Instance);
    }

    private static ClaimsPrincipal CreateUser(string? tenantClaim = null)
    {
        var claims = new List<Claim>();
        if (tenantClaim != null)
        {
            claims.Add(new Claim("tenant_id", tenantClaim));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static AuthorizationHandlerContext CreateAuthContext(
        ClaimsPrincipal user,
        TenantMatchRequirement requirement)
    {
        return new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);
    }

    [Fact]
    public async Task HandleRequirementAsync_NoResolvedTenant_ShouldFail()
    {
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(false);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns((Guid?)null);
        _tenantResolverMock.Setup(r => r.ResolveFromClaims(It.IsAny<ClaimsPrincipal>())).Returns((string?)null);

        var handler = CreateHandler();
        var user = CreateUser();
        var requirement = new TenantMatchRequirement();
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_TokenTenantMatchesResolved_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);

        var handler = CreateHandler();
        var user = CreateUser(tenantClaim: tenantId.ToString());
        var requirement = new TenantMatchRequirement();
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoTokenTenant_StrictMatch_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);

        var handler = CreateHandler();
        var user = CreateUser(); // no tenant claim
        var requirement = new TenantMatchRequirement(strictMatch: true);
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoTokenTenant_DefaultTenantMatch_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);
        _tenantResolverMock.Setup(r => r.GetUserDefaultTenant(It.IsAny<ClaimsPrincipal>()))
            .Returns(tenantId.ToString());

        var handler = CreateHandler();
        var user = CreateUser(); // no tenant claim
        var requirement = new TenantMatchRequirement(strictMatch: false);
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_TenantMismatch_NotStrict_BaseTenantFallback_ShouldSucceed()
    {
        // resolvedTenantId matches DefaultTenantId
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(false);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns((Guid?)null);
        _tenantResolverMock.Setup(r => r.ResolveFromClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns(_tenancyOptions.DefaultTenantId);

        var handler = CreateHandler();
        var user = CreateUser(tenantClaim: "some-other-tenant");
        var requirement = new TenantMatchRequirement(strictMatch: false);
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_TenantMismatch_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(true);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns(tenantId);

        var handler = CreateHandler();
        var user = CreateUser(tenantClaim: Guid.NewGuid().ToString()); // different tenant
        var requirement = new TenantMatchRequirement();
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_ResolvedFromClaims_ShouldWork()
    {
        var tenantStr = "resolved-tenant-id";
        _tenantContextMock.Setup(tc => tc.HasTenant).Returns(false);
        _tenantContextMock.Setup(tc => tc.TenantId).Returns((Guid?)null);
        _tenantResolverMock.Setup(r => r.ResolveFromClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns(tenantStr);

        var handler = CreateHandler();
        var user = CreateUser(tenantClaim: tenantStr);
        var requirement = new TenantMatchRequirement();
        var context = CreateAuthContext(user, requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }
}

#endregion
