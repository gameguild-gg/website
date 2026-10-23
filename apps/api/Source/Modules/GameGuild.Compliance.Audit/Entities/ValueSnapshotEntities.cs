namespace GameGuild.Compliance.Audit;

/// <summary>
///     Before/after value snapshot for a single mutation of a single entity (issue #176).
///     Staged transactionally in the same <c>SaveChanges</c> call as the mutation itself by the
///     host capture pipeline, so a rolled-back transaction leaves no snapshot rows. Updated rows
///     store only the changed-field delta (delta compression); Added/Deleted rows store the full
///     state. Payloads larger than the configured threshold are truncated with the full-content
///     SHA-256 retained for integrity verification. Payloads are deduplicated into
///     <see cref="MutationValuePayloadChunk"/> rows by the compaction pass; after deduplication
///     (or archival) the inline payload columns are null and the chunk reference carries the content.
/// </summary>
public sealed class MutationValueSnapshot : EntityBase
{
    /// <summary>CLR type name of the mutated entity.</summary>
    public required string EntityName { get; set; }

    /// <summary>Primary key value(s) of the mutated entity, colon-joined for composite keys.</summary>
    public required string KeyId { get; set; }

    /// <summary>EF change kind: Added, Modified, or Deleted.</summary>
    public required string ChangeKind { get; set; }

    /// <summary>SHA-256 over the full original before-state JSON (before any truncation).</summary>
    public string? BeforeIntegritySha256 { get; set; }

    /// <summary>SHA-256 over the full original after-state JSON (before any truncation).</summary>
    public string? AfterIntegritySha256 { get; set; }

    /// <summary>SHA-256 over the stored (possibly truncated) before payload — the deduplication key.</summary>
    public string? BeforeStoredSha256 { get; set; }

    /// <summary>SHA-256 over the stored (possibly truncated) after payload — the deduplication key.</summary>
    public string? AfterStoredSha256 { get; set; }

    /// <summary>Byte length of the full original before-state JSON.</summary>
    public int BeforeBytes { get; set; }

    /// <summary>Byte length of the full original after-state JSON.</summary>
    public int AfterBytes { get; set; }

    /// <summary>Inline before payload (delta for Modified, full for Deleted); null after dedup/archive or when absent.</summary>
    public string? BeforePayload { get; set; }

    /// <summary>Inline after payload (delta for Modified, full for Added); null after dedup/archive or when absent.</summary>
    public string? AfterPayload { get; set; }

    /// <summary>Whether the inline before payload was truncated at the storage threshold.</summary>
    public bool BeforePayloadTruncated { get; set; }

    /// <summary>Whether the inline after payload was truncated at the storage threshold.</summary>
    public bool AfterPayloadTruncated { get; set; }

    /// <summary>Deduplicated before payload chunk, set by the compaction pass.</summary>
    public Guid? BeforeChunkId { get; set; }

    /// <summary>Deduplicated after payload chunk, set by the compaction pass.</summary>
    public Guid? AfterChunkId { get; set; }

    /// <summary>Structured field-level difference analysis (JSON array of field diffs, including nested paths).</summary>
    public string StructuredDiffJson { get; set; } = "[]";

    /// <summary>Number of field-level changes recorded in <see cref="StructuredDiffJson"/>.</summary>
    public int DiffCount { get; set; }

    /// <summary>Storage encoding: <c>Full</c> for Added/Deleted state, <c>Delta</c> for changed-field-only updates.</summary>
    public string StorageMode { get; set; } = "Full";

    /// <summary>When this snapshot was moved to the archive tier (inline payloads stripped, checksums retained).</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public Guid? ActorId { get; set; }

    /// <summary>Correlation id shared by every mutation in the same use-case operation — the cascade link for impact analysis.</summary>
    public string? CorrelationId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

/// <summary>
///     Content-addressed payload chunk used to deduplicate identical snapshot payloads (issue #176).
///     One chunk stores a payload once regardless of how many snapshots reference it; the
///     reference count is maintained by the compaction and retention passes.
/// </summary>
public sealed class MutationValuePayloadChunk : EntityBase
{
    /// <summary>SHA-256 over the stored payload content — unique per tenant.</summary>
    public required string ContentSha256 { get; set; }

    /// <summary>The deduplicated payload content.</summary>
    public required string Content { get; set; }

    /// <summary>Byte length of <see cref="Content"/>.</summary>
    public int LengthBytes { get; set; }

    /// <summary>Number of snapshot payload references pointing at this chunk.</summary>
    public long RefCount { get; set; }
}

/// <summary>
///     Configurable retention policy for mutation value snapshots (issue #176): retention days
///     (optionally per entity name), an archive tier cutoff that strips payloads down to
///     checksums, and a legal-hold window that suspends deletion.
/// </summary>
public sealed class ValueSnapshotRetentionPolicy : EntityBase
{
    public int RetentionDays { get; set; }

    /// <summary>Snapshots older than this many days are archived (payload stripped, checksums kept).</summary>
    public int ArchiveAfterDays { get; set; }

    /// <summary>JSON object of entity-name to retention-day overrides.</summary>
    public string EntityOverridesJson { get; set; } = "{}";

    /// <summary>While set in the future, every deletion for this tenant is suspended.</summary>
    public DateTime? LegalHoldUntilUtc { get; set; }

    /// <summary>Optimistic-concurrency revision; mutations must carry the observed value.</summary>
    public int Revision { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public DateTime ConfiguredAtUtc { get; set; }
}

/// <summary>Recorded outcome of one value-snapshot retention enforcement pass (manual, dry-run, or scheduled).</summary>
public sealed class ValueSnapshotRetentionExecution : EntityBase
{
    public DateTime ExecutedAtUtc { get; set; }

    public Guid TriggeredByUserId { get; set; }

    public bool DryRun { get; set; }

    public bool LegalHoldActive { get; set; }

    public int DeletedCount { get; set; }

    public int ArchivedCount { get; set; }

    public int CompactedCount { get; set; }

    public long BytesReclaimed { get; set; }
}

/// <summary>
///     A rollback plan for critical data recovery (issue #176): the reconstructed target state of
///     one entity at a chosen point in time, the inverse field operations that restore it, and the
///     audited execution outcome. Plans refuse to execute when newer changes exist after the basis
///     snapshot unless the caller explicitly accepts reverting them.
/// </summary>
public sealed class ValueSnapshotRollbackPlan : EntityBase
{
    public required string TargetEntityName { get; set; }

    public required string TargetKeyId { get; set; }

    /// <summary>The snapshot the reconstructed target state is based on (latest at or before the requested instant).</summary>
    public required Guid BasisSnapshotId { get; set; }

    /// <summary>Requested point-in-time instant for the rollback.</summary>
    public DateTime AsOfUtc { get; set; }

    /// <summary>Reconstructed full target state (JSON object of property to value).</summary>
    public required string BasisStateJson { get; set; }

    /// <summary>SHA-256 over <see cref="BasisStateJson"/> — the integrity anchor of the plan.</summary>
    public required string BasisStateSha256 { get; set; }

    /// <summary>Inverse operations as a JSON array: set/remove field ops plus delete-entity/reinsert-entity markers.</summary>
    public required string PlanJson { get; set; }

    /// <summary>Proposed, Executed, Failed, or Rejected.</summary>
    public string Status { get; set; } = "Proposed";

    public string? FailureReason { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExecutedAtUtc { get; set; }

    public Guid? ExecutedByUserId { get; set; }

    /// <summary>Per-operation execution outcome recorded when the plan runs.</summary>
    public string? ResultJson { get; set; }
}
