using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Regression tests for ACL cache-key collision prevention (issue #353): free-form
///     resourceType/resourceId values may contain the key delimiter, so two distinct resources
///     must never share a cache entry. Keys fingerprint those segments with SHA-256 (see
///     <see cref="AclCacheKeys"/>), mirroring the bulk permission key builder.
/// </summary>
public sealed class AclCacheKeyCollisionPreventionTests
{
    [Fact]
    public void FingerprintCacheKeyPart_IsDelimiterFreeHexAndDeterministic()
    {
        var first = AclCacheKeys.FingerprintCacheKeyPart("Document");
        var second = AclCacheKeys.FingerprintCacheKeyPart("Document");
        var different = AclCacheKeys.FingerprintCacheKeyPart("Document2");

        first.Should().Be(second, "the fingerprint must be stable for equal inputs");
        first.Should().NotBe(different, "distinct inputs must fingerprint differently");
        first.Should().NotContain(":");
        first.Should().MatchRegex("^[0-9A-F]+$", "fingerprints are uppercase hex");
        AclCacheKeys.FingerprintCacheKeyPart(null).Should().Be("none");
        AclCacheKeys.FingerprintCacheKeyPart(string.Empty).Should().NotBe(AclCacheKeys.FingerprintCacheKeyPart("none"));
    }

    [Theory]
    [InlineData("a:b", "c", "a", "b:c")]
    [InlineData("Document", "doc:1", "Document:", "1")]
    [InlineData("a:b:c", "d", "a", "b:c:d")]
    [InlineData("x", ":y", "x:", "y")]
    public void BuildUserCacheKey_DelimiterBearingResourcePairsDoNotCollide(
        string firstType, string firstId, string secondType, string secondId)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var first = AclCacheKeys.BuildUserCacheKey(userId, tenantId, firstType, firstId, 1, 2, 3);
        var second = AclCacheKeys.BuildUserCacheKey(userId, tenantId, secondType, secondId, 1, 2, 3);

        first.Should().NotBe(second, "distinct resource pairs must never share an ACL cache key");
    }

    [Theory]
    [InlineData("a:b", "c", "a", "b:c")]
    [InlineData("Document", "doc:1", "Document:", "1")]
    [InlineData("a:b:c", "d", "a", "b:c:d")]
    [InlineData("x", ":y", "x:", "y")]
    public void BuildSubjectCacheKey_DelimiterBearingResourcePairsDoNotCollide(
        string firstType, string firstId, string secondType, string secondId)
    {
        var subject = AclSubject.ForUser(Guid.NewGuid(), [Guid.NewGuid()], [Guid.NewGuid()]);
        var tenantId = Guid.NewGuid();

        var first = AclCacheKeys.BuildSubjectCacheKey(subject, tenantId, firstType, firstId, 1, 2, 3);
        var second = AclCacheKeys.BuildSubjectCacheKey(subject, tenantId, secondType, secondId, 1, 2, 3);

        first.Should().NotBe(second, "distinct resource pairs must never share an ACL cache key");
    }

    [Fact]
    public void CacheKeys_AreStableForEqualResources()
    {
        var userId = Guid.NewGuid();
        var subject = AclSubject.ForUser(userId, [Guid.NewGuid()]);
        var tenantId = Guid.NewGuid();

        AclCacheKeys.BuildUserCacheKey(userId, tenantId, "a:b", "c", 1, 2, 3)
            .Should().Be(AclCacheKeys.BuildUserCacheKey(userId, tenantId, "a:b", "c", 1, 2, 3));
        AclCacheKeys.BuildSubjectCacheKey(subject, tenantId, "a:b", "c", 1, 2, 3)
            .Should().Be(AclCacheKeys.BuildSubjectCacheKey(subject, tenantId, "a:b", "c", 1, 2, 3));
    }

    [Fact]
    public async Task EvaluateAccessAsync_DelimiterBearingResourcesCacheIndependently()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inner = new Mock<IAccessControlListService>();
        inner.SetupSequence(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, "a:b", "c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read)
            .ReturnsAsync(AccessLevel.Write);
        inner.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, "a", "b:c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Admin);

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var (service, _) = CreateCachedService(inner.Object, memoryCache, tenantId, userId);
        var subject = AclSubject.ForUser(userId);

        (await service.EvaluateAccessAsync(subject, tenantId, "a:b", "c")).Should().Be(AccessLevel.Read);
        (await service.EvaluateAccessAsync(subject, tenantId, "a", "b:c")).Should().Be(AccessLevel.Admin,
            "the delimiter-adjacent resource must not read the first resource's cached decision");
        (await service.EvaluateAccessAsync(subject, tenantId, "a:b", "c")).Should().Be(AccessLevel.Read,
            "each delimiter-bearing resource keeps its own cached decision");

        inner.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, "a:b", "c", It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, "a", "b:c", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccessLevelAsync_DelimiterBearingResourcesCacheIndependently()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(service => service.GetAccessLevelAsync(
                userId, tenantId, "a:b", "c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);
        inner.Setup(service => service.GetAccessLevelAsync(
                userId, tenantId, "a", "b:c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Admin);

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var (service, _) = CreateCachedService(inner.Object, memoryCache, tenantId, userId);

        (await service.GetAccessLevelAsync(userId, tenantId, "a:b", "c")).Should().Be(AccessLevel.Read);
        (await service.GetAccessLevelAsync(userId, tenantId, "a", "b:c")).Should().Be(AccessLevel.Admin,
            "the delimiter-adjacent resource must not read the first resource's cached decision");

        inner.Verify(service => service.GetAccessLevelAsync(
            userId, tenantId, "a:b", "c", It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(service => service.GetAccessLevelAsync(
            userId, tenantId, "a", "b:c", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResourceInvalidation_EvictsDelimiterBearingResourceEntries()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object);
        var hybrid = new HybridPermissionCache(
            memoryCache,
            Options.Create(new AuthorizationCacheOptions()),
            metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore.Setup(store => store.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var invalidation = new CacheInvalidationService(
            memoryCache,
            versionStore.Object,
            hybrid,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true }),
            NullLogger<CacheInvalidationService>.Instance,
            invalidationPublisher: null,
            keyTracker: tracker);

        var key = AclCacheKeys.BuildSubjectCacheKey(
            AclSubject.ForUser(userId, roleIds: [roleId]), tenantId, "a:b", "c", 0, 0, 0);
        await hybrid.SetValueAsync(key, new CachedAclDecision(AccessLevel.Write, null), "acl");

        await invalidation.InvalidateBatchAsync(
            tenantId,
            [new CacheInvalidationTarget(CacheInvalidationTargetType.Resource, ResourceType: "a:b", ResourceId: "c")]);

        (await hybrid.GetValueAsync<CachedAclDecision>(key, "acl")).Found.Should().BeFalse(
            "resource invalidation must match fingerprinted segments of delimiter-bearing resources");
    }

    private static (CachedAccessControlListService Service, Mock<ITenantSecurityVersionStore> TenantVersions) CreateCachedService(
        IAccessControlListService inner,
        IMemoryCache memoryCache,
        Guid tenantId,
        Guid userId)
    {
        var tenantVersions = new Mock<ITenantSecurityVersionStore>();
        tenantVersions
            .Setup(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, 1L));
        var userVersions = new Mock<IUserSecurityVersionStore>();
        userVersions
            .Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        var service = new CachedAccessControlListService(
            inner,
            memoryCache,
            tenantVersions.Object,
            userVersions.Object,
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }));
        return (service, tenantVersions);
    }
}
