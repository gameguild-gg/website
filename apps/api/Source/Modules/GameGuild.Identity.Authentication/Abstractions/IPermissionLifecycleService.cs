using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>Typed create/delete and atomic grant/revoke batches for the three existing grant families.</summary>
/// <remarks>
/// Creates return Conflict for an existing non-deleted subject/binding instead of silently merging or extending its lifetime.
/// Delete soft-deletes the complete grant. Each batch contains 1-256 items in one authorized target scope; no input is discarded.
/// Inspect Applied and every item result. Expected rejections roll back all writes; provider errors/cancellation propagate.
/// The relational transaction, caller-work, rollback, audit and retry rules of IPermissionUpdateService also apply.
/// </remarks>
public interface IPermissionLifecycleService
{
    /// <summary>Create one new tenant grant with native string permissions.</summary>
    /// <param name="request">Explicit subject/default selection, authorized target scope and allow payload.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>The saved grant or a typed expected failure.</returns>
    Task<Result<TenantPermission>> CreateTenantPermissionAsync(TenantPermissionCreation request, CancellationToken cancellationToken = default);

    /// <summary>Create one new content-type grant.</summary>
    /// <param name="request">Explicit subject/default selection, scope, type binding and allow values.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>The saved grant or a typed expected failure.</returns>
    Task<Result<ContentTypePermission>> CreateContentTypePermissionAsync(ContentTypePermissionCreation request, CancellationToken cancellationToken = default);

    /// <summary>Create one new grant in an already mapped resource permission entity.</summary>
    /// <typeparam name="TPermission">Mapped resource permission entity with a parameterless constructor.</typeparam>
    /// <typeparam name="TResource">Its existing resource entity type.</typeparam>
    /// <param name="request">Explicit target user, scope, resource binding and allow values.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>The saved grant or a typed expected failure.</returns>
    Task<Result<TPermission>> CreateResourcePermissionAsync<TPermission, TResource>(ResourcePermissionCreation request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>Soft-delete one complete tenant grant at its expected version.</summary>
    /// <param name="request">Grant identity, authorized target scope and positive expected version.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>The saved deleted grant or a typed expected failure.</returns>
    Task<Result<TenantPermission>> DeleteTenantPermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete one complete content-type grant at its expected version.</summary>
    /// <param name="request">Grant identity, authorized target scope and positive expected version.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>The saved deleted grant or a typed expected failure.</returns>
    Task<Result<ContentTypePermission>> DeleteContentTypePermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete one complete mapped resource grant at its expected version.</summary>
    /// <typeparam name="TPermission">Already mapped resource permission entity.</typeparam>
    /// <typeparam name="TResource">Its existing resource entity type.</typeparam>
    /// <param name="request">Grant identity, authorized target scope and positive expected version.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>The saved deleted grant or a typed expected failure.</returns>
    Task<Result<TPermission>> DeleteResourcePermissionAsync<TPermission, TResource>(PermissionGrantDeletion request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase;

    /// <summary>Atomically create 1-256 tenant grants with indexed outcomes and one scope-version bump.</summary>
    /// <param name="requests">All inputs, in one authorized scope; duplicates remain indexed errors.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>All ordered outcomes; inspect Applied and item results.</returns>
    Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkGrantTenantPermissionsAsync(IReadOnlyList<TenantPermissionCreation> requests, CancellationToken cancellationToken = default);

    /// <summary>Atomically create 1-256 content-type grants with indexed outcomes.</summary>
    /// <param name="requests">All subject/type inputs in one authorized scope.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>All ordered outcomes; inspect Applied and item results.</returns>
    Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkGrantContentTypePermissionsAsync(IReadOnlyList<ContentTypePermissionCreation> requests, CancellationToken cancellationToken = default);

    /// <summary>Atomically create 1-256 mapped resource grants with indexed outcomes.</summary>
    /// <typeparam name="TPermission">Mapped resource permission entity with a parameterless constructor.</typeparam>
    /// <typeparam name="TResource">Its existing resource entity type.</typeparam>
    /// <param name="requests">All subject/resource inputs in one authorized scope.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>All ordered outcomes; inspect Applied and item results.</returns>
    Task<Result<PermissionMutationBatchResult<TPermission>>> BulkGrantResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<ResourcePermissionCreation> requests, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>Atomically soft-delete 1-256 tenant grants, preserving every indexed validation/conflict result.</summary>
    /// <param name="requests">Distinct grant identities and current versions in one authorized scope.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>All ordered outcomes; no batch writes survive when Applied is false.</returns>
    Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkRevokeTenantPermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default);

    /// <summary>Atomically soft-delete 1-256 content-type grants.</summary>
    /// <param name="requests">Distinct grant identities and current versions in one authorized scope.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>All ordered outcomes; no batch writes survive when Applied is false.</returns>
    Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkRevokeContentTypePermissionsAsync(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default);

    /// <summary>Atomically soft-delete 1-256 mapped resource grants.</summary>
    /// <typeparam name="TPermission">Already mapped resource permission entity.</typeparam>
    /// <typeparam name="TResource">Its existing resource entity type.</typeparam>
    /// <param name="requests">Distinct grant identities and current versions in one authorized scope.</param>
    /// <param name="cancellationToken">Cancels persistence without cancelling rollback cleanup.</param>
    /// <returns>All ordered outcomes; no batch writes survive when Applied is false.</returns>
    Task<Result<PermissionMutationBatchResult<TPermission>>> BulkRevokeResourcePermissionsAsync<TPermission, TResource>(IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase;
}
