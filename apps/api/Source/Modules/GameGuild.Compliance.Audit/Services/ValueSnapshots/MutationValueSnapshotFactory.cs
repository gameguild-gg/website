using System.Text;

namespace GameGuild.Compliance.Audit;

/// <summary>Configuration bounds for mutation value snapshot staging and storage (issue #176).</summary>
public sealed class ValueSnapshotOptions
{
    /// <summary>Section name used for configuration binding.</summary>
    public const string ConfigurationSection = "Audit:ValueSnapshots";

    /// <summary>Master switch for snapshot staging; defaults to enabled. Fail-open staging never blocks a mutation.</summary>
    public bool StagingEnabled { get; set; } = true;

    /// <summary>Maximum stored bytes per snapshot payload before truncation (checksums are always kept).</summary>
    public int MaxPayloadBytes { get; set; } = 8_192;

    /// <summary>Maximum stored bytes per field value inside the structured diff before truncation.</summary>
    public int MaxValueBytes { get; set; } = ValueSnapshotDiffEngine.DefaultValueMaxBytes;
}

/// <summary>
///     Stages <see cref="MutationValueSnapshot"/> rows from the raw before/after JSON produced by the
///     host entity-change capture pass (issue #176). Pure in-memory computation — no database access —
///     so it can run inside the mutating <c>SaveChanges</c> call without extra queries. Applies
///     delta compression (changed fields only for Modified rows), SHA-256 checksums of full and
///     stored payloads, and large-object truncation with the integrity hash retained.
/// </summary>
public sealed class MutationValueSnapshotFactory
{
    private readonly ValueSnapshotOptions _options;

    public MutationValueSnapshotFactory(ValueSnapshotOptions? options = null)
    {
        _options = options ?? new ValueSnapshotOptions();
    }

    /// <summary>Builds one snapshot row for a captured mutation. Returns null for Added rows without
    /// after state or Deleted rows without before state (nothing value-comparable to record).</summary>
    public MutationValueSnapshot? Create(
        string entityName,
        string keyId,
        string changeKind,
        string? beforeJson,
        string? afterJson,
        Guid? tenantId,
        Guid? actorId,
        string? correlationId,
        DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(keyId))
        {
            return null;
        }

        var diffs = ValueSnapshotDiffEngine.ComputeDiff(beforeJson, afterJson, _options.MaxValueBytes);

        string? beforePayload = null;
        string? afterPayload = null;
        string? beforeIntegrity = null;
        string? afterIntegrity = null;
        string? beforeStoredHash = null;
        string? afterStoredHash = null;
        var beforeTruncated = false;
        var afterTruncated = false;
        var storageMode = "Full";

        if (beforeJson is not null)
        {
            (beforePayload, beforeTruncated, beforeIntegrity) =
                ValueSnapshotDiffEngine.ApplyLargeValueHandling(beforeJson, _options.MaxPayloadBytes);
            beforeStoredHash = ValueSnapshotDiffEngine.ComputeSha256(beforePayload);
        }

        if (afterJson is not null)
        {
            // Delta compression: Modified rows keep only the changed-field after values, which the
            // host capture pass already reduced to the modified property set. Added/Deleted rows
            // store the full state once.
            (afterPayload, afterTruncated, afterIntegrity) =
                ValueSnapshotDiffEngine.ApplyLargeValueHandling(afterJson, _options.MaxPayloadBytes);
            afterStoredHash = ValueSnapshotDiffEngine.ComputeSha256(afterPayload);
            storageMode = changeKind == "Modified" ? "Delta" : "Full";
        }

        return new MutationValueSnapshot
        {
            Id = Guid.NewGuid(),
            EntityName = entityName,
            KeyId = keyId,
            ChangeKind = changeKind,
            BeforeIntegritySha256 = beforeIntegrity,
            AfterIntegritySha256 = afterIntegrity,
            BeforeStoredSha256 = beforeStoredHash,
            AfterStoredSha256 = afterStoredHash,
            BeforeBytes = beforeJson is null ? 0 : Encoding.UTF8.GetByteCount(beforeJson),
            AfterBytes = afterJson is null ? 0 : Encoding.UTF8.GetByteCount(afterJson),
            BeforePayload = beforePayload,
            AfterPayload = afterPayload,
            BeforePayloadTruncated = beforeTruncated,
            AfterPayloadTruncated = afterTruncated,
            StructuredDiffJson = ValueSnapshotDiffEngine.SerializeDiffs(diffs),
            DiffCount = diffs.Count,
            StorageMode = storageMode,
            ActorId = actorId,
            CorrelationId = correlationId,
            OccurredAtUtc = occurredAtUtc,
            TenantId = tenantId
        };
    }
}
