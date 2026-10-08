using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Caching;

namespace GameGuild.Identity.Authentication;

/// <summary>Lists tracked authorization L1 cache keys for system-administrator cache-key debugging.</summary>
public sealed class GetPermissionCacheKeysQueryHandler(
    IPermissionCacheKeyTracker keyTracker)
    : IQueryHandler<GetPermissionCacheKeysQuery, PermissionCacheKeysDto>
{
    private const int MaxPageSize = 200;

    public Task<PermissionCacheKeysDto> Handle(
        GetPermissionCacheKeysQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var search = NormalizeFilter(request.Search);
        var cacheType = NormalizeFilter(request.CacheType);

        var totalTracked = keyTracker.GetStatistics().TotalEntries;
        var maxMatches = Math.Max(1, checked((int)Math.Min(int.MaxValue, totalTracked)));
        var items = keyTracker.SearchTrackedKeys(
            search,
            cacheType,
            skip: 0,
            take: maxMatches);

        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(info => new PermissionCacheKeyInfoDto
            {
                Key = info.Key,
                CacheType = info.CacheType,
                FirstTrackedUtc = info.FirstTrackedUtc,
                LastAccessUtc = info.LastAccessUtc,
                PresentInL1 = info.PresentInL1
            })
            .ToList();

        return Task.FromResult(new PermissionCacheKeysDto
        {
            Items = pageItems,
            TotalMatchingEntries = items.Count,
            TotalTrackedEntries = totalTracked,
            Page = page,
            PageSize = pageSize
        });
    }

    private static string? NormalizeFilter(string? filter) =>
        string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
}

/// <summary>Returns tracking metadata for a single authorization L1 cache key.</summary>
public sealed class GetPermissionCacheKeyQueryHandler(
    IPermissionCacheKeyTracker keyTracker)
    : IQueryHandler<GetPermissionCacheKeyQuery, PermissionCacheKeyInfoDto?>
{
    public Task<PermissionCacheKeyInfoDto?> Handle(
        GetPermissionCacheKeyQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var info = string.IsNullOrWhiteSpace(request.Key)
            ? null
            : keyTracker.GetTrackedKey(request.Key.Trim());

        return Task.FromResult(info is null
            ? null
            : new PermissionCacheKeyInfoDto
            {
                Key = info.Key,
                CacheType = info.CacheType,
                FirstTrackedUtc = info.FirstTrackedUtc,
                LastAccessUtc = info.LastAccessUtc,
                PresentInL1 = info.PresentInL1
            });
    }
}
