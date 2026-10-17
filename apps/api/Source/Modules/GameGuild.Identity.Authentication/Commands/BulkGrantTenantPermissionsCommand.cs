using GameGuild.Identity.Authorization;
﻿using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record BulkGrantTenantPermissionsCommand : ICommand<BulkPermissionResult>
{
    public List<Guid> UserIds { get; init; } = new List<Guid>();

    public Guid TenantId { get; init; }

    public List<PermissionType> Permissions { get; init; } = new List<PermissionType>();

    public DateTime? ExpiresAt { get; init; }

    public string? GrantedBy { get; init; }

    public string? Reason { get; init; }

    /// <summary>
    ///     All-or-nothing mode (issues #302/#365): when set, the first per-user failure
    ///     aborts the bulk grant and throws, rolling back the ambient transaction so no
    ///     user keeps a partial grant. The default (unset) is per-item semantics: each
    ///     user's outcome is reported independently and successful items commit.
    /// </summary>
    public bool Atomic { get; init; }
}
