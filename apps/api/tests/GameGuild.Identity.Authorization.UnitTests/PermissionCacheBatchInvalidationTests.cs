using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class PermissionCacheBatchInvalidationTests
{
    [Fact]
    public async Task CachedAclEntry_ExpiresUsingTheAclSpecificTtl()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantVersions = new Mock<ITenantSecurityVersionStore>();
        tenantVersions.Setup(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, 1L));
        var userVersions = new Mock<IUserSecurityVersionStore>();
        userVersions.Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var options = new AuthorizationCacheOptions
        {
            AccessControlListTtlSeconds = 2,
            PermissionTtlSeconds = 60,
            DistributedCacheTtlSeconds = 120
        };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = Mock.Of<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics);
        var hybridCache = new HybridPermissionCache(
            memoryCache,
            Options.Create(options),
            metrics,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, "Document", "doc-ttl", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);
        var service = new CachedAccessControlListService(
            inner.Object,
            memoryCache,
            tenantVersions.Object,
            userVersions.Object,
            Options.Create(options),
            hybridCache,
            metrics,
            keyTracker: tracker,
            invalidationService: null);
        var subject = AclSubject.ForUser(userId, [], []);

        (await service.EvaluateAccessAsync(subject, tenantId, "Document", "doc-ttl"))
            .Should().Be(AccessLevel.Read);
        await Task.Delay(TimeSpan.FromMilliseconds(2_200));
        (await service.EvaluateAccessAsync(subject, tenantId, "Document", "doc-ttl"))
            .Should().Be(AccessLevel.Read);

        inner.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, "Document", "doc-ttl", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CachedAclTenantInvalidation_RemovesL1AndL2Entries()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantVersions = new Mock<ITenantSecurityVersionStore>();
        tenantVersions.Setup(store => store.GetVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var userVersions = new Mock<IUserSecurityVersionStore>();
        userVersions.Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var hybridCache = new Mock<IHybridPermissionCache>();
        hybridCache.Setup(cache => cache.GetValueAsync<CachedAclDecision>(
                It.IsAny<string>(), "acl", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CacheResult<CachedAclDecision>.Miss());
        hybridCache.Setup(cache => cache.SetValueAsync(
                It.IsAny<string>(), It.IsAny<CachedAclDecision>(), "acl", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        hybridCache.Setup(cache => cache.RemoveAsync(
                It.IsAny<string>(), "acl", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(service => service.GetAccessLevelAsync(
                userId, tenantId, "Document", "doc-7", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);
        var service = new CachedAccessControlListService(
            inner.Object,
            new MemoryCache(new MemoryCacheOptions()),
            tenantVersions.Object,
            userVersions.Object,
            Options.Create(new AuthorizationCacheOptions()),
            hybridCache.Object);

        (await service.GetAccessLevelAsync(userId, tenantId, "Document", "doc-7"))
            .Should().Be(AccessLevel.Read);

        await service.InvalidateTenantAsync(tenantId.ToString());

        hybridCache.Verify(cache => cache.RemoveAsync(
            It.Is<string>(key => key.Contains($"acl:{tenantId}:{userId}:", StringComparison.Ordinal)),
            "acl",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CachedAclRoleGrant_PublishesResourceAndRoleDependencyInvalidation()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(service => service.GrantAccessAsync(
                It.IsAny<Guid>(), AclPrincipalType.Role, roleId, tenantId, "Document", "doc-9", AccessLevel.Read,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var invalidation = new Mock<ICacheInvalidationService>();
        var service = new CachedAccessControlListService(
            inner.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITenantSecurityVersionStore>(),
            Mock.Of<IUserSecurityVersionStore>(),
            Options.Create(new AuthorizationCacheOptions()),
            invalidationService: invalidation.Object);

        await service.GrantAccessAsync(
            Guid.NewGuid(), AclPrincipalType.Role, roleId, tenantId, "Document", "doc-9", AccessLevel.Read);

        invalidation.Verify(cache => cache.InvalidateBatchAsync(
            tenantId,
            It.Is<IReadOnlyCollection<CacheInvalidationTarget>>(targets =>
                targets.Count == 2 &&
                targets.Any(target => target.Type == CacheInvalidationTargetType.Resource &&
                                       target.ResourceType == "Document" && target.ResourceId == "doc-9") &&
                targets.Any(target => target.Type == CacheInvalidationTargetType.Dependency &&
                                       target.DependencyKind == "role" && target.DependencyId == roleId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateBatchAsync_EvictsResourceAndRoleDependentsAndPublishesOneVersionedBatch()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object);
        var cache = new HybridPermissionCache(
            memoryCache,
            Options.Create(new AuthorizationCacheOptions()),
            metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore.Setup(store => store.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        CacheInvalidationEvent? publishedEvent = null;
        var publisher = new Mock<ICacheInvalidationPublisher>();
        publisher.Setup(service => service.PublishAsync(It.IsAny<CacheInvalidationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CacheInvalidationEvent, CancellationToken>((value, _) => publishedEvent = value)
            .Returns(Task.CompletedTask);
        var invalidation = new CacheInvalidationService(
            memoryCache,
            versionStore.Object,
            cache,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true, UsePubSubInvalidation = true }),
            NullLogger<CacheInvalidationService>.Instance,
            publisher.Object,
            tracker);

        var documentOne = AclCacheKeys.BuildUserCacheKey(userId, tenantId, "Document", "doc-1", 0, 0, 0);
        var documentTwo = AclCacheKeys.BuildSubjectCacheKey(
            AclSubject.ForUser(userId, roleIds: [roleId]), tenantId, "Document", "doc-2", 0, 0, 0);
        var otherResource = AclCacheKeys.BuildSubjectCacheKey(
            AclSubject.ForUser(userId, roleIds: [roleId]), tenantId, "Project", "project-1", 0, 0, 0);
        await cache.SetValueAsync(documentOne, AccessLevel.Write, "acl");
        await cache.SetValueAsync(documentTwo, AccessLevel.Read, "acl");
        await cache.SetValueAsync(otherResource, AccessLevel.Read, "acl");

        await invalidation.InvalidateBatchAsync(tenantId,
        [
            new CacheInvalidationTarget(CacheInvalidationTargetType.Resource, ResourceType: "Document", ResourceId: "doc-1"),
            new CacheInvalidationTarget(CacheInvalidationTargetType.Resource, ResourceType: "Document", ResourceId: "doc-2"),
            new CacheInvalidationTarget(CacheInvalidationTargetType.Dependency, DependencyKind: "role", DependencyId: roleId)
        ]);

        versionStore.Verify(store => store.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(service => service.PublishAsync(It.IsAny<CacheInvalidationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Type.Should().Be(CacheInvalidationType.Batch);
        publishedEvent.TenantId.Should().Be(tenantId);
        publishedEvent.Targets.Should().HaveCount(3);
        (await cache.GetValueAsync<AccessLevel>(documentOne, "acl")).Found.Should().BeFalse();
        (await cache.GetValueAsync<AccessLevel>(documentTwo, "acl")).Found.Should().BeFalse();
        (await cache.GetValueAsync<AccessLevel>(otherResource, "acl")).Found.Should().BeFalse(
            "a role dependency change invalidates this role-derived ACL entry as well");
    }

    [Fact]
    public async Task HandleInvalidationEvent_BatchFromAnotherInstanceEvictsSharedDependencyEntries()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object);
        var cache = new HybridPermissionCache(
            memoryCache,
            Options.Create(new AuthorizationCacheOptions()),
            metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var invalidation = new CacheInvalidationService(
            memoryCache,
            Mock.Of<ITenantSecurityVersionStore>(),
            cache,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions()),
            NullLogger<CacheInvalidationService>.Instance,
            keyTracker: tracker);
        var key = AclCacheKeys.BuildSubjectCacheKey(
            AclSubject.Anonymous with { RoleIds = [roleId] }, tenantId, "Document", "doc-3", 0, 0, 0);
        await cache.SetValueAsync(key, AccessLevel.Read, "acl");

        invalidation.HandleInvalidationEvent(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Batch,
            TenantId = tenantId,
            OriginInstanceId = "remote-node",
            Targets = [new CacheInvalidationTarget(CacheInvalidationTargetType.Dependency, DependencyKind: "role", DependencyId: roleId)]
        });

        (await cache.GetValueAsync<AccessLevel>(key, "acl")).Found.Should().BeFalse();
    }

    [Fact]
    public async Task HandleInvalidationEvent_GroupDependencyFromAnotherInstanceEvictsSharedEntries()
    {
        var tenantId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object);
        var cache = new HybridPermissionCache(
            memoryCache,
            Options.Create(new AuthorizationCacheOptions()),
            metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var invalidation = new CacheInvalidationService(
            memoryCache,
            Mock.Of<ITenantSecurityVersionStore>(),
            cache,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions()),
            NullLogger<CacheInvalidationService>.Instance,
            keyTracker: tracker);
        var key = AclCacheKeys.BuildSubjectCacheKey(
            AclSubject.Anonymous with { GroupIds = [groupId] }, tenantId, "Document", "doc-group", 0, 0, 0);
        await cache.SetValueAsync(key, AccessLevel.Read, "acl");

        invalidation.HandleInvalidationEvent(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Batch,
            TenantId = tenantId,
            OriginInstanceId = "remote-node",
            Targets = [new CacheInvalidationTarget(CacheInvalidationTargetType.Dependency, DependencyKind: "group", DependencyId: groupId)]
        });

        (await cache.GetValueAsync<AccessLevel>(key, "acl")).Found.Should().BeFalse();
    }

    [Fact]
    public async Task GlobalAclInvalidation_ChangesL1AndL2KeysForEveryTenant()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var versions = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore.Setup(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult((
                versions.GetValueOrDefault(tenantId.ToString()),
                versions.GetValueOrDefault(Guid.Empty.ToString()))));
        versionStore.Setup(store => store.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string scope, CancellationToken _) => versions.GetValueOrDefault(scope));
        versionStore.Setup(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string scope, CancellationToken _) => versions.AddOrUpdate(scope, 1, static (_, current) => current + 1));
        var userVersionStore = new Mock<IUserSecurityVersionStore>();
        userVersionStore.Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var observedKeys = new List<string>();
        var hybridCache = new Mock<IHybridPermissionCache>();
        hybridCache.Setup(cache => cache.GetValueAsync<CachedAclDecision>(
                It.IsAny<string>(), "acl", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((key, _, _) => observedKeys.Add(key))
            .ReturnsAsync(CacheResult<CachedAclDecision>.Miss());
        hybridCache.Setup(cache => cache.SetValueAsync(
                It.IsAny<string>(), It.IsAny<CachedAclDecision>(), "acl", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var inner = new Mock<IAccessControlListService>();
        inner.SetupSequence(service => service.GetAccessLevelAsync(
                userId, tenantId, "Document", "doc-global", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read)
            .ReturnsAsync(AccessLevel.None);
        var cache = new CachedAccessControlListService(
            inner.Object,
            new MemoryCache(new MemoryCacheOptions()),
            versionStore.Object,
            userVersionStore.Object,
            Options.Create(new AuthorizationCacheOptions()),
            hybridCache.Object);

        (await cache.GetAccessLevelAsync(userId, tenantId, "Document", "doc-global")).Should().Be(AccessLevel.Read);
        await versionStore.Object.IncrementVersionAsync(Guid.Empty.ToString());
        (await cache.GetAccessLevelAsync(userId, tenantId, "Document", "doc-global")).Should().Be(AccessLevel.None);
        observedKeys.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        observedKeys[0].Should().EndWith(":gv0");
        observedKeys[1].Should().EndWith(":gv1");
        inner.Verify(service => service.GetAccessLevelAsync(
            userId, tenantId, "Document", "doc-global", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DatabaseVersionStore_ReadsTenantAndGlobalVersionsInOneRepositoryCall()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantSecurityVersionRepository>();
        repository.Setup(store => store.GetVersionsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(tenantId) && ids.Contains(Guid.Empty)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, long> { [tenantId] = 8, [Guid.Empty] = 3 });
        var versionStore = new DatabaseTenantSecurityVersionStore(repository.Object);

        var versions = await versionStore.GetTenantAndGlobalVersionsAsync(tenantId);

        versions.TenantVersion.Should().Be(8);
        versions.GlobalVersion.Should().Be(3);
        repository.Verify(store => store.GetVersionsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GlobalInvalidation_AdvancesReservedVersionAndPublishesGlobalEvent()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object);
        var hybridCache = new HybridPermissionCache(
            memoryCache,
            Options.Create(new AuthorizationCacheOptions()),
            metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore.Setup(store => store.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        CacheInvalidationEvent? publishedEvent = null;
        var publisher = new Mock<ICacheInvalidationPublisher>();
        publisher.Setup(service => service.PublishAsync(It.IsAny<CacheInvalidationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CacheInvalidationEvent, CancellationToken>((value, _) => publishedEvent = value)
            .Returns(Task.CompletedTask);
        var invalidation = new CacheInvalidationService(
            memoryCache,
            versionStore.Object,
            hybridCache,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions { UseDistributedCache = true, UsePubSubInvalidation = true }),
            NullLogger<CacheInvalidationService>.Instance,
            publisher.Object,
            tracker);

        await invalidation.InvalidateGlobalAsync();

        versionStore.Verify(store => store.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Type.Should().Be(CacheInvalidationType.Global);
        publishedEvent.TenantId.Should().Be(Guid.Empty);
        publisher.Verify(service => service.PublishAsync(It.IsAny<CacheInvalidationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GlobalInvalidationEvent_EvictsAclEntriesAcrossTenants()
    {
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object);
        var hybridCache = new HybridPermissionCache(
            memoryCache,
            Options.Create(new AuthorizationCacheOptions()),
            metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            keyTracker: tracker);
        var invalidation = new CacheInvalidationService(
            memoryCache,
            Mock.Of<ITenantSecurityVersionStore>(),
            hybridCache,
            metrics.Object,
            Options.Create(new AuthorizationCacheOptions()),
            NullLogger<CacheInvalidationService>.Instance,
            keyTracker: tracker);
        var firstAclKey = $"acl:{firstTenant}:user-a:Document:doc-1:tv0:uv0:gv0";
        var secondAclKey = $"acl:subj:{secondTenant}:user-b:role-a:ng:Document:doc-2:tv0:uv0:gv0";
        const string policyKey = "policy:tenant:documents.read:v1";
        await hybridCache.SetValueAsync(firstAclKey, AccessLevel.Read, "acl");
        await hybridCache.SetValueAsync(secondAclKey, AccessLevel.Write, "acl");
        await hybridCache.SetValueAsync(policyKey, 1, "policy");

        invalidation.HandleInvalidationEvent(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Global,
            TenantId = Guid.Empty,
            OriginInstanceId = "remote-instance"
        });

        (await hybridCache.GetValueAsync<AccessLevel>(firstAclKey, "acl")).Found.Should().BeFalse();
        (await hybridCache.GetValueAsync<AccessLevel>(secondAclKey, "acl")).Found.Should().BeFalse();
        (await hybridCache.GetValueAsync<int>(policyKey, "policy")).Found.Should().BeTrue();
    }
}
