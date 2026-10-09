using FluentAssertions;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class PermissionCacheKeyTrackerLruTests
{
    [Fact]
    public void Eviction_RemovesLeastRecentlyUsedInsteadOfOldestInserted()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object, maxTrackedEntries: 3);

        Track(tracker, memoryCache, "perm:user-a:resource-1:permission", "permission");
        Track(tracker, memoryCache, "perm:user-b:resource-1:permission", "permission");
        Track(tracker, memoryCache, "perm:user-c:resource-1:permission", "permission");

        // user-a was inserted first but is the most recently used after this read.
        tracker.Touch("perm:user-a:resource-1:permission");

        Track(tracker, memoryCache, "perm:user-d:resource-1:permission", "permission");

        memoryCache.TryGetValue("perm:user-a:resource-1:permission", out _).Should().BeTrue();
        memoryCache.TryGetValue("perm:user-b:resource-1:permission", out _).Should().BeFalse("the least-recently-used entry is evicted, not the oldest-inserted one");
        memoryCache.TryGetValue("perm:user-c:resource-1:permission", out _).Should().BeTrue();
        memoryCache.TryGetValue("perm:user-d:resource-1:permission", out _).Should().BeTrue();
        tracker.GetStatistics().TotalEntries.Should().Be(3);
        metrics.Verify(service => service.RecordEviction(CacheLevel.L1, "permission", "capacity"), Times.Once);
    }

    [Fact]
    public void Touch_OnUntrackedKey_IsANoOp()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new PermissionCacheKeyTracker(memoryCache, Mock.Of<ICacheMetricsService>(), maxTrackedEntries: 2);

        var act = () => tracker.Touch("perm:never-tracked");

        act.Should().NotThrow();
        tracker.GetStatistics().TotalEntries.Should().Be(0);
    }

    [Fact]
    public void Touch_RepeatedReadsKeepAnEntryAliveAcrossMultipleCapacityEvictions()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object, maxTrackedEntries: 2);

        Track(tracker, memoryCache, "perm:hot:resource-1:permission", "permission");
        Track(tracker, memoryCache, "perm:cold-1:resource-1:permission", "permission");

        foreach (var index in Enumerable.Range(0, 5))
        {
            tracker.Touch("perm:hot:resource-1:permission");
            Track(tracker, memoryCache, $"perm:cold-{index + 2}:resource-1:permission", "permission");
        }

        memoryCache.TryGetValue("perm:hot:resource-1:permission", out _).Should().BeTrue();
        tracker.GetTrackedKey("perm:hot:resource-1:permission").Should().NotBeNull();
    }

    [Fact]
    public void GetTrackedKey_ReturnsMetadataAndNullForUnknownKeys()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new PermissionCacheKeyTracker(memoryCache, Mock.Of<ICacheMetricsService>(), maxTrackedEntries: 10);

        Track(tracker, memoryCache, "acl:subj:user-1:res-1:v3:read", "acl");
        tracker.Touch("acl:subj:user-1:res-1:v3:read");

        var info = tracker.GetTrackedKey("acl:subj:user-1:res-1:v3:read");
        info.Should().NotBeNull();
        info!.Key.Should().Be("acl:subj:user-1:res-1:v3:read");
        info.CacheType.Should().Be("acl");
        info.PresentInL1.Should().BeTrue();
        info.LastAccessUtc.Should().BeOnOrAfter(info.FirstTrackedUtc);

        tracker.GetTrackedKey("acl:missing").Should().BeNull();
    }

    [Fact]
    public void GetTrackedKey_ReportsAbsenceAfterTheL1EntryExpires()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new PermissionCacheKeyTracker(memoryCache, Mock.Of<ICacheMetricsService>(), maxTrackedEntries: 10);

        Track(tracker, memoryCache, "perm:gone:resource-1:permission", "permission");
        memoryCache.Remove("perm:gone:resource-1:permission");

        var info = tracker.GetTrackedKey("perm:gone:resource-1:permission");
        info.Should().NotBeNull();
        info!.PresentInL1.Should().BeFalse("the key is still tracked although L1 no longer holds the entry");
    }

    [Fact]
    public void SearchTrackedKeys_FiltersSearchesAndOrdersByMostRecentUse()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new PermissionCacheKeyTracker(memoryCache, Mock.Of<ICacheMetricsService>(), maxTrackedEntries: 10);

        Track(tracker, memoryCache, "acl:subj:user-a:res-1:v1:read", "acl");
        Track(tracker, memoryCache, "perm:user-a:resource-2:permission", "permission");
        Track(tracker, memoryCache, "acl:subj:user-b:res-2:v1:write", "acl");
        tracker.Touch("acl:subj:user-a:res-1:v1:read");

        var allKeys = tracker.SearchTrackedKeys(searchTerm: null, cacheType: null, skip: 0, take: 10);
        allKeys.Should().HaveCount(3);
        allKeys[0].Key.Should().Be("acl:subj:user-a:res-1:v1:read", "the touched key is the most recently used");

        var aclKeys = tracker.SearchTrackedKeys(searchTerm: "res-1", cacheType: "acl", skip: 0, take: 10);
        aclKeys.Should().ContainSingle().Which.Key.Should().Be("acl:subj:user-a:res-1:v1:read");

        var paged = tracker.SearchTrackedKeys(searchTerm: null, cacheType: null, skip: 1, take: 1);
        paged.Should().ContainSingle();
        paged[0].Key.Should().Be(allKeys[1].Key);
    }

    [Fact]
    public void SearchTrackedKeys_RejectsInvalidPaging()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new PermissionCacheKeyTracker(memoryCache, Mock.Of<ICacheMetricsService>(), maxTrackedEntries: 10);

        var negativeSkip = () => tracker.SearchTrackedKeys(null, null, skip: -1, take: 1);
        var nonPositiveTake = () => tracker.SearchTrackedKeys(null, null, skip: 0, take: 0);

        negativeSkip.Should().Throw<ArgumentOutOfRangeException>();
        nonPositiveTake.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReTrackingAnExistingKey_UpdatesAccessOrderingWithoutDuplicating()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var metrics = new Mock<ICacheMetricsService>();
        var tracker = new PermissionCacheKeyTracker(memoryCache, metrics.Object, maxTrackedEntries: 2);

        Track(tracker, memoryCache, "perm:first:resource-1:permission", "permission");
        Track(tracker, memoryCache, "perm:second:resource-1:permission", "permission");
        Track(tracker, memoryCache, "perm:first:resource-1:permission", "permission"); // re-write refreshes it

        Track(tracker, memoryCache, "perm:third:resource-1:permission", "permission");

        memoryCache.TryGetValue("perm:first:resource-1:permission", out _).Should().BeTrue("the re-tracked entry counts as recently used");
        memoryCache.TryGetValue("perm:second:resource-1:permission", out _).Should().BeFalse();
        tracker.GetStatistics().TotalEntries.Should().Be(2);
    }

    private static void Track(IPermissionCacheKeyTracker tracker, IMemoryCache memoryCache, string key, string cacheType)
    {
        var options = new MemoryCacheEntryOptions();
        tracker.Track(key, cacheType, options);
        memoryCache.Set(key, true, options);
    }
}
