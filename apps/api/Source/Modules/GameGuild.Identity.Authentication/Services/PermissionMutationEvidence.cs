using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>Required version and audit writes participating in the permission owner's transaction.</summary>
internal sealed class PermissionMutationEvidence(ITenantSecurityVersionStore versions, IPermissionAuditService audit)
{
    public async Task AdvanceVersionAsync(AuthorizedPermissionWriteScope scope, CancellationToken cancellationToken)
    {
        var version = await versions.IncrementVersionAsync((scope.TenantId ?? Guid.Empty).ToString("D"), cancellationToken).ConfigureAwait(false);
        if (version <= 0)
        {
            throw new InvalidOperationException("Permission invalidation did not advance the security version.");
        }
    }

    public async Task RecordAsync(PermissionBase grant, PermissionOperationType operation, AuthorizedPermissionWriteScope scope,
        string family, Guid? resourceId, string? before, string after, string? reason, CancellationToken cancellationToken)
    {
        var evidence = await audit.LogPermissionChangeAsync(operation, grant.UserId, scope.ActorId, scope.TenantId,
            permissionType: "GrantState", resourceId: resourceId, resourceType: family,
            oldValue: before, newValue: after, reason: reason ?? $"{operation} through permission service contract",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (evidence is null || evidence.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Permission mutation audit was not persisted.");
        }
    }
}
