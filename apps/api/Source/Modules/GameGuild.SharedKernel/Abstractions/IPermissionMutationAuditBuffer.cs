namespace GameGuild;

/// <summary>Coordinates permission-audit evidence with a context-owned mutation transaction.</summary>
public interface IPermissionMutationAuditBuffer
{
    /// <summary>Bind an authenticated actor and target scope; multi-tenant maintenance may preserve each entity's own tenant instead.</summary>
    IPermissionMutationAuditScope BeginPermissionMutationAuditScope(Guid actorId, Guid? tenantId) =>
        BeginPermissionMutationAuditScope(actorId, tenantId, false);

    /// <summary>Bind an actor with either an explicit target or per-entity tenant attribution for maintenance.</summary>
    IPermissionMutationAuditScope BeginPermissionMutationAuditScope(Guid actorId, Guid? tenantId, bool useEntityTenantScope);

    /// <summary>Deliver staged evidence after the owner has committed its transaction.</summary>
    Task FlushPendingPermissionAuditChangesAsync() => FlushPendingPermissionAuditChangesAsync(CancellationToken.None);

    /// <summary>Deliver committed evidence with explicit cancellation.</summary>
    Task FlushPendingPermissionAuditChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Restores pending permission evidence and operation-event state after transaction rollback.</summary>
public interface IPermissionMutationAuditScope : IDisposable
{
    /// <summary>Restore the entry checkpoint without losing the caller's earlier evidence or operation state.</summary>
    void RestoreAfterRollback();
}
