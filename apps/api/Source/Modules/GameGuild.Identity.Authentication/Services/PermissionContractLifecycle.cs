using System.Linq.Expressions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>Atomic lifecycle operations over the existing grant entities and evaluation model.</summary>
internal sealed class PermissionContractLifecycle(IApplicationDbContext context, ITenantSecurityVersionStore? versions,
    IPermissionAuditService? audit, IActorContextAccessor? actors) : IPermissionLifecycleService
{
    public Task<Result<TenantPermission>> CreateTenantPermissionAsync(TenantPermissionCreation request, CancellationToken cancellationToken = default) =>
        SingleAsync(request, value => BulkGrantTenantPermissionsAsync([value], cancellationToken));

    public Task<Result<ContentTypePermission>> CreateContentTypePermissionAsync(ContentTypePermissionCreation request, CancellationToken cancellationToken = default) =>
        SingleAsync(request, value => BulkGrantContentTypePermissionsAsync([value], cancellationToken));

    public Task<Result<TPermission>> CreateResourcePermissionAsync<TPermission, TResource>(ResourcePermissionCreation request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        SingleAsync(request, value => BulkGrantResourcePermissionsAsync<TPermission, TResource>([value], cancellationToken));

    public Task<Result<TenantPermission>> DeleteTenantPermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken = default) =>
        SingleAsync(request, value => BulkRevokeTenantPermissionsAsync([value], cancellationToken));

    public Task<Result<ContentTypePermission>> DeleteContentTypePermissionAsync(PermissionGrantDeletion request, CancellationToken cancellationToken = default) =>
        SingleAsync(request, value => BulkRevokeContentTypePermissionsAsync([value], cancellationToken));

    public Task<Result<TPermission>> DeleteResourcePermissionAsync<TPermission, TResource>(PermissionGrantDeletion request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        SingleAsync(request, value => BulkRevokeResourcePermissionsAsync<TPermission, TResource>([value], cancellationToken));

    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkGrantTenantPermissionsAsync(
        IReadOnlyList<TenantPermissionCreation> requests, CancellationToken cancellationToken = default) =>
        CreateAsync<TenantPermission, TenantPermissionCreation, string>(requests,
            tenant => context.Set<TenantPermission>().Where(row => row.TenantId == tenant),
            (rows, _) => rows, row => new GrantBinding(row.UserId, null, null), request => new GrantBinding(request.UserId, null, null),
            (_, values) => ValidateStrings(values), (request, scope, values) => new TenantPermission
            {
                UserId = request.UserId, TenantId = scope.TenantId, Permissions = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                GrantedBy = scope.ActorId, GrantedAt = SystemClock.UtcNow, ExpiresAt = request.ExpiresAt, Reason = request.Reason
            }, grant => PermissionService.CapturePermissionSnapshot(grant), nameof(TenantPermission), _ => null, cancellationToken);

    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkGrantContentTypePermissionsAsync(
        IReadOnlyList<ContentTypePermissionCreation> requests, CancellationToken cancellationToken = default) =>
        CreateAsync<ContentTypePermission, ContentTypePermissionCreation, PermissionType>(requests,
            tenant => context.Set<ContentTypePermission>().Where(row => row.TenantId == tenant),
            (rows, items) => { var names = items.Select(item => item.ContentTypeName).Distinct().ToArray(); return rows.Where(row => names.Contains(row.ContentTypeName)); },
            row => new GrantBinding(row.UserId, row.ContentTypeName, null), request => new GrantBinding(request.UserId, request.ContentTypeName, null),
            (request, values) => ValidateTyped(values) ?? ValidateName(request.ContentTypeName),
            (request, scope, values) =>
            {
                var grant = new ContentTypePermission(request.UserId, scope.TenantId, request.ContentTypeName)
                    { GrantedBy = scope.ActorId, ExpiresAt = request.ExpiresAt, Notes = request.Reason };
                grant.SetPermissions(values.Distinct());
                return grant;
            }, grant => PermissionService.CapturePermissionSnapshot(grant), nameof(ContentTypePermission), _ => null, cancellationToken);

    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkGrantResourcePermissionsAsync<TPermission, TResource>(
        IReadOnlyList<ResourcePermissionCreation> requests, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase =>
        CreateAsync<TPermission, ResourcePermissionCreation, PermissionType>(requests,
            tenant => context.Set<TPermission>().Where(row => row.TenantId == tenant),
            (rows, items) =>
            {
                var ids = items.Select(item => item.ResourceId).Distinct().ToArray();
                var types = items.Select(item => item.ResourceType).Distinct().ToArray();
                return rows.Where(row => ids.Contains(row.ResourceId) && types.Contains(row.ResourceType));
            }, row => new GrantBinding(row.UserId, row.ResourceType, row.ResourceId), request => new GrantBinding(request.UserId, request.ResourceType, request.ResourceId),
            (request, values) => ValidateTyped(values) ?? ValidateName(request.ResourceType)
                ?? (request.ResourceId == Guid.Empty || request.DefaultSubject ? Invalid("A resource grant requires a resource id and an explicit user.") : null),
            (request, scope, values) =>
            {
                var grant = new TPermission
                {
                    UserId = request.UserId, TenantId = scope.TenantId, ResourceId = request.ResourceId, ResourceType = request.ResourceType,
                    GrantedBy = scope.ActorId, GrantedAt = SystemClock.UtcNow, ExpiresAt = request.ExpiresAt, Notes = request.Reason
                };
                grant.SetPermissions(values.Distinct());
                return grant;
            }, grant => PermissionService.CapturePermissionSnapshot(grant, grant.ResourceType, grant.ResourceId),
            nameof(ResourcePermission<TResource>), grant => grant.ResourceId, cancellationToken);

    public Task<Result<PermissionMutationBatchResult<TenantPermission>>> BulkRevokeTenantPermissionsAsync(
        IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default) =>
        DeleteAsync(requests, tenant => context.Set<TenantPermission>().Where(row => row.TenantId == tenant),
            grant => PermissionService.CapturePermissionSnapshot(grant), nameof(TenantPermission), _ => null, cancellationToken);

    public Task<Result<PermissionMutationBatchResult<ContentTypePermission>>> BulkRevokeContentTypePermissionsAsync(
        IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default) =>
        DeleteAsync(requests, tenant => context.Set<ContentTypePermission>().Where(row => row.TenantId == tenant),
            grant => PermissionService.CapturePermissionSnapshot(grant), nameof(ContentTypePermission), _ => null, cancellationToken);

    public Task<Result<PermissionMutationBatchResult<TPermission>>> BulkRevokeResourcePermissionsAsync<TPermission, TResource>(
        IReadOnlyList<PermissionGrantDeletion> requests, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource> where TResource : EntityBase =>
        DeleteAsync(requests, tenant => context.Set<TPermission>().Where(row => row.TenantId == tenant),
            grant => PermissionService.CapturePermissionSnapshot(grant, grant.ResourceType, grant.ResourceId),
            nameof(ResourcePermission<TResource>), grant => grant.ResourceId, cancellationToken);

    private async Task<Result<PermissionMutationBatchResult<TGrant>>> CreateAsync<TGrant, TRequest, TValue>(
        IReadOnlyList<TRequest> requests, Func<Guid?, IQueryable<TGrant>> scopedRows,
        Func<IQueryable<TGrant>, TRequest[], IQueryable<TGrant>> restrictTargets,
        Expression<Func<TGrant, GrantBinding>> bindingProjection, Func<TRequest, GrantBinding> binding,
        Func<TRequest, TValue[], Error?> validatePayload, Func<TRequest, AuthorizedPermissionWriteScope, TValue[], TGrant> create,
        Func<TGrant, string> snapshot, string family, Func<TGrant, Guid?> resourceId, CancellationToken cancellationToken)
        where TGrant : PermissionBase where TRequest : PermissionGrantCreation<TValue>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count is < 1 or > 256) { return InvalidBatch<TGrant>(); }
        var items = requests.ToArray();
        if (items.Length is < 1 or > 256) { return InvalidBatch<TGrant>(); }
        var errors = new Dictionary<int, Error>();
        var scopes = new AuthorizedPermissionWriteScope[items.Length];
        var payloads = new TValue[items.Length][];
        var seen = new Dictionary<GrantBinding, int>();
        for (var index = 0; index < items.Length; index++)
        {
            var request = items[index];
            if (request is null) { errors[index] = Invalid("A batch item cannot be null."); continue; }
            var scope = PermissionWriteAuthorization.Resolve(actors, request.TenantId, request.GlobalScope);
            if (scope.IsFailure) { errors[index] = scope.Error; continue; }
            scopes[index] = scope.Value;
            if (request.UserId == Guid.Empty || request.DefaultSubject == request.UserId.HasValue
                || request.Permissions is null || request.Reason is { Length: > 500 }
                || request.ExpiresAt is { Kind: not DateTimeKind.Utc } || request.ExpiresAt <= SystemClock.UtcNow)
            {
                errors[index] = Invalid("An explicit user/default choice, nonempty payload, bounded reason and future UTC lifetime are required.");
                continue;
            }
            // Clone before the first await; caller-owned collections cannot change the saved payload.
            payloads[index] = request.Permissions.ToArray();
            var payloadError = payloads[index].Length == 0 ? Invalid("A creation requires a nonempty allow payload.") : validatePayload(request, payloads[index]);
            if (payloadError is not null) { errors[index] = payloadError; continue; }
            var key = binding(request);
            if (seen.TryGetValue(key, out var first))
            {
                errors[first] = errors[index] = Invalid("The same subject/binding appears more than once in the batch.");
            }
            else { seen.Add(key, index); }
        }
        if (errors.Count > 0) { return Result.Success(Rejected<TGrant>(items.Length, errors)); }
        if (scopes.Any(scope => scope != scopes[0])) { return MixedScope<TGrant>(); }
        if (versions is null || audit is null) { return MissingCollaborators<TGrant>(); }
        var target = scopes[0];
        var evidence = new PermissionMutationEvidence(versions, audit);
        return await new PermissionMutationExecutor(context).ExecuteAsync(target.ActorId, target.TenantId, async token =>
        {
            // Version writes serialize competing creations in this scope before checking bindings.
            // Optimistic version/initial-row conflicts roll back the entire transaction without replay.
            await evidence.AdvanceVersionAsync(target, token).ConfigureAwait(false);
            var users = items.Where(item => item.UserId.HasValue).Select(item => item.UserId!.Value).Distinct().ToArray();
            var defaults = items.Any(item => item.DefaultSubject);
            var rows = scopedRows(target.TenantId).AsNoTracking().Where(row => row.DeletedAt == null &&
                (defaults && row.UserId == null || row.UserId.HasValue && users.Contains(row.UserId.Value)));
            var existing = (await restrictTargets(rows, items).Select(bindingProjection).Distinct().ToListAsync(token).ConfigureAwait(false)).ToHashSet();
            for (var index = 0; index < items.Length; index++)
            {
                if (existing.Contains(binding(items[index])))
                {
                    errors[index] = Error.Conflict("PermissionWrite.AlreadyExists", "A non-deleted grant already exists for this subject and binding.");
                }
            }
            if (errors.Count > 0) { return Result.Success(Rejected<TGrant>(items.Length, errors)); }
            var grants = items.Select((item, index) => create(item, target, payloads[index])).ToArray();
            context.Set<TGrant>().AddRange(grants);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            for (var index = 0; index < grants.Length; index++)
            {
                await ((DbContext)context).Entry(grants[index]).ReloadAsync(token).ConfigureAwait(false);
                await evidence.RecordAsync(grants[index], PermissionOperationType.Grant, target, family, resourceId(grants[index]),
                    null, snapshot(grants[index]), items[index].Reason, token).ConfigureAwait(false);
            }
            return Result.Success(Applied(grants));
        }, cancellationToken, outcome => outcome.Applied).ConfigureAwait(false);
    }

    private async Task<Result<PermissionMutationBatchResult<TGrant>>> DeleteAsync<TGrant>(IReadOnlyList<PermissionGrantDeletion> requests,
        Func<Guid?, IQueryable<TGrant>> scopedRows, Func<TGrant, string> snapshot, string family,
        Func<TGrant, Guid?> resourceId, CancellationToken cancellationToken) where TGrant : PermissionBase
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count is < 1 or > 256) { return InvalidBatch<TGrant>(); }
        var items = requests.ToArray();
        if (items.Length is < 1 or > 256) { return InvalidBatch<TGrant>(); }
        var errors = new Dictionary<int, Error>();
        var scopes = new AuthorizedPermissionWriteScope[items.Length];
        var seen = new Dictionary<Guid, int>();
        for (var index = 0; index < items.Length; index++)
        {
            var request = items[index];
            if (request is null) { errors[index] = Invalid("A batch item cannot be null."); continue; }
            var scope = PermissionWriteAuthorization.Resolve(actors, request.TenantId, request.GlobalScope);
            if (scope.IsFailure) { errors[index] = scope.Error; continue; }
            scopes[index] = scope.Value;
            if (request.GrantId == Guid.Empty || request.ExpectedVersion is < 1 or int.MaxValue || request.Reason is { Length: > 500 })
            {
                errors[index] = Invalid("A grant id, positive non-exhausted version and bounded reason are required.");
                continue;
            }
            if (seen.TryGetValue(request.GrantId, out var first))
            {
                errors[first] = errors[index] = Invalid("The same grant appears more than once in the batch.");
            }
            else { seen.Add(request.GrantId, index); }
        }
        if (errors.Count > 0) { return Result.Success(Rejected<TGrant>(items.Length, errors)); }
        if (scopes.Any(scope => scope != scopes[0])) { return MixedScope<TGrant>(); }
        if (versions is null || audit is null) { return MissingCollaborators<TGrant>(); }
        var target = scopes[0];
        var evidence = new PermissionMutationEvidence(versions, audit);
        return await new PermissionMutationExecutor(context).ExecuteAsync(target.ActorId, target.TenantId, async token =>
        {
            var ids = items.Select(item => item.GrantId).ToArray();
            var found = await scopedRows(target.TenantId).Where(row => row.DeletedAt == null && ids.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, token).ConfigureAwait(false);
            for (var index = 0; index < items.Length; index++)
            {
                if (!found.TryGetValue(items[index].GrantId, out var grant))
                {
                    errors[index] = Error.NotFound("PermissionWrite.NotFound", "The grant was not found in the selected scope.");
                }
                else if (grant.Version != items[index].ExpectedVersion)
                {
                    errors[index] = Error.Conflict("PermissionWrite.Concurrency", "The permission changed; reload its current version.");
                }
            }
            if (errors.Count > 0) { return Result.Success(Rejected<TGrant>(items.Length, errors)); }
            var grants = items.Select(item => found[item.GrantId]).ToArray();
            var previous = grants.Select(snapshot).ToArray();
            foreach (var grant in grants) { grant.IsActive = false; grant.SoftDelete(); }
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await evidence.AdvanceVersionAsync(target, token).ConfigureAwait(false);
            for (var index = 0; index < grants.Length; index++)
            {
                await ((DbContext)context).Entry(grants[index]).ReloadAsync(token).ConfigureAwait(false);
                await evidence.RecordAsync(grants[index], PermissionOperationType.Revoke, target, family, resourceId(grants[index]),
                    previous[index], snapshot(grants[index]), items[index].Reason, token).ConfigureAwait(false);
            }
            return Result.Success(Applied(grants));
        }, cancellationToken, outcome => outcome.Applied).ConfigureAwait(false);
    }

    private static async Task<Result<TGrant>> SingleAsync<TRequest, TGrant>(TRequest request,
        Func<TRequest, Task<Result<PermissionMutationBatchResult<TGrant>>>> operation)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await operation(request).ConfigureAwait(false);
        return result.IsFailure ? Result.Failure<TGrant>(result.Error) : result.Value.Items[0].Result;
    }

    private static PermissionMutationBatchResult<TGrant> Applied<TGrant>(TGrant[] grants) =>
        new(true, grants.Select((grant, index) => new PermissionMutationItemResult<TGrant>(index, Result.Success(grant))).ToArray());

    private static PermissionMutationBatchResult<TGrant> Rejected<TGrant>(int count, IReadOnlyDictionary<int, Error> errors) =>
        new(false, Enumerable.Range(0, count).Select(index => new PermissionMutationItemResult<TGrant>(index,
            Result.Failure<TGrant>(errors.TryGetValue(index, out var error) ? error : Error.Conflict(
                "PermissionWrite.BatchNotApplied", "Another item failed; no changes in this atomic batch were applied.")))).ToArray());

    private static Result<PermissionMutationBatchResult<TGrant>> InvalidBatch<TGrant>() =>
        Result.Failure<PermissionMutationBatchResult<TGrant>>(Invalid("A batch must contain 1-256 items."));
    private static Result<PermissionMutationBatchResult<TGrant>> MixedScope<TGrant>() =>
        Result.Failure<PermissionMutationBatchResult<TGrant>>(Invalid("Every item must select the same authorized target scope and actor."));
    private static Result<PermissionMutationBatchResult<TGrant>> MissingCollaborators<TGrant>() =>
        Result.Failure<PermissionMutationBatchResult<TGrant>>(Error.Failure("PermissionWrite.CollaboratorsRequired", "Versioning and required audit services must be configured."));
    private static Error? ValidateStrings(IReadOnlyCollection<string> values) =>
        values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256) ? Invalid("Permission strings must contain 1-256 nonblank characters.") : null;
    private static Error? ValidateTyped(IReadOnlyCollection<PermissionType> values) =>
        values.Any(value => !Enum.IsDefined(value)) || string.Join(",", values.Distinct().Select(value => (int)value)).Length > 500
            ? Invalid("Typed permissions must be defined values fitting their 500-character encoding.") : null;
    private static Error? ValidateName(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 256 ? Invalid("Type bindings must contain 1-256 nonblank characters.") : null;
    private static Error Invalid(string description) => Error.Validation("PermissionWrite.InvalidRequest", description);
    private sealed record GrantBinding(Guid? UserId, string? Type, Guid? ResourceId);
}
