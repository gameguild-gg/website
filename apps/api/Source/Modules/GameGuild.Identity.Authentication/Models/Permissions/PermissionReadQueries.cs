using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>Explicit read scope and bounded pagination shared by permission records and history.</summary>
public abstract record PermissionReadQuery
{
    /// <summary>Optional target tenant; defaults to the actor's tenant. Only SystemAdmin may cross tenants.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Explicitly select global rows. Requires SystemAdmin and no target TenantId.</summary>
    public bool GlobalScope { get; init; }

    /// <summary>Target subject; defaults to the authenticated actor, never to every user.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Explicitly include all non-default subjects; cannot be combined with UserId.</summary>
    public bool AllUsers { get; init; }

    /// <summary>Also include null-subject default rows. Requires administration of the selected scope.</summary>
    public bool IncludeDefaultSubjects { get; init; }

    /// <summary>One-based page number. Its computed offset must fit a signed 32-bit integer.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of rows per page, from 1 through 200; defaults to 50.</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>Direct grant filters. Soft-deleted rows are always excluded.</summary>
public abstract record PermissionGrantListQuery : PermissionReadQuery
{
    /// <summary>Include inactive/expired records as metadata; this never changes permission evaluation.</summary>
    public bool IncludeInactive { get; init; }

    /// <summary>Inclusive UTC grant-time lower bound.</summary>
    public DateTime? GrantedFromUtc { get; init; }

    /// <summary>Inclusive UTC grant-time upper bound.</summary>
    public DateTime? GrantedToUtc { get; init; }
}

/// <summary>Tenant-level direct-grant listing.</summary>
public sealed record TenantPermissionListQuery : PermissionGrantListQuery;

/// <summary>Content-type direct-grant listing.</summary>
public sealed record ContentTypePermissionListQuery : PermissionGrantListQuery
{
    /// <summary>Optional exact content-type name, from 1 through 256 nonblank characters.</summary>
    public string? ContentTypeName { get; init; }
}

/// <summary>Resource direct-grant listing for the generic entity selected by the caller's code.</summary>
public sealed record ResourcePermissionListQuery : PermissionGrantListQuery
{
    /// <summary>Optional nonempty resource identifier.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Optional exact stored resource type, from 1 through 256 nonblank characters.</summary>
    public string? ResourceType { get; init; }
}

/// <summary>Permission audit history with explicit inclusive UTC bounds.</summary>
public sealed record PermissionAuditHistoryQuery : PermissionReadQuery
{
    /// <summary>Inclusive UTC event-time lower bound.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Inclusive UTC event-time upper bound.</summary>
    public DateTime? ToUtc { get; init; }

    /// <summary>Optional defined operation kind.</summary>
    public PermissionOperationType? Operation { get; init; }

    /// <summary>Optional nonempty resource identifier.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>Optional exact stored resource type, from 1 through 256 nonblank characters.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Optional exact stored permission-type label; no enum value is guessed from it.</summary>
    public string? PermissionType { get; init; }

    /// <summary>Optional successful/failed operation filter.</summary>
    public bool? Success { get; init; }
}
