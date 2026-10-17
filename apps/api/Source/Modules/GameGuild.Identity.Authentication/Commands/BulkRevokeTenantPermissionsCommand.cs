using GameGuild.Identity.Authorization;
﻿using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed record BulkRevokeTenantPermissionsCommand : ICommand<BulkPermissionResult>
{
    public List<Guid> UserIds { get; init; } = new List<Guid>();

    public Guid TenantId { get; init; }

    public List<PermissionType> Permissions { get; init; } = new List<PermissionType>();

    public string? RevokedBy { get; init; }

    public string? Reason { get; init; }

    /// <summary>
    ///     All-or-nothing mode (issues #302/#365): when set, the first per-user failure
    ///     aborts the bulk revoke and throws, rolling back the ambient transaction so the
    ///     operation is atomic. The default (unset) is per-item semantics: each user's
    ///     outcome is reported independently and successful revocations commit.
    /// </summary>
    public bool Atomic { get; init; }
}
