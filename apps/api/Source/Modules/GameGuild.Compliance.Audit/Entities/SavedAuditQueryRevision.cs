namespace GameGuild.Compliance.Audit;

/// <summary>
///     Immutable revision snapshot of a <see cref="SavedAuditQuery"/>. A new revision is written
///     whenever a saved view is created or updated, giving auditors the full change history of
///     every investigation view: who changed it, when, and the exact query DSL payload.
/// </summary>
public sealed class SavedAuditQueryRevision : EntityBase
{
    public new Guid? TenantId { get; private set; }

    /// <summary> owning saved view identifier. </summary>
    public Guid SavedAuditQueryId { get; private set; }

    /// <summary> Monotonic revision number starting at 1 on creation. </summary>
    public int Revision { get; private set; }

    /// <summary> Canonical query DSL JSON captured for this revision. </summary>
    public string QueryDsl { get; private set; } = string.Empty;

    /// <summary> Display name captured for this revision. </summary>
    public string QueryName { get; private set; } = string.Empty;

    /// <summary> Description captured for this revision. </summary>
    public string? Description { get; private set; }

    /// <summary> Sharing flag captured for this revision. </summary>
    public bool IsPublic { get; private set; }

    /// <summary> User who authored this revision. </summary>
    public Guid ChangedByUserId { get; private set; }

    /// <summary> True when the revision was produced by the initial create. </summary>
    public bool IsCreation { get; private set; }

    private SavedAuditQueryRevision() { }

    public static SavedAuditQueryRevision Capture(
        Guid tenantId,
        Guid savedAuditQueryId,
        int revision,
        string queryName,
        string? description,
        string queryDsl,
        bool isPublic,
        Guid changedByUserId,
        bool isCreation)
    {
        return new SavedAuditQueryRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SavedAuditQueryId = savedAuditQueryId,
            Revision = revision,
            QueryName = queryName,
            Description = description,
            QueryDsl = queryDsl,
            IsPublic = isPublic,
            ChangedByUserId = changedByUserId,
            IsCreation = isCreation
        };
    }
}
