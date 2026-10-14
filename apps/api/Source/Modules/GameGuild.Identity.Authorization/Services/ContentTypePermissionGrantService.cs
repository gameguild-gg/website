using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Mutation and read contract for the content-type grant layer's scoped store
///     (issues #360/#365). Every mutation is guarded (tenant scope), versioned (tenant
///     security version bump for cache invalidation) and audited
///     (<see cref="IPermissionAuditService"/> entries), mirroring the
///     <see cref="IPermissionGrantService"/> contract of the tenant family.
/// </summary>
public interface IContentTypePermissionGrantService
{
    /// <summary>
    ///     Grants content-type scoped permissions to a user (or, with a null user, sets
    ///     the tenant default for the content type). Existing rows are extended; a grant
    ///     clears matching deny entries on the row (allow replaces deny for the same
    ///     entry, mirroring the tenant family).
    /// </summary>
    Task<ContentTypePermissionGrant> GrantAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes content-type scoped permissions from a user (or the tenant default).
    ///     The row is soft-deleted once its payload is empty.
    /// </summary>
    Task<bool> RevokeAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds explicit deny entries (DENY-WINS) to a user's content-type grant row,
    ///     creating the row when absent.
    /// </summary>
    Task<ContentTypePermissionGrant> DenyAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        Guid? deniedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes deny entries from a user's content-type grant row. Returns false when
    ///     no row exists.
    /// </summary>
    Task<bool> RemoveDenyAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the live grant rows of one tenant, optionally narrowed to one content
    ///     type. Cross-tenant reads are the caller's responsibility (command-side guards).
    /// </summary>
    Task<List<ContentTypePermissionGrant>> GetGrantsAsync(
        Guid tenantId,
        string? contentTypeName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Guarded + versioned + audited implementation of the content-type grant store
///     (issues #360/#365). Mirrors <see cref="PermissionGrantService"/>: defense-in-depth
///     global-default authorization, tenant security-version bump on every mutation, a
///     <see cref="PermissionAuditLog"/> entry per change, and webhook fan-out.
/// </summary>
public sealed class ContentTypePermissionGrantService(
    IContentTypePermissionGrantRepository repository,
    IPermissionAuditService auditService,
    ITenantSecurityVersionStore securityVersionStore,
    IActorContextAccessor actorContextAccessor,
    ILogger<ContentTypePermissionGrantService> logger,
    IPermissionChangeNotifier? changeNotifier = null
) : IContentTypePermissionGrantService
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<ContentTypePermissionGrant> GrantAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentType = ValidateMutation(contentTypeName, permissions);
        if (expiresAt.HasValue && expiresAt.Value <= SystemClock.UtcNow)
        {
            throw new ArgumentException("The grant expiration must be in the future.", nameof(expiresAt));
        }

        logger.LogInformation(
            "Granting content-type permissions {Permissions} on {ContentType} to user {UserId} in tenant {TenantId}",
            string.Join(", ", permissions),
            normalizedContentType,
            userId,
            tenantId);

        var performedBy = grantedBy ?? Actor.SubjectIdAsGuid ?? Guid.Empty;
        var existing = await repository
            .GetBySubjectAndContentTypeAsync(userId, tenantId, normalizedContentType, cancellationToken)
            .ConfigureAwait(false);
        string[]? previousPermissions = existing?.Permissions.ToArray();
        ContentTypePermissionGrant result;

        if (existing is not null)
        {
            existing.AddPermissions(permissions);
            existing.RemoveDenyPermissions(permissions);
            existing.IsActive = true;
            existing.ExpiresAt = existing.ExpiresAt.HasValue && expiresAt.HasValue
                ? (existing.ExpiresAt.Value <= expiresAt.Value ? existing.ExpiresAt : expiresAt)
                : existing.ExpiresAt ?? expiresAt;
            existing.GrantedAt = SystemClock.UtcNow;
            existing.GrantedBy = performedBy;
            existing.Reason = reason;
            result = await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var grant = new ContentTypePermissionGrant
            {
                UserId = userId,
                TenantId = tenantId,
                ContentTypeName = normalizedContentType,
                GrantedAt = SystemClock.UtcNow,
                GrantedBy = performedBy,
                ExpiresAt = expiresAt,
                Reason = reason
            };
            grant.AddPermissions(permissions);
            result = await repository.CreateAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            userId,
            performedBy,
            tenantId,
            permissionType: "ContentType",
            resourceType: $"ContentTypePermissionGrant:{normalizedContentType}",
            oldValue: previousPermissions is null ? null : string.Join(",", previousPermissions),
            newValue: string.Join(",", result.Permissions),
            reason: reason,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await NotifyChangeAsync(
            PermissionChangeEventType.Granted,
            tenantId,
            userId,
            normalizedContentType,
            permissions,
            performedBy,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<bool> RevokeAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentType = ValidateMutation(contentTypeName, permissions);

        var existing = await repository
            .GetBySubjectAndContentTypeAsync(userId, tenantId, normalizedContentType, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        var previousPermissions = existing.Permissions.ToArray();
        existing.RemovePermissions(permissions);

        if (existing.Permissions.Length == 0)
        {
            await repository.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            userId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId,
            permissionType: "ContentType",
            resourceType: $"ContentTypePermissionGrant:{normalizedContentType}",
            oldValue: string.Join(",", previousPermissions),
            newValue: existing.Permissions.Length == 0 ? null : string.Join(",", existing.Permissions),
            reason: "Content-type permissions revoked",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await NotifyChangeAsync(
            PermissionChangeEventType.Revoked,
            tenantId,
            userId,
            normalizedContentType,
            permissions,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<ContentTypePermissionGrant> DenyAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        Guid? deniedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentType = ValidateMutation(contentTypeName, permissions);

        var performedBy = deniedBy ?? Actor.SubjectIdAsGuid ?? Guid.Empty;
        var existing = await repository
            .GetBySubjectAndContentTypeAsync(userId, tenantId, normalizedContentType, cancellationToken)
            .ConfigureAwait(false);
        ContentTypePermissionGrant result;

        if (existing is not null)
        {
            existing.AddDenyPermissions(permissions);
            existing.RemovePermissions(permissions);
            result = await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var grant = new ContentTypePermissionGrant
            {
                UserId = userId,
                TenantId = tenantId,
                ContentTypeName = normalizedContentType,
                GrantedAt = SystemClock.UtcNow,
                GrantedBy = performedBy,
                Reason = reason
            };
            grant.AddDenyPermissions(permissions);
            result = await repository.CreateAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            userId,
            performedBy,
            tenantId,
            permissionType: "ContentTypeDeny",
            resourceType: $"ContentTypePermissionGrant:{normalizedContentType}",
            oldValue: null,
            newValue: string.Join(",", result.DenyPermissions),
            reason: reason ?? "Content-type permissions denied (DENY-WINS)",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await NotifyChangeAsync(
            PermissionChangeEventType.Denied,
            tenantId,
            userId,
            normalizedContentType,
            permissions,
            performedBy,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<bool> RemoveDenyAsync(
        Guid? userId,
        Guid tenantId,
        string contentTypeName,
        string[] permissions,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentType = ValidateMutation(contentTypeName, permissions);

        var existing = await repository
            .GetBySubjectAndContentTypeAsync(userId, tenantId, normalizedContentType, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        existing.RemoveDenyPermissions(permissions);
        if (existing.Permissions.Length == 0 && existing.DenyPermissions.Length == 0)
        {
            await repository.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Update,
            userId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId,
            permissionType: "ContentTypeDeny",
            resourceType: $"ContentTypePermissionGrant:{normalizedContentType}",
            oldValue: null,
            newValue: existing.DenyPermissions.Length == 0 ? null : string.Join(",", existing.DenyPermissions),
            reason: "Content-type deny entries removed",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<List<ContentTypePermissionGrant>> GetGrantsAsync(
        Guid tenantId,
        string? contentTypeName = null,
        CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(contentTypeName)
            ? await repository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false)
            : await repository.GetByContentTypeAsync(tenantId, contentTypeName.Trim(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    ///     Normalizes and validates a mutation payload: non-blank content type, at least
    ///     one non-blank permission entry. Returns the normalized content-type name.
    /// </summary>
    private static string ValidateMutation(string contentTypeName, string[] permissions)
    {
        if (string.IsNullOrWhiteSpace(contentTypeName))
        {
            throw new ArgumentException("The content type name is required.", nameof(contentTypeName));
        }

        if (permissions is not { Length: > 0 } || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-blank permission is required.", nameof(permissions));
        }

        return contentTypeName.Trim();
    }

    /// <summary>
    ///     Bumps the tenant security version so cached permission decisions for the tenant
    ///     are invalidated (guarded-mutation contract).
    /// </summary>
    private async Task InvalidateTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var newVersion = await securityVersionStore
                .IncrementVersionAsync(tenantId.ToString(), cancellationToken)
                .ConfigureAwait(false);
            logger.LogDebug(
                "Incremented security version for tenant {TenantId} to {Version} after a content-type grant change",
                tenantId,
                newVersion);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to increment security version for tenant {TenantId} after a content-type grant change. Cache may be stale.",
                tenantId);
            throw;
        }
    }

    /// <summary>
    ///     Best-effort webhook fan-out; a notifier failure never alters the mutation
    ///     outcome (same contract as the tenant family).
    /// </summary>
    private async Task NotifyChangeAsync(
        PermissionChangeEventType eventType,
        Guid tenantId,
        Guid? userId,
        string contentTypeName,
        string[] permissions,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (changeNotifier is null)
        {
            return;
        }

        try
        {
            await changeNotifier
                .NotifyAsync(
                    new PermissionChangeEvent(
                        eventType,
                        tenantId,
                        userId,
                        $"ContentType:{contentTypeName}",
                        permissions,
                        performedBy),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Permission change notifier {NotifierType} failed for {EventType}; the permission change is unaffected.",
                changeNotifier.GetType().Name,
                eventType);
        }
    }
}
