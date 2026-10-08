using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>Lists tracked authorization L1 cache keys with optional search and cache-type filters.</summary>
public sealed record GetPermissionCacheKeysQuery : IQuery<PermissionCacheKeysDto>
{
    /// <summary>Case-insensitive substring filter on the cache key; <c>null</c> matches every key.</summary>
    public string? Search { get; init; }

    /// <summary>Exact cache-type filter (for example <c>acl</c>); <c>null</c> matches every type.</summary>
    public string? CacheType { get; init; }

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Page size (1–200).</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>Gets tracking metadata for one authorization L1 cache key.</summary>
public sealed record GetPermissionCacheKeyQuery : IQuery<PermissionCacheKeyInfoDto?>
{
    /// <summary>The cache key to inspect.</summary>
    public string Key { get; init; } = string.Empty;
}
