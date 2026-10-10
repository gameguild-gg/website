using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace GameGuild.Identity.Authorization.IntegrationTests;

[CollectionDefinition("Permission cache Redis")]
public sealed class PermissionCacheRedisCollection : ICollectionFixture<PermissionCacheRedisFixture>
{
}

public sealed class PermissionCacheRedisFixture : IAsyncLifetime
{
    private readonly int _hostPort = GetAvailablePort();
    private readonly IContainer _container;

    public PermissionCacheRedisFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(_hostPort, 6379)
            .WithCleanUp(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(["redis-cli", "ping"]))
            .Build();
    }

    public string ConnectionString => $"{_container.Hostname}:{_container.GetMappedPublicPort(6379)}";
    public bool IsRedisRunning => _container.State == TestcontainersStates.Running;

    public Task InitializeAsync() => _container.StartAsync();

    public Task StopRedisAsync() => _container.StopAsync();

    public Task StartRedisAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[Collection("Permission cache Redis")]
[Trait("Category", "Integration")]
[Trait("Infrastructure", "Redis")]
public sealed class PermissionCacheRedisIntegrationTests(PermissionCacheRedisFixture fixture)
{
    [Theory]
    [InlineData(AccessLevel.Write, AccessLevel.None)]
    [InlineData(AccessLevel.None, AccessLevel.Write)]
    public async Task ConcurrentAclLookupRetriesWhenSharedSecurityVersionChanges(
        AccessLevel permissionBeforeMutation,
        AccessLevel permissionAfterMutation)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string resourceType = "Project";
        const string resourceId = "concurrent-security-version";
        var tenantVersions = new SharedTenantSecurityVersionStore();
        var userVersions = new SharedUserSecurityVersionStore();
        var redisPrefix = $"gg:auth:concurrent-version:{Guid.NewGuid():N}:";
        var invalidationChannel = $"gg:auth:concurrent-version:{Guid.NewGuid():N}";
        await using var instanceA = CreateCacheInstance(
            fixture.ConnectionString, redisPrefix, invalidationChannel, tenantVersions, userVersions);
        await using var instanceB = CreateCacheInstance(
            fixture.ConnectionString, redisPrefix, invalidationChannel, tenantVersions, userVersions);
        using var scopeA = instanceA.CreateScope();
        using var scopeB = instanceB.CreateScope();

        var initialLookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialLookup = new TaskCompletionSource<AccessLevel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupCount = 0;
        var currentDatabaseAccess = permissionAfterMutation;
        var databaseAclB = new Mock<IAccessControlListService>();
        databaseAclB.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref lookupCount) == 1
                ? WaitForInitialLookupAsync()
                : Task.FromResult(currentDatabaseAccess));

        var aclCacheB = CreateAclCache(
            scopeB.ServiceProvider,
            databaseAclB.Object,
            tenantVersions,
            userVersions);
        var subject = AclSubject.ForUser(userId);

        var concurrentDecision = aclCacheB.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId);
        await initialLookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        currentDatabaseAccess = permissionAfterMutation;
        await scopeA.ServiceProvider.GetRequiredService<ICacheInvalidationService>()
            .InvalidateResourceAsync(tenantId, resourceType, resourceId);
        releaseInitialLookup.SetResult(permissionBeforeMutation);

        (await concurrentDecision.WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be(permissionAfterMutation,
                "a permission lookup that overlaps an invalidation must retry with the new security version");
        lookupCount.Should().Be(2, "the stale database result must be discarded and re-evaluated");

        (await aclCacheB.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId))
            .Should().Be(permissionAfterMutation);
        lookupCount.Should().Be(2, "the stable decision should be cached under the current security version");

        async Task<AccessLevel> WaitForInitialLookupAsync()
        {
            initialLookupStarted.TrySetResult();
            return await releaseInitialLookup.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task SharedL2AndTenantVersionPreventStaleAclGrantsAcrossInstances()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string resourceType = "Project";
        const string resourceId = "project-42";
        var tenantVersions = new SharedTenantSecurityVersionStore();
        var userVersions = new SharedUserSecurityVersionStore();
        var invalidationChannel = $"gg:auth:invalidate:{Guid.NewGuid():N}";
        var redisPrefix = $"gg:auth:integration:{Guid.NewGuid():N}:";
        await using var instanceA = CreateCacheInstance(fixture.ConnectionString, redisPrefix, invalidationChannel, tenantVersions, userVersions);
        await using var instanceB = CreateCacheInstance(fixture.ConnectionString, redisPrefix, invalidationChannel, tenantVersions, userVersions);
        using var scopeA = instanceA.CreateScope();
        using var scopeB = instanceB.CreateScope();

        var databaseAclA = new Mock<IAccessControlListService>();
        databaseAclA.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Write);

        var currentDatabaseAccessOnB = AccessLevel.None;
        var databaseAclB = new Mock<IAccessControlListService>();
        databaseAclB.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(currentDatabaseAccessOnB));

        var aclCacheA = CreateAclCache(scopeA.ServiceProvider, databaseAclA.Object, tenantVersions, userVersions);
        var aclCacheB = CreateAclCache(scopeB.ServiceProvider, databaseAclB.Object, tenantVersions, userVersions);
        var subject = AclSubject.ForUser(userId);

        (await aclCacheA.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId))
            .Should().Be(AccessLevel.Write);
        (await aclCacheB.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId))
            .Should().Be(AccessLevel.Write);
        databaseAclB.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()), Times.Never);

        await scopeA.ServiceProvider.GetRequiredService<ICacheInvalidationService>()
            .InvalidateResourceAsync(tenantId, resourceType, resourceId);
        tenantVersions.GetVersion(tenantId.ToString()).Should().Be(1);

        (await aclCacheB.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId))
            .Should().Be(AccessLevel.None);
        databaseAclB.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RedisPubSubSubscriberDispatchesRemoteInvalidationToItsInstance()
    {
        await using var publisherConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        await using var subscriberConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var channel = $"gg:auth:subscriber-test:{Guid.NewGuid():N}";
        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            UsePubSubInvalidation = true,
            InvalidationChannelName = channel
        });
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cacheKeys = new[]
        {
            $"perm:{tenantId}:{userId}:v0",
            AclCacheKeys.BuildUserCacheKey(userId, tenantId, "Project", "project-42", 0, 0, 0),
            AclCacheKeys.BuildSubjectCacheKey(AclSubject.ForUser(userId), tenantId, "Project", "project-42", 0, 0, 0)
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<ITenantSecurityVersionStore, SharedTenantSecurityVersionStore>();
        services.AddAuthorizationCaching();
        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var cacheScope = serviceProvider.CreateScope();
        var cache = cacheScope.ServiceProvider.GetRequiredService<IHybridPermissionCache>();
        foreach (var key in cacheKeys)
        {
            await cache.SetAsync(key, "cached-permission", key.StartsWith("perm:", StringComparison.Ordinal) ? "permission" : "acl");
        }

        var subscriber = new RedisPermissionCacheInvalidationSubscriber(
            subscriberConnection,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<RedisPermissionCacheInvalidationSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);
        try
        {
            var expected = new CacheInvalidationEvent
            {
                Type = CacheInvalidationType.User,
                TenantId = tenantId,
                UserId = userId,
                OriginInstanceId = "remote-instance-a"
            };
            var publisher = new RedisPermissionCacheInvalidationPublisher(
                publisherConnection,
                options,
                NullLogger<RedisPermissionCacheInvalidationPublisher>.Instance);
            var timeout = DateTime.UtcNow.AddSeconds(5);
            var invalidated = false;
            while (!invalidated && DateTime.UtcNow < timeout)
            {
                await publisher.PublishAsync(expected);
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                invalidated = true;
                foreach (var key in cacheKeys)
                {
                    invalidated &= await cache.GetAsync<string>(key, "permission") is null;
                }
            }

            invalidated.Should().BeTrue("a remote Redis event should evict permission and ACL L1 entries from another request scope");
        }
        finally
        {
            await subscriber.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RedisPubSubBatchInvalidationEvictsInheritedRoleAndGroupDependenciesAcrossScopes()
    {
        await using var publisherConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        await using var subscriberConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var channel = $"gg:auth:batch-subscriber-test:{Guid.NewGuid():N}";
        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            UsePubSubInvalidation = true,
            InvalidationChannelName = channel
        });
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var inheritedRoleId = Guid.NewGuid();
        var otherRoleId = Guid.NewGuid();
        var inheritedGroupId = Guid.NewGuid();
        var unrelatedGroupId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var invalidatedKeys = new[]
        {
            AclCacheKeys.BuildSubjectCacheKey(
                AclSubject.Anonymous with { RoleIds = [roleId, inheritedRoleId] }, tenantId, "Document", "doc-1", 0, 0, 0),
            AclCacheKeys.BuildSubjectCacheKey(
                AclSubject.ForUser(memberUserId, roleIds: [roleId, inheritedRoleId]), tenantId, "Project", "project-1", 0, 0, 0),
            AclCacheKeys.BuildSubjectCacheKey(
                AclSubject.ForUser(memberUserId, groupIds: [inheritedGroupId, unrelatedGroupId]), tenantId, "Project", "project-2", 0, 0, 0)
        };
        var unaffectedKey = AclCacheKeys.BuildSubjectCacheKey(
            AclSubject.Anonymous with { RoleIds = [otherRoleId] }, tenantId, "Document", "doc-1", 0, 0, 0);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<ITenantSecurityVersionStore, SharedTenantSecurityVersionStore>();
        services.AddAuthorizationCaching();
        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var writerScope = serviceProvider.CreateScope();
        var cache = writerScope.ServiceProvider.GetRequiredService<IHybridPermissionCache>();
        foreach (var key in invalidatedKeys.Append(unaffectedKey))
        {
            await cache.SetValueAsync(key, AccessLevel.Write, "acl");
        }

        var subscriber = new RedisPermissionCacheInvalidationSubscriber(
            subscriberConnection,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<RedisPermissionCacheInvalidationSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);
        try
        {
            var expected = new CacheInvalidationEvent
            {
                Type = CacheInvalidationType.Batch,
                TenantId = tenantId,
                OriginInstanceId = "remote-role-admin",
                Targets =
                [
                    new(CacheInvalidationTargetType.Dependency, DependencyKind: "role", DependencyId: roleId),
                    new(CacheInvalidationTargetType.Dependency, DependencyKind: "role", DependencyId: inheritedRoleId),
                    new(CacheInvalidationTargetType.Dependency, DependencyKind: "group", DependencyId: inheritedGroupId)
                ]
            };
            var publisher = new RedisPermissionCacheInvalidationPublisher(
                publisherConnection,
                options,
                NullLogger<RedisPermissionCacheInvalidationPublisher>.Instance);
            var timeout = DateTime.UtcNow.AddSeconds(5);
            var batchEvicted = false;
            while (!batchEvicted && DateTime.UtcNow < timeout)
            {
                await publisher.PublishAsync(expected);
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                batchEvicted = true;
                foreach (var key in invalidatedKeys)
                {
                    batchEvicted &= !(await cache.GetValueAsync<AccessLevel>(key, "acl")).Found;
                }
            }

            batchEvicted.Should().BeTrue(
                "a batch event should evict ACL entries carrying direct or inherited role and group dependencies across request scopes");
            (await cache.GetValueAsync<AccessLevel>(unaffectedKey, "acl")).Found.Should().BeTrue(
                "an entry for a different role must survive the batch invalidation");
        }
        finally
        {
            await subscriber.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GroupAclGrantAdvancesSharedVersionAndRefreshesCachedDecisionAcrossInstances()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        const string resourceType = "Project";
        const string resourceId = "project-group-acl";
        var invalidationChannel = $"gg:auth:group-write-test:{Guid.NewGuid():N}";
        var redisPrefix = $"gg:auth:group-write:{Guid.NewGuid():N}:";
        var tenantVersions = new SharedTenantSecurityVersionStore();
        var userVersions = new SharedUserSecurityVersionStore();
        await using var instanceA = CreateCacheInstance(
            fixture.ConnectionString,
            redisPrefix,
            invalidationChannel,
            tenantVersions,
            userVersions);
        await using var instanceB = CreateCacheInstance(
            fixture.ConnectionString,
            redisPrefix,
            invalidationChannel,
            tenantVersions,
            userVersions);
        using var scopeA = instanceA.CreateScope();
        using var scopeB = instanceB.CreateScope();

        var currentDatabaseAccess = AccessLevel.None;
        var databaseAclA = new Mock<IAccessControlListService>();
        databaseAclA.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(currentDatabaseAccess));
        databaseAclA.Setup(service => service.GrantAccessAsync(
                It.IsAny<Guid>(), AclPrincipalType.Group, groupId, tenantId, resourceType, resourceId,
                AccessLevel.Write, It.IsAny<CancellationToken>()))
            .Callback(() => currentDatabaseAccess = AccessLevel.Write)
            .Returns(Task.CompletedTask);

        var databaseAclB = new Mock<IAccessControlListService>();
        databaseAclB.Setup(service => service.EvaluateAccessAsync(
                It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(currentDatabaseAccess));

        var aclCacheA = CreateAclCache(scopeA.ServiceProvider, databaseAclA.Object, tenantVersions, userVersions);
        var aclCacheB = CreateAclCache(scopeB.ServiceProvider, databaseAclB.Object, tenantVersions, userVersions);

        var subject = AclSubject.ForUser(userId, groupIds: [groupId]);
        (await aclCacheB.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId))
            .Should().Be(AccessLevel.None);
        databaseAclB.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()), Times.Once);

        await aclCacheA.GrantAccessAsync(
            Guid.NewGuid(),
            AclPrincipalType.Group,
            groupId,
            tenantId,
            resourceType,
            resourceId,
            AccessLevel.Write);

        tenantVersions.GetVersion(tenantId.ToString()).Should().Be(1);

        var observedAccess = await aclCacheB.EvaluateAccessAsync(subject, tenantId, resourceType, resourceId);
        observedAccess.Should().Be(AccessLevel.Write,
            "a group ACL grant must advance the shared tenant version so another instance cannot reuse its stale decision");
        databaseAclB.Verify(service => service.EvaluateAccessAsync(
            It.IsAny<AclSubject>(), tenantId, resourceType, resourceId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RedisSubscriberResubscribesAfterServerDisconnectAndRestart()
    {
        var connectionString = fixture.ConnectionString;
        await using var subscriberConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        await using var publisherConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriberConnection.ConnectionFailed += (_, _) => disconnected.TrySetResult();
        subscriberConnection.ConnectionRestored += (_, _) => restored.TrySetResult();

        var channel = $"gg:auth:reconnect-test:{Guid.NewGuid():N}";
        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            UsePubSubInvalidation = true,
            InvalidationChannelName = channel
        });
        var publisher = new RedisPermissionCacheInvalidationPublisher(
            publisherConnection,
            options,
            NullLogger<RedisPermissionCacheInvalidationPublisher>.Instance);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cacheKey = AclCacheKeys.BuildUserCacheKey(userId, tenantId, "Project", "project-42", 0, 0, 0);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<ITenantSecurityVersionStore, SharedTenantSecurityVersionStore>();
        services.AddAuthorizationCaching();
        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var cacheScope = serviceProvider.CreateScope();
        var cache = cacheScope.ServiceProvider.GetRequiredService<IHybridPermissionCache>();
        var subscriber = new RedisPermissionCacheInvalidationSubscriber(
            subscriberConnection,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<RedisPermissionCacheInvalidationSubscriber>.Instance);
        await subscriber.StartAsync(CancellationToken.None);
        try
        {
            await cache.SetValueAsync(cacheKey, AccessLevel.Write, "acl");
            await PublishUntilEvictedAsync(publisher, cache, cacheKey, tenantId, userId);

            await cache.SetValueAsync(cacheKey, AccessLevel.Write, "acl");
            await fixture.StopRedisAsync();
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(15));

            await fixture.StartRedisAsync();
            await restored.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cache.SetValueAsync(cacheKey, AccessLevel.Write, "acl");

            await PublishUntilEvictedAsync(publisher, cache, cacheKey, tenantId, userId);
            (await cache.GetValueAsync<AccessLevel>(cacheKey, "acl")).Found.Should().BeFalse(
                "the original subscriber connection should receive invalidations after Redis reconnects");
        }
        finally
        {
            if (!fixture.IsRedisRunning)
            {
                await fixture.StartRedisAsync();
            }

            await subscriber.StopAsync(CancellationToken.None);
        }
    }

    private static async Task PublishUntilEvictedAsync(
        RedisPermissionCacheInvalidationPublisher publisher,
        IHybridPermissionCache cache,
        string cacheKey,
        Guid tenantId,
        Guid userId)
    {
        var invalidation = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.User,
            TenantId = tenantId,
            UserId = userId,
            OriginInstanceId = "remote-instance"
        };
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < timeout)
        {
            await publisher.PublishAsync(invalidation);
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            if (!(await cache.GetValueAsync<AccessLevel>(cacheKey, "acl")).Found)
            {
                return;
            }
        }

        throw new TimeoutException("Redis subscriber did not evict the cached permission entry.");
    }

    private static ServiceProvider CreateCacheInstance(
        string redisConnectionString,
        string redisPrefix,
        string invalidationChannel,
        ITenantSecurityVersionStore tenantVersions,
        IUserSecurityVersionStore userVersions)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton(tenantVersions);
        services.AddSingleton(userVersions);
        services.AddAuthorizationRedisCache(redisConnectionString, redisPrefix);
        services.AddAuthorizationCaching(options =>
        {
            options.PermissionTtlSeconds = 120;
            options.AccessControlListTtlSeconds = 120;
            options.DistributedCacheTtlSeconds = 300;
            options.InvalidationChannelName = invalidationChannel;
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static CachedAccessControlListService CreateAclCache(
        IServiceProvider services,
        IAccessControlListService databaseAcl,
        ITenantSecurityVersionStore tenantVersions,
        IUserSecurityVersionStore userVersions) => new(
        databaseAcl,
        services.GetRequiredService<IMemoryCache>(),
        tenantVersions,
        userVersions,
        services.GetRequiredService<IOptions<AuthorizationCacheOptions>>(),
        services.GetRequiredService<IHybridPermissionCache>(),
        services.GetRequiredService<ICacheMetricsService>(),
        services.GetRequiredService<IPermissionCacheKeyTracker>(),
        services.GetRequiredService<ICacheInvalidationService>());

    private sealed class SharedTenantSecurityVersionStore : ITenantSecurityVersionStore
    {
        private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.Ordinal);

        public Task<long> GetVersionAsync(string tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetVersion(tenantId));
        }

        public Task<long> IncrementVersionAsync(string tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_versions.AddOrUpdate(tenantId, 1, static (_, current) => current + 1));
        }

        public long GetVersion(string tenantId) => _versions.TryGetValue(tenantId, out var version) ? version : 0;
    }

    private sealed class SharedUserSecurityVersionStore : IUserSecurityVersionStore
    {
        private readonly ConcurrentDictionary<Guid, long> _versions = new();

        public Task<long> GetVersionAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_versions.TryGetValue(userId, out var version) ? version : 0);
        }

        public Task<long> IncrementVersionAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_versions.AddOrUpdate(userId, 1, static (_, current) => current + 1));
        }

        public Task IncrementVersionsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var userId in userIds)
            {
                _versions.AddOrUpdate(userId, 1, static (_, current) => current + 1);
            }

            return Task.CompletedTask;
        }
    }
}
