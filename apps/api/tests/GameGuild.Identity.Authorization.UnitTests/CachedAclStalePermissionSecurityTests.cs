using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Security regression tests for stale-permission windows on the cached ACL service (issue #354):
///     time-lapsed grants must never serve a cached allow, on both the preferred subject path and the
///     legacy user path, and decisions evaluated across a concurrent permission mutation must be
///     discarded instead of cached or returned.
/// </summary>
public sealed class CachedAclStalePermissionSecurityTests
{
    private static readonly DateTime StartTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TimeLapsedGrant_CannotServeStaleAllow_PreferedPath()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = CreateAllowEntry(tenantId, userId, "Document", "doc-time", AccessLevel.Write, StartTime.AddSeconds(5));
        var (inner, repository) = CreateDatabaseService(entry);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var clock = new MutableTimeProvider(new DateTimeOffset(StartTime));
        SystemClock.SetProvider(clock);
        try
        {
            var service = CreateCachedService(inner, memoryCache, tenantId, userId, accessControlListTtlSeconds: 60);
            var subject = AclSubject.ForUser(userId);

            (await service.EvaluateAccessAsync(subject, tenantId, "Document", "doc-time"))
                .Should().Be(AccessLevel.Write, "the grant is effective at evaluation time");
            clock.Advance(TimeSpan.FromSeconds(6));

            (await service.EvaluateAccessAsync(subject, tenantId, "Document", "doc-time"))
                .Should().Be(AccessLevel.None,
                    "a time-lapsed grant must not keep serving its cached allow on the preferred path");
            repository.Verify(repository => repository.GetByResourceAndPrincipalsAsync(
                tenantId, "Document", "doc-time", It.IsAny<IEnumerable<(AclPrincipalType Type, Guid? Id)>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2),
                "the expired decision must be re-evaluated from the authoritative source");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task TimeLapsedGrant_CannotServeStaleAllow_LegacyPath()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = CreateAllowEntry(tenantId, userId, "Document", "doc-time", AccessLevel.Write, StartTime.AddSeconds(5));
        var (inner, repository) = CreateDatabaseService(entry);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var clock = new MutableTimeProvider(new DateTimeOffset(StartTime));
        SystemClock.SetProvider(clock);
        try
        {
            var service = CreateCachedService(inner, memoryCache, tenantId, userId, accessControlListTtlSeconds: 60);

            (await service.GetAccessLevelAsync(userId, tenantId, "Document", "doc-time"))
                .Should().Be(AccessLevel.Write);
            clock.Advance(TimeSpan.FromSeconds(6));

            (await service.GetAccessLevelAsync(userId, tenantId, "Document", "doc-time"))
                .Should().Be(AccessLevel.None,
                    "a time-lapsed grant must not keep serving its cached allow on the legacy path");
            repository.Verify(repository => repository.GetByResourceAndPrincipalsAsync(
                tenantId, "Document", "doc-time", It.IsAny<IEnumerable<(AclPrincipalType Type, Guid? Id)>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task TimeBoundDecision_L2WriteTtlIsClampedToEarliestGrantExpiration()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new MutableTimeProvider(new DateTimeOffset(StartTime));
        SystemClock.SetProvider(clock);
        try
        {
            var inner = new TimeBoundAclService(
                new TimeBoundAccessEvaluation(AccessLevel.Write, StartTime.AddSeconds(5.5)));
            var hybrid = new Mock<IHybridPermissionCache>();
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateCachedService(inner, memoryCache, tenantId, userId, accessControlListTtlSeconds: 60, hybrid);

            (await service.EvaluateAccessAsync(AclSubject.ForUser(userId), tenantId, "Document", "doc-clamp"))
                .Should().Be(AccessLevel.Write);

            hybrid.Verify(cache => cache.SetValueAsync(
                It.IsAny<string>(), It.IsAny<CachedAclDecision>(), "acl", 5, It.IsAny<CancellationToken>()),
                Times.Once,
                "the L2 write must be clamped to the seconds remaining before the grant expires (5.5s floors to 5s)");
            hybrid.Verify(cache => cache.SetValueAsync(
                It.IsAny<string>(), It.IsAny<CachedAclDecision>(), "acl", It.IsAny<CancellationToken>()),
                Times.Never,
                "time-bound decisions must use the TTL-clamped write, never the fixed-TTL write");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task L2Promotion_DoesNotServeDecisionPastItsGrantExpiry()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new MutableTimeProvider(new DateTimeOffset(StartTime));
        SystemClock.SetProvider(clock);
        try
        {
            var inner = new TimeBoundAclService(new TimeBoundAccessEvaluation(AccessLevel.None, null));
            var hybrid = new Mock<IHybridPermissionCache>();
            hybrid.Setup(cache => cache.GetValueAsync<CachedAclDecision>(
                    It.IsAny<string>(), "acl", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CacheResult<CachedAclDecision>.Hit(
                    new CachedAclDecision(AccessLevel.Write, StartTime.AddSeconds(5))));
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateCachedService(inner, memoryCache, tenantId, userId, accessControlListTtlSeconds: 60, hybrid);
            clock.Advance(TimeSpan.FromSeconds(6));

            (await service.EvaluateAccessAsync(AclSubject.ForUser(userId), tenantId, "Document", "doc-promote"))
                .Should().Be(AccessLevel.None,
                    "an L2 entry whose grant already expired must not be served or promoted as a fresh allow");
            inner.TimeBoundEvaluations.Should().Be(1,
                "the lapsed L2 entry must be treated as a miss and re-evaluated");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task LegacyPath_DiscardsDecisionEvaluatedAcrossConcurrentVersionChange()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(service => service.GetAccessLevelAsync(
                userId, tenantId, "Document", "doc-race", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Write);
        var tenantVersions = new Mock<ITenantSecurityVersionStore>();
        tenantVersions
            .SetupSequence(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, 1L))
            .ReturnsAsync((2L, 2L))
            .ReturnsAsync((2L, 2L))
            .ReturnsAsync((2L, 2L));
        var userVersions = new Mock<IUserSecurityVersionStore>();
        userVersions.Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedAccessControlListService(
            inner.Object,
            memoryCache,
            tenantVersions.Object,
            userVersions.Object,
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }));

        (await service.GetAccessLevelAsync(userId, tenantId, "Document", "doc-race"))
            .Should().Be(AccessLevel.Write,
                "the retried evaluation under the new version is the one returned");

        inner.Verify(service => service.GetAccessLevelAsync(
            userId, tenantId, "Document", "doc-race", It.IsAny<CancellationToken>()), Times.Exactly(2),
            "the decision evaluated across a concurrent mutation must be discarded and re-read");
        tenantVersions.Verify(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(4),
            "each attempt reads the version before the query and re-checks it afterwards");
    }

    [Fact]
    public async Task LegacyPath_ThrowsAfterPersistentVersionInstability()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(service => service.GetAccessLevelAsync(
                userId, tenantId, "Document", "doc-unstable", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Write);
        var version = 0L;
        var tenantVersions = new Mock<ITenantSecurityVersionStore>();
        tenantVersions
            .Setup(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (Interlocked.Increment(ref version), 0L));
        var userVersions = new Mock<IUserSecurityVersionStore>();
        userVersions.Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedAccessControlListService(
            inner.Object,
            memoryCache,
            tenantVersions.Object,
            userVersions.Object,
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }));

        var act = () => service.GetAccessLevelAsync(userId, tenantId, "Document", "doc-unstable");
        await act.Should().ThrowAsync<InvalidOperationException>(
            "a legacy lookup that never observes a stable version must fail closed instead of serving a possibly stale decision");
    }

    [Fact]
    public async Task AlreadyExpiredDecision_IsReturnedButNeverCached()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new MutableTimeProvider(new DateTimeOffset(StartTime));
        SystemClock.SetProvider(clock);
        try
        {
            var inner = new TimeBoundAclService(new TimeBoundAccessEvaluation(AccessLevel.Write, StartTime.AddSeconds(-1)));
            var hybrid = new Mock<IHybridPermissionCache>();
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateCachedService(inner, memoryCache, tenantId, userId, accessControlListTtlSeconds: 60, hybrid);

            (await service.EvaluateAccessAsync(AclSubject.ForUser(userId), tenantId, "Document", "doc-late"))
                .Should().Be(AccessLevel.Write,
                    "the authoritative source answer is returned even when its grants just lapsed");

            hybrid.Verify(cache => cache.SetValueAsync(
                It.IsAny<string>(), It.IsAny<CachedAclDecision>(), "acl", It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
            hybrid.Verify(cache => cache.SetValueAsync(
                It.IsAny<string>(), It.IsAny<CachedAclDecision>(), "acl", It.IsAny<CancellationToken>()),
                Times.Never);

            (await service.EvaluateAccessAsync(AclSubject.ForUser(userId), tenantId, "Document", "doc-late"))
                .Should().Be(AccessLevel.Write);
            inner.TimeBoundEvaluations.Should().Be(2,
                "a decision whose grants already expired must not be cached under any TTL");
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    private static AccessControlListEntry CreateAllowEntry(
        Guid tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        AccessLevel level,
        DateTime expiresAtUtc) => new()
    {
        TenantId = tenantId,
        PrincipalType = AclPrincipalType.User,
        PrincipalId = userId,
        ResourceType = resourceType,
        ResourceId = resourceId,
        AccessLevel = level,
        IsDenied = false,
        GrantedBy = Guid.NewGuid(),
        GrantedAt = expiresAtUtc.AddMinutes(-10),
        IsActive = true,
        ExpiresAt = expiresAtUtc
    };

    private static (DatabaseAccessControlListService Service, Mock<IAccessControlListEntryRepository> Repository) CreateDatabaseService(
        params AccessControlListEntry[] entries)
    {
        var repository = new Mock<IAccessControlListEntryRepository>();
        repository.Setup(repo => repo.GetByResourceAndPrincipalsAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<(AclPrincipalType Type, Guid? Id)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        var service = new DatabaseAccessControlListService(
            repository.Object,
            Mock.Of<ITenantSecurityVersionRepository>());
        return (service, repository);
    }

    private static CachedAccessControlListService CreateCachedService(
        IAccessControlListService inner,
        IMemoryCache memoryCache,
        Guid tenantId,
        Guid userId,
        int accessControlListTtlSeconds,
        Mock<IHybridPermissionCache>? hybrid = null)
    {
        var tenantVersions = new Mock<ITenantSecurityVersionStore>();
        tenantVersions
            .Setup(store => store.GetTenantAndGlobalVersionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, 1L));
        var userVersions = new Mock<IUserSecurityVersionStore>();
        userVersions
            .Setup(store => store.GetVersionAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        return hybrid is null
            ? new CachedAccessControlListService(
                inner,
                memoryCache,
                tenantVersions.Object,
                userVersions.Object,
                Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = accessControlListTtlSeconds }))
            : new CachedAccessControlListService(
                inner,
                memoryCache,
                tenantVersions.Object,
                userVersions.Object,
                Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = accessControlListTtlSeconds }),
                hybrid.Object);
    }

    private sealed class TimeBoundAclService(TimeBoundAccessEvaluation nextEvaluation) : IAccessControlListService, ITimeBoundAccessControlListEvaluation
    {
        public int TimeBoundEvaluations { get; private set; }

        public Task<TimeBoundAccessEvaluation> EvaluateAccessTimeBoundAsync(
            AclSubject subject,
            Guid tenantId,
            string resourceType,
            string resourceId,
            CancellationToken cancellationToken = default)
        {
            TimeBoundEvaluations++;
            return Task.FromResult(nextEvaluation);
        }

        public async Task<AccessLevel> EvaluateAccessAsync(
            AclSubject subject, Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
        {
            var evaluation = await EvaluateAccessTimeBoundAsync(subject, tenantId, resourceType, resourceId, cancellationToken);
            return evaluation.AccessLevel;
        }

        public Task<bool> HasAccessAsync(
            AclSubject subject, Guid tenantId, string resourceType, string resourceId, AccessLevel requiredLevel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<AccessLevel> GetAccessLevelAsync(
            Guid userId, Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
        {
            var evaluation = await EvaluateAccessTimeBoundAsync(AclSubject.ForUser(userId), tenantId, resourceType, resourceId, cancellationToken);
            return evaluation.AccessLevel;
        }

        public Task GrantAccessAsync(
            Guid grantorId, AclPrincipalType principalType, Guid? principalId, Guid tenantId, string resourceType, string resourceId, AccessLevel accessLevel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task GrantAccessAsync(
            Guid grantorId, Guid granteeId, Guid tenantId, string resourceType, string resourceId, AccessLevel accessLevel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DenyAccessAsync(
            Guid grantorId, AclPrincipalType principalType, Guid? principalId, Guid tenantId, string resourceType, string resourceId, AccessLevel accessLevel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAccessAsync(
            Guid revokerId, AclPrincipalType principalType, Guid? principalId, Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAccessAsync(
            Guid revokerId, Guid userId, Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HasAccessAsync(
            Guid userId, Guid tenantId, string resourceType, string resourceId, AccessLevel requiredLevel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _utcNow = start;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
