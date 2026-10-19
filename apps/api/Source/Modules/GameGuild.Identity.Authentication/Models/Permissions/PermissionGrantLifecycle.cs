using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>Authenticated target scope and creation payload; actor identity always comes from context.</summary>
public abstract record PermissionGrantCreation<TValue>
{
    /// <summary>Target tenant, defaulting to the actor's tenant; only SystemAdmin may select another tenant.</summary>
    public Guid? TenantId { get; init; }
    /// <summary>Explicit global target, requiring SystemAdmin or system:manage-global-defaults.</summary>
    public bool GlobalScope { get; init; }
    /// <summary>Explicit nonempty target user; must be absent when DefaultSubject is true.</summary>
    public Guid? UserId { get; init; }
    /// <summary>Explicitly create a null-subject default grant; supported for tenant/content-type grants.</summary>
    public bool DefaultSubject { get; init; }
    /// <summary>Nonempty allow payload, cloned and validated before storage.</summary>
    public required IReadOnlyCollection<TValue> Permissions { get; init; }
    /// <summary>Optional future UTC expiration; null creates a permanent grant.</summary>
    public DateTime? ExpiresAt { get; init; }
    /// <summary>Optional administration note, at most 500 characters.</summary>
    public string? Reason { get; init; }
}

/// <summary>Create one new tenant grant, preserving native custom permission keys.</summary>
public sealed record TenantPermissionCreation : PermissionGrantCreation<string>;

/// <summary>Create one new content-type grant with its exact stored type binding.</summary>
public sealed record ContentTypePermissionCreation : PermissionGrantCreation<PermissionType>
{
    /// <summary>Exact nonblank type name of at most 256 characters.</summary>
    public required string ContentTypeName { get; init; }
}

/// <summary>Create one new mapped resource grant for an explicit target user.</summary>
public sealed record ResourcePermissionCreation : PermissionGrantCreation<PermissionType>
{
    /// <summary>Nonempty resource identifier.</summary>
    public required Guid ResourceId { get; init; }
    /// <summary>Exact nonblank stored resource type, at most 256 characters.</summary>
    public required string ResourceType { get; init; }
}

/// <summary>Soft-delete one complete grant, including its allow/deny state, with optimistic concurrency.</summary>
/// <remarks>Inherited/default permissions still follow the existing evaluation engine after this grant is removed.</remarks>
public sealed record PermissionGrantDeletion
{
    /// <summary>Target tenant, defaulting to the actor's tenant.</summary>
    public Guid? TenantId { get; init; }
    /// <summary>Explicit global target requiring system-level authority.</summary>
    public bool GlobalScope { get; init; }
    /// <summary>Nonempty existing grant identifier.</summary>
    public required Guid GrantId { get; init; }
    /// <summary>Positive current entity version; stale versions return Conflict.</summary>
    public required int ExpectedVersion { get; init; }
    /// <summary>Optional bounded administration reason.</summary>
    public string? Reason { get; init; }
}

/// <summary>One ordered input outcome; rejected atomic batches retain every original index and failure.</summary>
public sealed record PermissionMutationItemResult<TGrant>(int Index, Result<TGrant> Result);

/// <summary>Atomic batch outcome with one result per input, in input order.</summary>
/// <remarks>
/// Applied=false means no batch writes survive. Applied=true means all items were written atomically;
/// standalone calls commit before returning, while an existing transaction's owner retains final commit responsibility.
/// Validation/conflict rejections retain per-item errors; cancellation/storage failures propagate instead of fabricating results.
/// </remarks>
public sealed record PermissionMutationBatchResult<TGrant>(bool Applied, IReadOnlyList<PermissionMutationItemResult<TGrant>> Items);
