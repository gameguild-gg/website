using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class PermissionCacheWarmupServiceTests
{
    [Fact]
    public async Task WarmAsync_EvaluatesEachDistinctSubjectResourcePairOnce()
    {
        var accessControlList = new Mock<IAccessControlListService>();
        accessControlList.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);
        var service = new PermissionCacheWarmupService(accessControlList.Object);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var requests = new[]
        {
            new PermissionCacheWarmupRequest(
                tenantId, AclSubject.ForUser(userId, roleIds), "Document", "doc-1"),
            new PermissionCacheWarmupRequest(
                tenantId, AclSubject.ForUser(userId, roleIds.Reverse().ToArray()), "Document", "doc-1"),
            new PermissionCacheWarmupRequest(
                tenantId, AclSubject.ForUser(userId, roleIds), "Document", "doc-2")
        };

        var result = await service.WarmAsync(requests);

        result.Should().Be(new PermissionCacheWarmupResult(Requested: 3, Warmed: 2, DuplicatesSkipped: 1));
        accessControlList.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, "Document", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WarmAsync_RejectsInvalidTenantAndOversizedBatches()
    {
        var accessControlList = new Mock<IAccessControlListService>();
        var service = new PermissionCacheWarmupService(accessControlList.Object);
        var invalidTenant = new PermissionCacheWarmupRequest(
            Guid.Empty, AclSubject.ForUser(Guid.NewGuid()), "Document", "doc-1");

        var invalidTenantAct = () => service.WarmAsync([invalidTenant]);
        var oversizedAct = () => service.WarmAsync(Enumerable.Repeat(invalidTenant, 501).ToArray());

        await invalidTenantAct.Should().ThrowAsync<ArgumentException>();
        await oversizedAct.Should().ThrowAsync<ArgumentOutOfRangeException>();
        accessControlList.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WarmAsync_UsesBulkCacheReadsAndWritesForCachedAclService()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var innerService = new Mock<IAccessControlListService>();
        innerService.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, "Document", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);

        var hybridCache = new Mock<IHybridPermissionCache>(MockBehavior.Strict);
        hybridCache
            .Setup(cache => cache.GetManyValuesAsync<CachedAclDecision>(
                It.Is<IReadOnlyCollection<string>>(keys => keys.Count == 2),
                "acl",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CacheResult<CachedAclDecision>>(StringComparer.Ordinal));
        hybridCache
            .Setup(cache => cache.SetManyValuesAsync(
                It.Is<IReadOnlyDictionary<string, CachedAclDecision>>(values => values.Count == 2),
                "acl",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenantVersionStore = new Mock<ITenantSecurityVersionStore>(MockBehavior.Strict);
        tenantVersionStore
            .Setup(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((4L, 2L));
        var userVersionStore = new Mock<IUserSecurityVersionStore>(MockBehavior.Strict);
        userVersionStore
            .Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3L);

        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cachedAcl = new CachedAccessControlListService(
            innerService.Object,
            memory,
            tenantVersionStore.Object,
            userVersionStore.Object,
            Options.Create(new AuthorizationCacheOptions()),
            hybridCache.Object);
        var warmup = new PermissionCacheWarmupService(cachedAcl);
        var roleIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var requests = new[]
        {
            new PermissionCacheWarmupRequest(tenantId, AclSubject.ForUser(userId, roleIds), "Document", "doc-1"),
            new PermissionCacheWarmupRequest(tenantId, AclSubject.ForUser(userId, roleIds.Reverse().ToArray()), "Document", "doc-1"),
            new PermissionCacheWarmupRequest(tenantId, AclSubject.ForUser(userId, roleIds), "Document", "doc-2")
        };

        var result = await warmup.WarmAsync(requests);

        result.Should().Be(new PermissionCacheWarmupResult(Requested: 3, Warmed: 2, DuplicatesSkipped: 1));
        innerService.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, "Document", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        tenantVersionStore.Verify(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        userVersionStore.Verify(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        hybridCache.Verify(cache => cache.GetManyValuesAsync<CachedAclDecision>(
            It.Is<IReadOnlyCollection<string>>(keys => keys.Count == 2), "acl", It.IsAny<CancellationToken>()), Times.Once);
        hybridCache.Verify(cache => cache.SetManyValuesAsync(
            It.Is<IReadOnlyDictionary<string, CachedAclDecision>>(values => values.Count == 2), "acl", It.IsAny<CancellationToken>()), Times.Once);
    }
}
