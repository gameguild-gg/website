namespace GameGuild.Compliance.Audit;

/// <summary>Configuration bounds for audit query DSL execution. All values are enforced fail-closed.</summary>
public sealed class AuditQueryDslOptions
{
    public const string ConfigurationSection = "AuditQueryDsl";

    /// <summary>Maximum page size a single execution may return.</summary>
    public int MaxPageSize { get; set; } = 200;

    /// <summary>Maximum number of audit rows a single execution may scan.</summary>
    public int MaxRowsScanned { get; set; } = 50_000;

    /// <summary>Default lookback applied when the query carries no timestamp predicate.</summary>
    public int DefaultLookbackDays { get; set; } = 90;

    /// <summary>Default execution timeout.</summary>
    public int DefaultTimeoutSeconds { get; set; } = 10;

    /// <summary>Maximum allowed execution timeout.</summary>
    public int MaxTimeoutSeconds { get; set; } = 30;

    /// <summary>Resource type used to look up data-masking rules for result rows.</summary>
    public string MaskingResourceType { get; set; } = "UnifiedSecurityAuditEntry";
}

/// <summary>
///     A request to execute a parsed audit query DSL expression against the audit stores.
///     Acting-user identity and tenant scoping are supplied by the caller (from the actor
///     context), never taken from this payload.
/// </summary>
public sealed record AuditQueryDslExecutionRequest
{
    public required AuditQueryDslQuery Query { get; init; }

    /// <summary>Page offset.</summary>
    public int Skip { get; init; }

    /// <summary>Page size; clamped to <see cref="AuditQueryDslOptions.MaxPageSize"/>.</summary>
    public int Take { get; init; } = 50;

    /// <summary>Requested execution timeout in seconds; clamped between 1 and MaxTimeoutSeconds.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Acting user identifier, for query audit trails and saved-view attribution.</summary>
    public required Guid ActingUserId { get; init; }

    /// <summary>
    ///     Tenant scope clamp: when set, every matching row must belong to this tenant.
    ///     Used for non-system-admin callers so they can never read cross-tenant audit data.
    /// </summary>
    public Guid? TenantScope { get; init; }
}

/// <summary>Aggregated statistics computed over the full matched set (not just the returned page).</summary>
public sealed class AuditQueryDslAggregates
{
    public int MatchedCount { get; set; }

    public Dictionary<string, int> BySource { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> BySeverity { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByAction { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    /// <summary>UTC day buckets of matched events, oldest first, capped at 366 buckets.</summary>
    public List<AuditQueryDslTimeBucket> TimeBuckets { get; set; } = [];

    /// <summary>First matched timestamp (UTC) or null when nothing matched.</summary>
    public DateTime? FirstMatchedAt { get; set; }

    /// <summary>Last matched timestamp (UTC) or null when nothing matched.</summary>
    public DateTime? LastMatchedAt { get; set; }
}

/// <summary>One aggregated day bucket of matched events.</summary>
public sealed record AuditQueryDslTimeBucket(DateTime DateUtc, int Count);

/// <summary>Outcome of an audit query DSL execution.</summary>
public sealed class AuditQueryDslExecutionResult
{
    /// <summary>The (data-masked) page of matching audit entries.</summary>
    public List<object> Rows { get; set; } = [];

    public int TotalCount { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; }

    public AuditQueryDslAggregates Aggregates { get; set; } = new();

    /// <summary>Wall-clock execution duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Number of audit rows scanned before the filter was applied.</summary>
    public int RowsScanned { get; set; }

    /// <summary>True when the scan hit the configured row cap; totals then describe the scanned subset only.</summary>
    public bool ScanTruncated { get; set; }

    /// <summary>Effective UTC window the stores were queried for.</summary>
    public DateTime WindowStartUtc { get; set; }

    /// <summary>Effective UTC window the stores were queried for.</summary>
    public DateTime WindowEndUtc { get; set; }
}
