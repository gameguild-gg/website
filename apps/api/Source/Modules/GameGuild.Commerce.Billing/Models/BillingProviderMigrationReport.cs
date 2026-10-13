namespace GameGuild.Commerce.Billing;

/// <summary>
///     Classification of a subscription row within a provider-migration dry-run report.
/// </summary>
public enum BillingProviderMigrationEntryClassification
{
    /// <summary>
    ///     The subscription's external identifier is attributable to the source provider
    ///     and it has no external identifier in the target provider's namespace — it must
    ///     be migrated before cutover.
    /// </summary>
    BoundToSourceMissingTargetExternalId = 1,

    /// <summary>
    ///     The subscription's external identifier is already attributable to the target
    ///     provider — nothing to migrate.
    /// </summary>
    AlreadyOnTargetProvider = 2,

    /// <summary>
    ///     The subscription carries an external identifier that cannot be attributed to a
    ///     provider with confidence — manual review required (fail-closed, never guessed).
    /// </summary>
    UnattributedExternalId = 3,

    /// <summary>
    ///     The subscription has no external identifier at all — it is not bound to any
    ///     external provider and is out of scope for the migration.
    /// </summary>
    NotExternallyBound = 4
}

/// <summary>
///     One subscription row of a <see cref="BillingProviderMigrationReport"/>.
/// </summary>
public sealed record BillingProviderMigrationReportEntry
{
    /// <summary>Internal subscription identifier.</summary>
    public required Guid SubscriptionId { get; init; }

    /// <summary>Owning tenant identifier.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>External (provider) subscription identifier as stored.</summary>
    public required string? ExternalId { get; init; }

    /// <summary>Current subscription status.</summary>
    public required string Status { get; init; }

    /// <summary>Classification relative to the requested source/target pair.</summary>
    public required BillingProviderMigrationEntryClassification Classification { get; init; }
}

/// <summary>
///     Report-only result of a provider-migration dry-run (issue #397). No state is
///     mutated; executing the actual migration is explicitly out of scope and deferred
///     (gateway routing/failover is tracked separately in #413).
/// </summary>
public sealed record BillingProviderMigrationReport
{
    /// <summary>Provider the subscriptions would be migrated away from.</summary>
    public required string SourceProvider { get; init; }

    /// <summary>Provider the subscriptions would be migrated to.</summary>
    public required string TargetProvider { get; init; }

    /// <summary>Always true: this report never mutates state.</summary>
    public required bool DryRun { get; init; }

    /// <summary>Always false: reassurance flag for API consumers.</summary>
    public required bool MutationApplied { get; init; }

    /// <summary>When the report was produced.</summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>Total subscriptions scanned.</summary>
    public required int TotalSubscriptionsScanned { get; init; }

    /// <summary>Subscriptions bound to the source provider lacking a target external identifier — the migration work list.</summary>
    public required int BoundToSourceMissingTargetExternalIdCount { get; init; }

    /// <summary>Subscriptions already carrying a target-provider external identifier.</summary>
    public required int AlreadyOnTargetProviderCount { get; init; }

    /// <summary>Subscriptions whose external identifier could not be attributed — manual review required.</summary>
    public required int UnattributedExternalIdCount { get; init; }

    /// <summary>Subscriptions without any external identifier (out of scope).</summary>
    public required int NotExternallyBoundCount { get; init; }

    /// <summary>
    ///     Classified subscription entries (capped for response size; the counts above
    ///     always reflect the full scan).
    /// </summary>
    public IReadOnlyList<BillingProviderMigrationReportEntry> Entries { get; init; } = [];

    /// <summary>Cross-reference: gateway-level failover and routing is tracked in gameguild issue #413.</summary>
    public const string FailoverTrackingNote =
        "Report-only dry-run. Provider switching is not executed here; gateway routing/failover is tracked in issue #413.";
}
