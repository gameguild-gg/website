using System.Collections.Concurrent;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class PermissionServiceBulkCacheTests
{
    [Fact]
    public async Task BulkChecksReuseCachedDecisionsAndTenantVersionChangesInvalidateThem()
    {
        var options = new DbContextOptionsBuilder<PermissionServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new PermissionServiceDbContext(options);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        context.Set<TenantPermission>().Add(new TenantPermission
        {
            UserId = userId,
            TenantId = tenantId,
            Permissions = [nameof(PermissionType.Read), nameof(PermissionType.Edit)]
        });
        await context.SaveChangesAsync();

        var versionStore = new InMemoryTenantSecurityVersionStore();
        var cachedValues = new ConcurrentDictionary<string, CachedPermissionDecision>(StringComparer.Ordinal);
        var cache = CreatePermissionCache(cachedValues);
        var service = PermissionMutationTestServices.Create(
            context,
            versionStore: versionStore,
            cache: cache.Object);
        var firstResult = await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]);
        var cachedResult = await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]);
        firstResult[userId][PermissionType.Read].Should().BeTrue();
        cachedResult[userId][PermissionType.Read].Should().BeTrue();
        cache.Verify(
            permissionCache => permissionCache.SetManyValuesAsync(
                It.IsAny<IReadOnlyDictionary<string, CachedPermissionDecision>>(),
                "permission",
                It.IsAny<IReadOnlyDictionary<string, int>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await service.RevokeTenantPermissionAsync(userId, tenantId, [PermissionType.Read]);

        var invalidatedResult = await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]);
        var cachedInvalidatedResult = await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]);
        invalidatedResult[userId][PermissionType.Read].Should().BeFalse();
        cachedInvalidatedResult[userId][PermissionType.Read].Should().BeFalse();
        cache.Verify(
            permissionCache => permissionCache.GetManyValuesAsync<CachedPermissionDecision>(
                It.IsAny<IReadOnlyCollection<string>>(),
                "permission",
                It.IsAny<CancellationToken>()),
            Times.Exactly(4));
        cache.Verify(
            permissionCache => permissionCache.SetManyValuesAsync(
                It.IsAny<IReadOnlyDictionary<string, CachedPermissionDecision>>(),
                "permission",
                It.IsAny<IReadOnlyDictionary<string, int>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BulkChecksUseGlobalVersionToInvalidateCachedDefaultsAcrossTenants()
    {
        var options = new DbContextOptionsBuilder<PermissionServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new PermissionServiceDbContext(options);
        context.Set<TenantPermission>().Add(new TenantPermission
        {
            UserId = null,
            TenantId = null,
            Permissions = [nameof(PermissionType.Read)]
        });
        await context.SaveChangesAsync();

        var versionStore = new InMemoryTenantSecurityVersionStore();
        var cache = CreatePermissionCache(new ConcurrentDictionary<string, CachedPermissionDecision>(StringComparer.Ordinal));
        var service = PermissionMutationTestServices.Create(
            context,
            versionStore: versionStore,
            cache: cache.Object);
        var requests = new[]
        {
            new BulkPermissionCheckRequest(Guid.NewGuid(), Guid.NewGuid(), PermissionType.Read),
            new BulkPermissionCheckRequest(Guid.NewGuid(), Guid.NewGuid(), PermissionType.Read)
        };

        (await service.BulkCheckPermissionsAsync(requests)).Select(result => result.IsGranted).Should().Equal(true, true);
        (await service.BulkCheckPermissionsAsync(requests)).Select(result => result.IsGranted).Should().Equal(true, true);

        await service.SetGlobalDefaultPermissionsAsync([]);

        (await service.BulkCheckPermissionsAsync(requests)).Select(result => result.IsGranted).Should().Equal(false, false);
    }

    [Fact]
    public async Task BulkChecksRetryWhenSecurityVersionChangesDuringDatabaseEvaluation()
    {
        var options = new DbContextOptionsBuilder<PermissionServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new PermissionServiceDbContext(options);
        var tenantId = Guid.NewGuid();
        var grant = new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Permissions = [nameof(PermissionType.Read)]
        };
        context.Set<TenantPermission>().Add(grant);
        await context.SaveChangesAsync();

        InMemoryTenantSecurityVersionStore? versionStore = null;
        versionStore = new InMemoryTenantSecurityVersionStore(snapshotNumber =>
        {
            if (snapshotNumber != 2)
            {
                return;
            }

            grant.IsActive = false;
            context.SaveChanges();
            versionStore!.SetVersion(tenantId, 2);
        });
        versionStore.SetVersion(tenantId, 1);
        var cache = CreatePermissionCache(new ConcurrentDictionary<string, CachedPermissionDecision>(StringComparer.Ordinal));
        var service = new PermissionService(
            context,
            securityVersionStore: versionStore,
            hybridPermissionCache: cache.Object);
        var request = new BulkPermissionCheckRequest(grant.UserId!.Value, tenantId, PermissionType.Read);

        var result = await service.BulkCheckPermissionsAsync(new[] { request });

        result.Single().IsGranted.Should().BeFalse();
        cache.Verify(
            permissionCache => permissionCache.GetManyValuesAsync<CachedPermissionDecision>(
                It.IsAny<IReadOnlyCollection<string>>(),
                "permission",
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        cache.Verify(
            permissionCache => permissionCache.SetManyValuesAsync(
                It.IsAny<IReadOnlyDictionary<string, CachedPermissionDecision>>(),
                "permission",
                It.IsAny<IReadOnlyDictionary<string, int>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IHybridPermissionCache> CreatePermissionCache(ConcurrentDictionary<string, CachedPermissionDecision> values)
    {
        var cache = new Mock<IHybridPermissionCache>(MockBehavior.Strict);
        cache
            .Setup(permissionCache => permissionCache.GetManyValuesAsync<CachedPermissionDecision>(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<string> keys, string _, CancellationToken _) =>
            {
                IReadOnlyDictionary<string, CacheResult<CachedPermissionDecision>> results = keys
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(
                        key => key,
                        key => values.TryGetValue(key, out var value)
                            ? CacheResult<CachedPermissionDecision>.Hit(value)
                            : CacheResult<CachedPermissionDecision>.Miss(),
                        StringComparer.Ordinal);
                return Task.FromResult(results);
            });
        cache
            .Setup(permissionCache => permissionCache.SetManyValuesAsync(
                It.IsAny<IReadOnlyDictionary<string, CachedPermissionDecision>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, int>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyDictionary<string, CachedPermissionDecision> entries, string _, IReadOnlyDictionary<string, int>? _, CancellationToken _) =>
            {
                foreach (var entry in entries)
                {
                    values[entry.Key] = entry.Value;
                }

                return Task.CompletedTask;
            });

        return cache;
    }

    private sealed class InMemoryTenantSecurityVersionStore : ITenantSecurityVersionStore
    {
        private readonly ConcurrentDictionary<Guid, long> _versions = new();
        private readonly Action<int>? _onVersionSnapshot;
        private int _snapshotCount;

        public InMemoryTenantSecurityVersionStore(Action<int>? onVersionSnapshot = null)
        {
            _onVersionSnapshot = onVersionSnapshot;
        }

        public Task<long> GetVersionAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Guid.TryParse(tenantId, out var parsedTenantId)
                ? Task.FromResult(_versions.GetValueOrDefault(parsedTenantId))
                : Task.FromResult(0L);
        }

        public Task<long> IncrementVersionAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(tenantId, out var parsedTenantId))
            {
                return Task.FromResult(0L);
            }

            return Task.FromResult(_versions.AddOrUpdate(parsedTenantId, 1, (_, version) => version + 1));
        }

        public Task<(long TenantVersion, long GlobalVersion)> GetTenantAndGlobalVersionsAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _onVersionSnapshot?.Invoke(Interlocked.Increment(ref _snapshotCount));
            var tenantVersion = _versions.GetValueOrDefault(tenantId);
            var globalVersion = tenantId == Guid.Empty ? tenantVersion : _versions.GetValueOrDefault(Guid.Empty);
            return Task.FromResult((tenantVersion, globalVersion));
        }

        public void SetVersion(Guid tenantId, long version)
        {
            _versions[tenantId] = version;
        }
    }

    private sealed class PermissionServiceDbContext(DbContextOptions<PermissionServiceDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantPermission>().Ignore(permission => permission.Metadata);
            modelBuilder.Entity<ContentTypePermission>();
            modelBuilder.Entity<GenericResourcePermission>();
        }
    }
}
