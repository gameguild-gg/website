namespace GameGuild;

/// <summary>Coordinates permission-audit evidence with a context-owned mutation transaction.</summary>
public interface IPermissionMutationAuditBuffer
{
    /// <summary>Bind an authenticated mutation's actor and authorized target scope, restoring the caller's scope on disposal.</summary>
    IPermissionMutationAuditScope BeginPermissionMutationAuditScope(Guid actorId, Guid? tenantId);

    /// <summary>Deliver staged evidence after the owner has committed its transaction.</summary>
    Task FlushPendingPermissionAuditChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Restores pending permission evidence and operation-event state after transaction rollback.</summary>
public interface IPermissionMutationAuditScope : IDisposable
{
    /// <summary>Restore the entry checkpoint without losing the caller's earlier evidence or operation state.</summary>
    void RestoreAfterRollback();
}
