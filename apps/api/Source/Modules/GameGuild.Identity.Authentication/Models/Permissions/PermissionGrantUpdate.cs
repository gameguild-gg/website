namespace GameGuild.Identity.Authentication;

/// <summary>Full replacement of an existing grant's editable access state (issue #340).</summary>
/// <typeparam name="TValue">Native permission payload: strings for tenant grants, PermissionType for typed grants.</typeparam>
/// <remarks>Identity, subject, grantor and resource binding remain immutable. Null expiration makes the grant permanent.</remarks>
public sealed record PermissionGrantUpdate<TValue>
{
    /// <summary>Existing, non-deleted grant to update.</summary>
    public required Guid GrantId { get; init; }

    /// <summary>Target tenant; defaults to the actor's tenant. Only SystemAdmin can select another tenant.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Explicit global selection, requiring SystemAdmin or system:manage-global-defaults.</summary>
    public bool GlobalScope { get; init; }

    /// <summary>Positive current entity version obtained from a read. A stale version returns Conflict.</summary>
    public required int ExpectedVersion { get; init; }

    /// <summary>Complete replacement allow payload. An empty collection clears allows; tenant denies are preserved.</summary>
    public required IReadOnlyCollection<TValue> Permissions { get; init; }

    /// <summary>Whether the grant is active after this update.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Optional UTC deadline; active grants require a future deadline.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Optional replacement administration note/audit reason, up to 500 characters.</summary>
    public string? Reason { get; init; }
}
