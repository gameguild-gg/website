using System.Collections.Concurrent;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Security regression tests for the bulk permission cache expiry envelope (issue #309):
///     cached allow decisions must carry the earliest expiration of the grants that produced them,
///     be written with a TTL clamped to that boundary, and be treated as a miss (fail closed) once
///     the boundary has passed — even when no permission mutation advanced the security version.
/// </summary>
public sealed class PermissionServiceBulkCacheExpiryEnvelopeTests
{
    private static readonly DateTime StartTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExpiringGrant_CachedAllowFlipsToDenyWhenGrantExpires()
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
            Permissions = [nameof(PermissionType.Read)],
            ExpiresAt = StartTime.AddSeconds(30)
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(StartTime);
        SystemClock.SetProvider(clock);
        try
        {
            var cache = CreateRecordingPermissionCache(out var writes);
            var service = new PermissionService(
                context,
                securityVersionStore: new InMemoryTenantSecurityVersionStore(),
                hybridPermissionCache: cache.Object);

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue("the grant is effective at evaluation time");
            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue("the still-servable cached allow is reused");
            writes.Should().HaveCount(1, "only the first (cache-miss) call writes");

            // Time passes but no permission mutation occurs: the security version is unchanged, so
            // only the entry's expiry envelope can retire the cached allow.
            clock.Advance(TimeSpan.FromSeconds(31));

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeFalse("a cached allow must not outlive its expiring grant");
            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeFalse("the re-evaluated deny is cached and reused");

            writes.Should().HaveCount(2, "the lapsed allow is re-evaluated and the fresh deny re-cached");
            var writeSnapshots = writes.ToArray();
            writeSnapshots[0].Values.Single().IsGranted.Should().BeTrue();
            writeSnapshots[0].Values.Single().EffectiveUntilUtc.Should().Be(StartTime.AddSeconds(30));
            writeSnapshots[0].TtlSecondsByKey.Should().ContainSingle().Which.Value.Should().Be(30,
                "the cache write TTL must be floored to the grant's remaining lifetime");
            writeSnapshots[1].Values.Single().IsGranted.Should().BeFalse();
            writeSnapshots[1].Values.Single().EffectiveUntilUtc.Should().BeNull(
                "the re-evaluated deny no longer has a participating time-bound grant");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task MultipleParticipatingGrants_EntryTtlIsClampedToEarliestExpiry()
    {
        var options = new DbContextOptionsBuilder<PermissionServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new PermissionServiceDbContext(options);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        context.Set<TenantPermission>().AddRange(
            new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = [nameof(PermissionType.Read)],
                ExpiresAt = StartTime.AddSeconds(600)
            },
            new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = [nameof(PermissionType.Read)],
                ExpiresAt = StartTime.AddSeconds(30)
            });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(StartTime);
        SystemClock.SetProvider(clock);
        try
        {
            var cache = CreateRecordingPermissionCache(out var writes);
            var service = new PermissionService(
                context,
                securityVersionStore: new InMemoryTenantSecurityVersionStore(),
                hybridPermissionCache: cache.Object);

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue();

            writes.Should().HaveCount(1);
            var writeSnapshot = writes.ToArray();
            writeSnapshot[0].Values.Should().ContainSingle();
            writeSnapshot[0].Values.Single().EffectiveUntilUtc.Should().Be(StartTime.AddSeconds(30),
                "the envelope must carry the earliest participating grant expiration");
            writeSnapshot[0].TtlSecondsByKey.Should().ContainSingle().Which.Value.Should().Be(30,
                "the entry TTL must be the minimum remaining grant lifetime, not the longer one");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task LapsedEnvelopeEntry_IsTreatedAsMissAndFailsClosed()
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
            Permissions = [nameof(PermissionType.Read)],
            ExpiresAt = StartTime.AddSeconds(30)
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(StartTime);
        SystemClock.SetProvider(clock);
        try
        {
            var cache = CreateRecordingPermissionCache(out var writes);
            var service = new PermissionService(
                context,
                securityVersionStore: new InMemoryTenantSecurityVersionStore(),
                hybridPermissionCache: cache.Object);

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue();
            writes.Should().HaveCount(1);

            // The cached allow entry is still physically present (as if written by a process with a
            // skewed clock or promoted from an L2 with a lagging TTL) — its envelope has lapsed.
            clock.Advance(TimeSpan.FromSeconds(30.5));

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeFalse("a found-but-lapsed cache entry must be treated as absent (fail closed)");

            cache.Verify(
                permissionCache => permissionCache.GetManyValuesAsync<CachedPermissionDecision>(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    "permission",
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2),
                "the lapsed entry is re-read and rejected, not trusted");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task DecisionLapsingDuringEvaluation_IsReturnedButNotCached()
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
            Permissions = [nameof(PermissionType.Read)],
            ExpiresAt = StartTime.AddMilliseconds(500)
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(StartTime);
        SystemClock.SetProvider(clock);
        try
        {
            var cache = CreateRecordingPermissionCache(out var writes);
            var service = new PermissionService(
                context,
                securityVersionStore: new InMemoryTenantSecurityVersionStore(),
                hybridPermissionCache: cache.Object);

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue("the authoritative source answer is returned even when its grant is about to lapse");

            writes.Should().BeEmpty(
                "a decision whose floored remaining lifetime is zero must be cached under no TTL");

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue("the still-effective grant keeps allowing via re-evaluation");
            writes.Should().BeEmpty();
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task NonTimeBoundDecisions_KeepUncappedEnvelopeAndAreCached()
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
            Permissions = [nameof(PermissionType.Read)]
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(StartTime);
        SystemClock.SetProvider(clock);
        try
        {
            var cache = CreateRecordingPermissionCache(out var writes);
            var service = new PermissionService(
                context,
                securityVersionStore: new InMemoryTenantSecurityVersionStore(),
                hybridPermissionCache: cache.Object);

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue();
            clock.Advance(TimeSpan.FromMinutes(10));
            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue("a decision from unbounded grants is not retired by time alone");

            writes.Should().HaveCount(1, "only the cache-miss call writes");
            var writeSnapshot = writes.ToArray();
            writeSnapshot[0].Values.Single().EffectiveUntilUtc.Should().BeNull(
                "decisions from unbounded grants carry no expiry envelope");
            writeSnapshot[0].TtlSecondsByKey.Should().BeEmpty(
                "unbounded decisions use the configured cache TTL, not a clamped one");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task CacheDisabled_FallsBackToDatabaseAndFailsClosed()
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
            Permissions = [nameof(PermissionType.Read)]
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(StartTime);
        SystemClock.SetProvider(clock);
        try
        {
            var service = new PermissionService(context);

            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Read]))[userId][PermissionType.Read]
                .Should().BeTrue("the cached-path collaborators are optional; the database still answers");
            (await service.BulkCheckPermissionsAsync([userId], tenantId, [PermissionType.Edit]))[userId][PermissionType.Edit]
                .Should().BeFalse("a permission with no grant fails closed (deny)");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    private static Mock<IHybridPermissionCache> CreateRecordingPermissionCache(
        out ConcurrentQueue<(IReadOnlyDictionary<string, CachedPermissionDecision> Values, IReadOnlyDictionary<string, int> TtlSecondsByKey)> writes)
    {
        var values = new ConcurrentDictionary<string, CachedPermissionDecision>(StringComparer.Ordinal);
        var recordedWrites = new ConcurrentQueue<(IReadOnlyDictionary<string, CachedPermissionDecision>, IReadOnlyDictionary<string, int>)>();
        writes = recordedWrites;

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
            .Returns((IReadOnlyDictionary<string, CachedPermissionDecision> entries, string _, IReadOnlyDictionary<string, int>? ttlSecondsByKey, CancellationToken _) =>
            {
                foreach (var entry in entries)
                {
                    values[entry.Key] = entry.Value;
                }

                recordedWrites.Enqueue((entries, ttlSecondsByKey ?? new Dictionary<string, int>()));
                return Task.CompletedTask;
            });

        return cache;
    }

    private sealed class InMemoryTenantSecurityVersionStore : ITenantSecurityVersionStore
    {
        private readonly ConcurrentDictionary<Guid, long> _versions = new();

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
            var tenantVersion = _versions.GetValueOrDefault(tenantId);
            var globalVersion = tenantId == Guid.Empty ? tenantVersion : _versions.GetValueOrDefault(Guid.Empty);
            return Task.FromResult((tenantVersion, globalVersion));
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

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _utcNow = start;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
