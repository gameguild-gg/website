using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

public partial class PermissionService
{
    /// <inheritdoc />
    public Task<TenantPermission> GrantTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions) =>
        ExecuteCompatibleMutationAsync(tenantId, () => GrantTenantPermissionCoreAsync(userId, tenantId, permissions));
    /// <inheritdoc />
    public Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(Guid[] userIds, Guid tenantId, PermissionType[] permissions) =>
        ExecuteCompatibleMutationAsync(tenantId, () => BulkGrantTenantPermissionCoreAsync(userIds, tenantId, permissions));
    /// <inheritdoc />
    public Task RevokeTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions) =>
        ExecuteCompatibleMutationAsync(tenantId, () => RevokeTenantPermissionCoreAsync(userId, tenantId, permissions));
    /// <inheritdoc />
    public Task<TenantPermission> JoinTenantAsync(Guid userId, Guid tenantId) =>
        ExecuteCompatibleMutationAsync(tenantId, () => JoinTenantCoreAsync(userId, tenantId));
    /// <inheritdoc />
    public Task LeaveTenantAsync(Guid userId, Guid tenantId) =>
        ExecuteCompatibleMutationAsync(tenantId, () => LeaveTenantCoreAsync(userId, tenantId));
    /// <inheritdoc />
    public Task<ContentTypePermission> GrantContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions) =>
        ExecuteCompatibleMutationAsync(tenantId, () => GrantContentTypePermissionCoreAsync(userId, tenantId, contentTypeName, permissions));
    /// <inheritdoc />
    public Task RevokeContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions) =>
        ExecuteCompatibleMutationAsync(tenantId, () => RevokeContentTypePermissionCoreAsync(userId, tenantId, contentTypeName, permissions));
    /// <inheritdoc />
    public Task BulkGrantResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid[] resourceIds, PermissionType[] permissions)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        ExecuteCompatibleMutationAsync(tenantId, () => BulkGrantResourcePermissionCoreAsync<TPermission, TResource>(userId, tenantId, resourceIds, permissions));
    /// <inheritdoc />
    public Task ShareResourceAsync<TPermission, TResource>(Guid resourceId, Guid targetUserId, Guid? tenantId, PermissionType[] permissions, DateTime? expiresAt = null)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        ExecuteCompatibleMutationAsync(tenantId, () => ShareResourceCoreAsync<TPermission, TResource>(resourceId, targetUserId, tenantId, permissions, expiresAt));
    /// <inheritdoc />
    public Task RevokeResourceAccessAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        ExecuteCompatibleMutationAsync(tenantId, () => RevokeResourceAccessCoreAsync<TPermission, TResource>(userId, tenantId, resourceId));
    /// <inheritdoc />
    public Task CleanupExpiredPermissionsAsync() =>
        ExecuteCompatibleMutationAsync(null, CleanupExpiredPermissionsCoreAsync, allTenants: true);

    private Task SetTenantGrantAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions, string reason) =>
        ExecuteCompatibleMutationAsync(tenantId, () => SetTenantGrantCoreAsync(userId, tenantId, permissions, reason));

    private Task ExecuteCompatibleMutationAsync(Guid? tenant, Func<Task> operation, bool allTenants = false) =>
        ExecuteCompatibleMutationAsync(tenant, async () => { await operation().ConfigureAwait(false); return true; }, allTenants);

    private async Task<T> ExecuteCompatibleMutationAsync<T>(Guid? tenant, Func<Task<T>> operation, bool allTenants = false)
    {
        var actor = RequireMutationScope(tenant, allTenants);
        if (context is not DbContext db || !db.Database.IsRelational())
        {
            // Compatible component providers retain their persistence behavior; this is not
            // an atomic-storage guarantee. The production relational host uses the path below.
            return await operation().ConfigureAwait(false);
        }
        var result = await new PermissionMutationExecutor(context).ExecuteAsync(actor, tenant,
            async _ => Result.Success(await operation().ConfigureAwait(false)), CancellationToken.None,
            useEntityTenantScope: allTenants).ConfigureAwait(false);
        if (result.IsSuccess) { return result.Value; }
        if (result.Error.Code == "PermissionWrite.Concurrency") { throw new DbUpdateConcurrencyException(result.Error.Description); }
        throw new InvalidOperationException(result.Error.Description);
    }
}
