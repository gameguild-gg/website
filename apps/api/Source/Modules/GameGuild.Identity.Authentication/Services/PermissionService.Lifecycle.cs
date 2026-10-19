using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

public partial class PermissionService
{
    /// <inheritdoc />
    public Task<Result<TenantPermission>> CreateTenantPermissionAsync(TenantPermissionCreation request) =>
        CreateTenantPermissionAsync(request, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<TenantPermission>> CreateTenantPermissionAsync(TenantPermissionCreation request, CancellationToken cancellationToken) =>
        _contractLifecycle.CreateTenantPermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<ContentTypePermission>> CreateContentTypePermissionAsync(ContentTypePermissionCreation request) =>
        CreateContentTypePermissionAsync(request, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<ContentTypePermission>> CreateContentTypePermissionAsync(ContentTypePermissionCreation request, CancellationToken cancellationToken) =>
        _contractLifecycle.CreateContentTypePermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<TPermission>> CreateResourcePermissionAsync<TPermission, TResource>(ResourcePermissionCreation request)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        CreateResourcePermissionAsync<TPermission, TResource>(request, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<TPermission>> CreateResourcePermissionAsync<TPermission, TResource>(ResourcePermissionCreation request, CancellationToken cancellationToken)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        _contractLifecycle.CreateResourcePermissionAsync<TPermission, TResource>(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<TenantPermission>> DeleteTenantPermissionAsync(PermissionGrantDeletion request) =>
        DeleteTenantPermissionAsync(request, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<TenantPermission>> DeleteTenantPermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken) =>
        _contractLifecycle.DeleteTenantPermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<ContentTypePermission>> DeleteContentTypePermissionAsync(PermissionGrantDeletion request) =>
        DeleteContentTypePermissionAsync(request, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<ContentTypePermission>> DeleteContentTypePermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken) =>
        _contractLifecycle.DeleteContentTypePermissionAsync(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<TPermission>> DeleteResourcePermissionAsync<TPermission, TResource>(PermissionGrantDeletion request)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        DeleteResourcePermissionAsync<TPermission, TResource>(request, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<TPermission>> DeleteResourcePermissionAsync<TPermission, TResource>(PermissionGrantDeletion request, CancellationToken cancellationToken)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        _contractLifecycle.DeleteResourcePermissionAsync<TPermission, TResource>(request, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkGrantTenantPermissionsAsync(IReadOnlyList<TenantPermissionCreation> requests) =>
        BulkGrantTenantPermissionsAsync(requests, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkGrantTenantPermissionsAsync(IReadOnlyList<TenantPermissionCreation> requests, CancellationToken cancellationToken) =>
        _contractLifecycle.BulkGrantTenantPermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkGrantContentTypePermissionsAsync(IReadOnlyList<ContentTypePermissionCreation> requests) =>
        BulkGrantContentTypePermissionsAsync(requests, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkGrantContentTypePermissionsAsync(IReadOnlyList<ContentTypePermissionCreation> requests, CancellationToken cancellationToken) =>
        _contractLifecycle.BulkGrantContentTypePermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkGrantResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<ResourcePermissionCreation> requests)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        BulkGrantResourcePermissionsAsync<TPermission, TResource>(requests, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkGrantResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<ResourcePermissionCreation> requests, CancellationToken cancellationToken)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        _contractLifecycle.BulkGrantResourcePermissionsAsync<TPermission, TResource>(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkRevokeTenantPermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests) =>
        BulkRevokeTenantPermissionsAsync(requests, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkRevokeTenantPermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken) =>
        _contractLifecycle.BulkRevokeTenantPermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkRevokeContentTypePermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests) =>
        BulkRevokeContentTypePermissionsAsync(requests, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkRevokeContentTypePermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken) =>
        _contractLifecycle.BulkRevokeContentTypePermissionsAsync(requests, cancellationToken);
    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkRevokeResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<PermissionGrantDeletion> requests)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        BulkRevokeResourcePermissionsAsync<TPermission, TResource>(requests, CancellationToken.None);

    /// <inheritdoc />
    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkRevokeResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        _contractLifecycle.BulkRevokeResourcePermissionsAsync<TPermission, TResource>(requests, cancellationToken);
}
