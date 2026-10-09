namespace GameGuild.Identity.Authentication;

/// <summary>Process-local tracking metadata for a single authorization L1 cache key.</summary>
public sealed class PermissionCacheKeyInfoDto
{
    /// <summary>The tracked cache key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The cache type the entry was tracked under (for example <c>acl</c> or <c>permission</c>).</summary>
    public string CacheType { get; set; } = string.Empty;

    /// <summary>UTC time the key was (re)registered in the tracker.</summary>
    public DateTime FirstTrackedUtc { get; set; }

    /// <summary>UTC time of the tracked insertion or the most recent L1 read hit.</summary>
    public DateTime LastAccessUtc { get; set; }

    /// <summary>Whether the entry is currently readable from the process-local L1 cache.</summary>
    public bool PresentInL1 { get; set; }
}

/// <summary>A page of tracked authorization L1 cache keys, most recently used first.</summary>
public sealed class PermissionCacheKeysDto
{
    /// <summary>Tracked keys on the requested page.</summary>
    public List<PermissionCacheKeyInfoDto> Items { get; set; } = new();

    /// <summary>Total tracked entries matching the filters (across all pages).</summary>
    public long TotalMatchingEntries { get; set; }

    /// <summary>Total tracked entries in this process, ignoring filters.</summary>
    public long TotalTrackedEntries { get; set; }

    /// <summary>The applied page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>The applied page size.</summary>
    public int PageSize { get; set; }
}
