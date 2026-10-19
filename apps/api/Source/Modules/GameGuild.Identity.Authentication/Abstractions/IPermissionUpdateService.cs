using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>Guarded, version-checked updates for each existing permission family.</summary>
/// <remarks>
/// Expected validation, access, missing-record and concurrency outcomes use Result. Persistence failures and
/// cancellation propagate. A relational context is required, with no other unsaved mutations or queued events.
/// Standalone calls own one atomic transaction and are not automatically replayed after ambiguous commit errors.
/// An existing transaction uses a savepoint; its owner retains commit, retry and final evidence-delivery responsibility.
/// </remarks>
public interface IPermissionUpdateService
{
    /// <summary>Replace tenant allow strings and activation/lifetime/note, preserving denies and grant identity.</summary>
    /// <param name="request">Validated target scope, expected version and replacement native string payload.</param>
    /// <returns>The saved grant or a typed expected failure. Null requests throw ArgumentNullException.</returns>
    Task<Result<TenantPermission>> UpdateTenantPermissionAsync(PermissionGrantUpdate<string> request) =>
        UpdateTenantPermissionAsync(request, CancellationToken.None);

    /// <summary>Replace tenant allow strings and activation/lifetime/note, preserving denies and grant identity.</summary>
    /// <param name="request">Validated target scope, expected version and replacement native string payload.</param>
    /// <param name="cancellationToken">Cancels storage work without cancelling rollback cleanup.</param>
    /// <returns>The saved grant or a typed expected failure. Null requests throw ArgumentNullException.</returns>
    Task<Result<TenantPermission>> UpdateTenantPermissionAsync(
        PermissionGrantUpdate<string> request, CancellationToken cancellationToken);

    /// <summary>Replace a content-type grant's allow values and activation/lifetime/note.</summary>
    /// <param name="request">Scope, expected version and defined permission enum values.</param>
    /// <returns>The saved grant or a typed expected failure; the registered content-type binding is unchanged.</returns>
    Task<Result<ContentTypePermission>> UpdateContentTypePermissionAsync(PermissionGrantUpdate<PermissionType> request) =>
        UpdateContentTypePermissionAsync(request, CancellationToken.None);

    /// <summary>Replace a content-type grant's allow values and activation/lifetime/note.</summary>
    /// <param name="request">Scope, expected version and defined permission enum values.</param>
    /// <param name="cancellationToken">Cancels storage work without cancelling rollback cleanup.</param>
    /// <returns>The saved grant or a typed expected failure; the registered content-type binding is unchanged.</returns>
    Task<Result<ContentTypePermission>> UpdateContentTypePermissionAsync(
        PermissionGrantUpdate<PermissionType> request, CancellationToken cancellationToken);

    /// <summary>Replace a typed resource grant's allow values and activation/lifetime/note.</summary>
    /// <typeparam name="TPermission">Already mapped resource permission entity.</typeparam>
    /// <typeparam name="TResource">Its existing resource entity type.</typeparam>
    /// <param name="request">Scope, expected version and defined permission enum values.</param>
    /// <returns>The saved grant or a typed expected failure; resource identity/type and grantor are unchanged.</returns>
    Task<Result<TPermission>> UpdateResourcePermissionAsync<TPermission, TResource>(PermissionGrantUpdate<PermissionType> request)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        UpdateResourcePermissionAsync<TPermission, TResource>(request, CancellationToken.None);

    /// <summary>Replace a typed resource grant's allow values and activation/lifetime/note.</summary>
    /// <typeparam name="TPermission">Already mapped resource permission entity.</typeparam>
    /// <typeparam name="TResource">Its existing resource entity type.</typeparam>
    /// <param name="request">Scope, expected version and defined permission enum values.</param>
    /// <param name="cancellationToken">Cancels storage work without cancelling rollback cleanup.</param>
    /// <returns>The saved grant or a typed expected failure; resource identity/type and grantor are unchanged.</returns>
    Task<Result<TPermission>> UpdateResourcePermissionAsync<TPermission, TResource>(
        PermissionGrantUpdate<PermissionType> request, CancellationToken cancellationToken)
        where TPermission : ResourcePermission<TResource>
        where TResource : EntityBase;
}
