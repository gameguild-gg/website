using FluentAssertions;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class PermissionCacheKeysQueryHandlerTests
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly PermissionCacheKeyTracker _tracker;

    public PermissionCacheKeysQueryHandlerTests()
    {
        _tracker = new PermissionCacheKeyTracker(_memoryCache, new CacheMetricsService(), maxTrackedEntries: 100);
    }

    [Fact]
    public async Task Handle_ListsKeysMostRecentlyUsedFirstWithPagination()
    {
        Track("acl:subj:user-a:res-1:v1:read", "acl");
        Track("perm:user-b:resource-2:permission", "permission");
        Track("acl:subj:user-c:res-3:v1:write", "acl");
        _tracker.Touch("perm:user-b:resource-2:permission");

        var handler = new GetPermissionCacheKeysQueryHandler(_tracker);

        var result = await handler.Handle(
            new GetPermissionCacheKeysQuery { Page = 1, PageSize = 2 },
            CancellationToken.None);

        result.TotalTrackedEntries.Should().Be(3);
        result.TotalMatchingEntries.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items[0].Key.Should().Be("perm:user-b:resource-2:permission", "the touched key is the most recently used");

        var secondPage = await handler.Handle(
            new GetPermissionCacheKeysQuery { Page = 2, PageSize = 2 },
            CancellationToken.None);
        secondPage.Items.Should().ContainSingle().Which.Key.Should().Be("acl:subj:user-a:res-1:v1:read");
    }

    [Fact]
    public async Task Handle_AppliesSearchAndCacheTypeFilters()
    {
        Track("acl:subj:user-a:res-1:v1:read", "acl");
        Track("perm:user-a:resource-2:permission", "permission");
        Track("acl:subj:user-b:res-2:v1:write", "acl");

        var handler = new GetPermissionCacheKeysQueryHandler(_tracker);

        var result = await handler.Handle(
            new GetPermissionCacheKeysQuery { Search = "USER-A", CacheType = "acl", Page = 1, PageSize = 50 },
            CancellationToken.None);

        result.TotalMatchingEntries.Should().Be(1);
        result.TotalTrackedEntries.Should().Be(3);
        result.Items.Should().ContainSingle().Which.Key.Should().Be("acl:subj:user-a:res-1:v1:read");
    }

    [Fact]
    public async Task Handle_ClampsOutOfRangePaging()
    {
        Track("perm:user-a:resource-1:permission", "permission");

        var handler = new GetPermissionCacheKeysQueryHandler(_tracker);

        var result = await handler.Handle(
            new GetPermissionCacheKeysQuery { Page = -5, PageSize = 5_000 },
            CancellationToken.None);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(200, "the page size is capped at 200");
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ReturnsKeyMetadataIncludingL1Presence()
    {
        Track("perm:user-a:resource-1:permission", "permission");
        _memoryCache.Remove("perm:user-a:resource-1:permission");

        var handler = new GetPermissionCacheKeysQueryHandler(_tracker);

        var result = await handler.Handle(
            new GetPermissionCacheKeysQuery(),
            CancellationToken.None);

        var entry = result.Items.Should().ContainSingle().Subject;
        entry.CacheType.Should().Be("permission");
        entry.PresentInL1.Should().BeFalse();
        entry.LastAccessUtc.Should().BeOnOrAfter(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task SingleKeyHandler_ReturnsMetadataOrNull()
    {
        Track("acl:subj:user-1:res-1:v3:read", "acl");

        var handler = new GetPermissionCacheKeyQueryHandler(_tracker);

        var found = await handler.Handle(
            new GetPermissionCacheKeyQuery { Key = "acl:subj:user-1:res-1:v3:read" },
            CancellationToken.None);
        found.Should().NotBeNull();
        found!.Key.Should().Be("acl:subj:user-1:res-1:v3:read");
        found.PresentInL1.Should().BeTrue();

        var missing = await handler.Handle(
            new GetPermissionCacheKeyQuery { Key = "  " },
            CancellationToken.None);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsAnEmptyPageWhenNothingIsTracked()
    {
        var handler = new GetPermissionCacheKeysQueryHandler(_tracker);

        var result = await handler.Handle(
            new GetPermissionCacheKeysQuery(),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalMatchingEntries.Should().Be(0);
        result.TotalTrackedEntries.Should().Be(0);
    }

    private void Track(string key, string cacheType)
    {
        var options = new MemoryCacheEntryOptions();
        _tracker.Track(key, cacheType, options);
        _memoryCache.Set(key, true, options);
    }
}
