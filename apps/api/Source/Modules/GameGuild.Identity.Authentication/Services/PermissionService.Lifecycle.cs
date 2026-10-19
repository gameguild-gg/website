using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

public partial class PermissionService
{
    /// <inheritdoc />
    public Task<Result<TenantPermission>> CreateTenantPermissionAsync(TenantPermissionCreation request, CancellationToken cancellationToken = default) =>
        _contractLifecycle.CreateTenantPermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<ContentTypePermission>> CreateContentTypePermissionAsync(ContentTypePermissionCreation request, CancellationToken cancellationToken = default) =>
        _contractLifecycle.CreateContentTypePermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<TPermission>> CreateResourcePermissionAsync<TPermission, TResource>(ResourcePermissionCreation request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        _contractLifecycle.CreateResourcePermissionAsync<TPermission, TResource>(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<TenantPermission>> DeleteTenantPermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken = default) =>
        _contractLifecycle.DeleteTenantPermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<ContentTypePermission>> DeleteContentTypePermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken = default) =>
        _contractLifecycle.DeleteContentTypePermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<TPermission>> DeleteResourcePermissionAsync<TPermission, TResource>(PermissionGrantDeletion request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        _contractLifecycle.DeleteResourcePermissionAsync<TPermission, TResource>(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkGrantTenantPermissionsAsync(IReadOnlyList<TenantPermissionCreation> requests, CancellationToken cancellationToken = default) =>
        _contractLifecycle.BulkGrantTenantPermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkGrantContentTypePermissionsAsync(IReadOnlyList<ContentTypePermissionCreation> requests, CancellationToken cancellationToken = default) =>
        _contractLifecycle.BulkGrantContentTypePermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkGrantResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<ResourcePermissionCreation> requests, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        _contractLifecycle.BulkGrantResourcePermissionsAsync<TPermission, TResource>(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkRevokeTenantPermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default) =>
        _contractLifecycle.BulkRevokeTenantPermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkRevokeContentTypePermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default) =>
        _contractLifecycle.BulkRevokeContentTypePermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkRevokeResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        _contractLifecycle.BulkRevokeResourcePermissionsAsync<TPermission, TResource>(requests, cancellationToken);
}
