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
        var scope = PermissionWriteAuthorization.Resolve(actors, request.TenantId, request.GlobalScope);
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
        var evidence = new PermissionMutationEvidence(versions, audit);
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
            await ((DbContext)context).Entry(grant).ReloadAsync(token).ConfigureAwait(false);
            await evidence.AdvanceVersionAsync(scope.Value, token).ConfigureAwait(false);
            await evidence.RecordAsync(grant, PermissionOperationType.Update, scope.Value, auditFamily, resourceId(grant),
                before, snapshot(grant), request.Reason, token).ConfigureAwait(false);
            return Result.Success(grant);
        }, cancellationToken).ConfigureAwait(false);
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
}
