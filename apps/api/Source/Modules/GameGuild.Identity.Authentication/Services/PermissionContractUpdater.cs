using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>Updates the existing three-family grant model without changing its evaluation engine.</summary>
internal sealed class PermissionContractUpdater(
    IApplicationDbContext context, ITenantSecurityVersionStore? versions,
    IPermissionAuditService? audit, IActorContextAccessor? actors) : IPermissionUpdateService
{
    public Task<Result<TenantPermission>> UpdateTenantPermissionAsync(
        PermissionGrantUpdate<string> request, CancellationToken cancellationToken = default) =>
        UpdateAsync<TenantPermission, string>(request, tenant => context.Set<TenantPermission>().Where(row => row.TenantId == tenant),
            values => values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256)
                ? Invalid("Tenant permission strings must contain 1-256 nonblank characters.") : null,
            values => values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            (grant, values) => { grant.Permissions = values; grant.Reason = request.Reason; },
            grant => PermissionService.CapturePermissionSnapshot(grant), nameof(TenantPermission), _ => null, cancellationToken);

    public Task<Result<ContentTypePermission>> UpdateContentTypePermissionAsync(
        PermissionGrantUpdate<PermissionType> request, CancellationToken cancellationToken = default) =>
        UpdateAsync<ContentTypePermission, PermissionType>(request,
            tenant => context.Set<ContentTypePermission>().Where(row => row.TenantId == tenant),
            ValidateTyped, values => values.Distinct().ToArray(),
            (grant, values) => { grant.SetPermissions(values); grant.Notes = request.Reason; },
            grant => PermissionService.CapturePermissionSnapshot(grant), nameof(ContentTypePermission), _ => null, cancellationToken);

    public Task<Result<TPermission>> UpdateResourcePermissionAsync<TPermission, TResource>(
        PermissionGrantUpdate<PermissionType> request, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>
        where TResource : EntityBase =>
        UpdateAsync<TPermission, PermissionType>(request,
            tenant => context.Set<TPermission>().Where(row => row.TenantId == tenant),
            ValidateTyped, values => values.Distinct().ToArray(),
            (grant, values) => { grant.SetPermissions(values); grant.Notes = request.Reason; },
            grant => PermissionService.CapturePermissionSnapshot(grant, grant.ResourceType, grant.ResourceId),
            nameof(ResourcePermission<TResource>), grant => grant.ResourceId, cancellationToken);

    private async Task<Result<TPermission>> UpdateAsync<TPermission, TValue>(
        PermissionGrantUpdate<TValue> request, Func<Guid?, IQueryable<TPermission>> query,
        Func<TValue[], Error?> validate, Func<TValue[], TValue[]> normalize,
        Action<TPermission, TValue[]> replace, Func<TPermission, string> snapshot,
        string auditFamily, Func<TPermission, Guid?> resourceId, CancellationToken cancellationToken)
        where TPermission : PermissionBase
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        var scope = ResolveScope(request);
        if (scope.IsFailure)
        {
            return Result.Failure<TPermission>(scope.Error);
        }
        if (request.GrantId == Guid.Empty || request.ExpectedVersion < 1 || request.ExpectedVersion == int.MaxValue
            || request.Reason is { Length: > 500 } || request.Permissions is null
            || request.ExpiresAt is { Kind: not DateTimeKind.Utc }
            || request.IsActive && request.ExpiresAt is { } expiry && expiry <= SystemClock.UtcNow)
        {
            return Result.Failure<TPermission>(Invalid("A grant id, positive non-exhausted version, valid UTC lifetime and bounded reason are required."));
        }
        var values = request.Permissions.ToArray();
        var error = validate(values);
        if (error is not null)
        {
            return Result.Failure<TPermission>(error);
        }
        values = normalize(values);
        if (versions is null || audit is null)
        {
            return Result.Failure<TPermission>(Error.Failure("PermissionWrite.CollaboratorsRequired", "Versioning and required audit services must be configured."));
        }
        return await new PermissionMutationExecutor(context).ExecuteAsync<TPermission>(scope.Value.ActorId, scope.Value.TenantId, async token =>
        {
            var grant = await query(scope.Value.TenantId)
                .SingleOrDefaultAsync(row => row.Id == request.GrantId && row.DeletedAt == null, token).ConfigureAwait(false);
            if (grant is null)
            {
                return Result.Failure<TPermission>(Error.NotFound("PermissionWrite.NotFound", "The grant was not found in the selected scope."));
            }
            if (grant.Version != request.ExpectedVersion)
            {
                return Result.Failure<TPermission>(Error.Conflict("PermissionWrite.Concurrency", "The permission changed; reload its current version."));
            }
            var before = snapshot(grant);
            replace(grant, values);
            grant.IsActive = request.IsActive;
            grant.ExpiresAt = request.ExpiresAt;
            grant.Touch();
            // Every accepted update advances the entity version, even an otherwise identical replacement.
            ((DbContext)context).Entry(grant).Property(nameof(PermissionBase.UpdatedAt)).IsModified = true;
            ((DbContext)context).ChangeTracker.DetectChanges();
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            var version = await versions.IncrementVersionAsync((scope.Value.TenantId ?? Guid.Empty).ToString("D"), token).ConfigureAwait(false);
            if (version <= 0)
            {
                throw new InvalidOperationException("Permission invalidation did not advance the security version.");
            }
            var evidence = await audit.LogPermissionChangeAsync(PermissionOperationType.Update,
                grant.UserId, scope.Value.ActorId, scope.Value.TenantId,
                permissionType: "GrantState", resourceId: resourceId(grant), resourceType: auditFamily,
                oldValue: before, newValue: snapshot(grant), reason: request.Reason ?? "Update through permission service contract",
                cancellationToken: token).ConfigureAwait(false);
            if (evidence is null || evidence.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Permission mutation audit was not persisted.");
            }
            return Result.Success(grant);
        }, cancellationToken).ConfigureAwait(false);
    }

    private Result<WriteScope> ResolveScope<TValue>(PermissionGrantUpdate<TValue> request)
    {
        var actor = actors?.ActorContext;
        if (actor is null || !actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty)
        {
            return Result.Failure<WriteScope>(Error.Unauthorized("PermissionWrite.AuthenticationRequired", "An authenticated actor is required."));
        }
        if (request.TenantId == Guid.Empty || request.GlobalScope && request.TenantId.HasValue)
        {
            return Result.Failure<WriteScope>(Invalid("A tenant identifier cannot be empty or combined with global scope."));
        }
        if (request.GlobalScope)
        {
            return actor.IsSystemAdmin || actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults)
                ? Result.Success(new WriteScope(null, actorId))
                : Result.Failure<WriteScope>(Denied("Global mutations require system:manage-global-defaults."));
        }
        var tenant = request.TenantId ?? actor.TenantId;
        if (!tenant.HasValue || tenant == Guid.Empty)
        {
            return Result.Failure<WriteScope>(Invalid("An explicit tenant context is required."));
        }
        if (!actor.IsSystemAdmin && (tenant != actor.TenantId || !actor.IsTenantAdmin && !actor.HasPermission("permissions:manage")))
        {
            return Result.Failure<WriteScope>(Denied("Permission updates require administration of the selected tenant."));
        }
        return Result.Success(new WriteScope(tenant, actorId));
    }

    private static Error? ValidateTyped(PermissionType[] values)
    {
        if (values.Any(value => !Enum.IsDefined(value)))
        {
            return Invalid("Undefined permission values cannot be stored.");
        }
        return string.Join(",", values.Distinct().Select(value => (int)value)).Length > 500
            ? Invalid("The typed permission payload exceeds its 500-character storage contract.") : null;
    }
    private static Error Invalid(string description) => Error.Validation("PermissionWrite.InvalidUpdate", description);
    private static Error Denied(string description) => Error.Forbidden("PermissionWrite.Forbidden", description);
    private sealed record WriteScope(Guid? TenantId, Guid ActorId);
}
