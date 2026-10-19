using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>Database-backed read implementation for the additive permission contract.</summary>
internal sealed class PermissionContractReader(IApplicationDbContext context, IActorContextAccessor? actors)
    : IPermissionReadService
{
    public async Task<Result<PagedResult<TenantPermission>>> QueryTenantPermissionsAsync(
        TenantPermissionListQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        var scope = ResolveScope(query, history: false);
        var validation = ValidateGrantWindow(query);
        if (scope.IsFailure || validation is not null)
        {
            return Result.Failure<PagedResult<TenantPermission>>(scope.IsFailure ? scope.Error : validation!);
        }

        var rows = context.Set<TenantPermission>().AsNoTracking().Where(row => row.TenantId == scope.Value.TenantId);
        return await ReadGrantPageAsync(rows, query, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PagedResult<ContentTypePermission>>> QueryContentTypePermissionsAsync(
        ContentTypePermissionListQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        var scope = ResolveScope(query, history: false);
        var validation = ValidateGrantWindow(query) ?? ValidateName(query.ContentTypeName);
        if (scope.IsFailure || validation is not null)
        {
            return Result.Failure<PagedResult<ContentTypePermission>>(scope.IsFailure ? scope.Error : validation!);
        }

        var tenant = scope.Value.TenantId;
        var rows = context.Set<ContentTypePermission>().AsNoTracking().Where(row => row.TenantId == tenant);
        if (query.ContentTypeName is { } name) { rows = rows.Where(row => row.ContentTypeName == name); }
        return await ReadGrantPageAsync(rows, query, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PagedResult<TPermission>>> QueryResourcePermissionsAsync<TPermission, TResource>(
        ResourcePermissionListQuery query, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>
        where TResource : EntityBase
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        var scope = ResolveScope(query, history: false);
        var validation = ValidateGrantWindow(query) ?? ValidateName(query.ResourceType);
        if (query.ResourceId == Guid.Empty) { validation = Invalid("A resource identifier cannot be empty."); }
        if (scope.IsFailure || validation is not null)
        {
            return Result.Failure<PagedResult<TPermission>>(scope.IsFailure ? scope.Error : validation!);
        }

        var tenant = scope.Value.TenantId;
        var rows = context.Set<TPermission>().AsNoTracking().Where(row => row.TenantId == tenant);
        if (query.ResourceId is { } resourceId) { rows = rows.Where(row => row.ResourceId == resourceId); }
        if (query.ResourceType is { } type) { rows = rows.Where(row => row.ResourceType == type); }
        return await ReadGrantPageAsync(rows, query, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PagedResult<PermissionAuditLog>>> QueryPermissionHistoryAsync(
        PermissionAuditHistoryQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        var scope = ResolveScope(query, history: true);
        var validation = ValidateWindow(query.FromUtc, query.ToUtc) ?? ValidateName(query.ResourceType) ?? ValidateName(query.PermissionType);
        if (query.ResourceId == Guid.Empty) { validation = Invalid("A resource identifier cannot be empty."); }
        if (query.Operation is { } operation && !Enum.IsDefined(operation)) { validation = Invalid("Unknown permission operation."); }
        if (scope.IsFailure || validation is not null)
        {
            return Result.Failure<PagedResult<PermissionAuditLog>>(scope.IsFailure ? scope.Error : validation!);
        }

        var tenant = ToTenantKey(scope.Value.TenantId);
        var rows = context.Set<PermissionAuditLog>().AsNoTracking().Where(row => row.TenantId == tenant);
        if (!scope.Value.AllUsers)
        {
            rows = query.IncludeDefaultSubjects
                ? rows.Where(row => row.UserId == scope.Value.UserId || row.UserId == null)
                : rows.Where(row => row.UserId == scope.Value.UserId);
        }
        else if (!query.IncludeDefaultSubjects) { rows = rows.Where(row => row.UserId != null); }

        if (query.FromUtc is { } from) { rows = rows.Where(row => row.Timestamp >= from); }
        if (query.ToUtc is { } to) { rows = rows.Where(row => row.Timestamp <= to); }
        if (query.Operation is { } kind) { rows = rows.Where(row => row.OperationType == kind); }
        if (query.ResourceId is { } resourceId) { rows = rows.Where(row => row.ResourceId == resourceId); }
        if (query.ResourceType is { } type) { rows = rows.Where(row => row.ResourceType == type); }
        if (query.PermissionType is { } permission) { rows = rows.Where(row => row.PermissionType == permission); }
        if (query.Success is { } success) { rows = rows.Where(row => row.Success == success); }
        var count = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var offset = (query.Page - 1) * query.PageSize;
        var items = await rows.OrderByDescending(row => row.Timestamp).ThenBy(row => row.Id)
            .Skip(offset).Take(query.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResult<PermissionAuditLog>(items, count, offset, query.PageSize));
    }

    private Result<ReadScope> ResolveScope(PermissionReadQuery query, bool history)
    {
        var actor = actors?.ActorContext;
        if (actor is null || !actor.IsAuthenticated)
        {
            return Result.Failure<ReadScope>(Error.Unauthorized("PermissionRead.AuthenticationRequired", "Authentication is required."));
        }
        if (query.Page < 1 || query.PageSize is < 1 or > 200 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
        {
            return Result.Failure<ReadScope>(Invalid("Page must be positive, page size must be 1-200, and the offset must fit Int32."));
        }
        if (query.TenantId == Guid.Empty || query.UserId == Guid.Empty || query.AllUsers && query.UserId.HasValue)
        {
            return Result.Failure<ReadScope>(Invalid("Identifiers cannot be empty and AllUsers cannot select a specific UserId."));
        }
        if (query.GlobalScope && query.TenantId.HasValue)
        {
            return Result.Failure<ReadScope>(Invalid("GlobalScope cannot select a tenant."));
        }
        if (query.GlobalScope && !actor.IsSystemAdmin)
        {
            return Result.Failure<ReadScope>(Denied("Global permission records require system administration."));
        }

        var tenantId = query.GlobalScope ? null : query.TenantId ?? actor.TenantId;
        if (!query.GlobalScope && (!tenantId.HasValue || tenantId == Guid.Empty))
        {
            return Result.Failure<ReadScope>(Invalid("A tenant context is required."));
        }
        if (!actor.IsSystemAdmin && tenantId != actor.TenantId)
        {
            return Result.Failure<ReadScope>(Denied("Permission records belong to the actor's tenant."));
        }

        var userId = query.UserId ?? actor.SubjectIdAsGuid;
        var canReadOthers = actor.IsSystemAdmin || !history && (actor.IsTenantAdmin || actor.HasPermission("permissions:manage"));
        if (!query.AllUsers && (!userId.HasValue || userId == Guid.Empty))
        {
            return Result.Failure<ReadScope>(Error.Unauthorized("PermissionRead.SubjectRequired", "An authenticated subject is required."));
        }
        if (!canReadOthers && (query.AllUsers || query.IncludeDefaultSubjects || userId != actor.SubjectIdAsGuid))
        {
            return Result.Failure<ReadScope>(Denied("Reading other subjects or default records requires administration of this scope."));
        }
        return Result.Success(new ReadScope(tenantId, userId, query.AllUsers));
    }

    private static async Task<Result<PagedResult<TPermission>>> ReadGrantPageAsync<TPermission>(
        IQueryable<TPermission> rows, PermissionGrantListQuery query, ReadScope scope, CancellationToken cancellationToken)
        where TPermission : PermissionBase
    {
        rows = rows.Where(row => row.DeletedAt == null);
        if (!scope.AllUsers)
        {
            rows = query.IncludeDefaultSubjects
                ? rows.Where(row => row.UserId == scope.UserId || row.UserId == null)
                : rows.Where(row => row.UserId == scope.UserId);
        }
        else if (!query.IncludeDefaultSubjects) { rows = rows.Where(row => row.UserId != null); }
        if (!query.IncludeInactive)
        {
            var now = SystemClock.UtcNow;
            rows = rows.Where(row => row.IsActive && (!row.ExpiresAt.HasValue || row.ExpiresAt > now));
        }
        if (query.GrantedFromUtc is { } from) { rows = rows.Where(row => row.GrantedAt >= from); }
        if (query.GrantedToUtc is { } to) { rows = rows.Where(row => row.GrantedAt <= to); }
        var count = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var offset = (query.Page - 1) * query.PageSize;
        var items = await rows.OrderByDescending(row => row.GrantedAt).ThenBy(row => row.Id)
            .Skip(offset).Take(query.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResult<TPermission>(items, count, offset, query.PageSize));
    }

    private static Error? ValidateGrantWindow(PermissionGrantListQuery query) => ValidateWindow(query.GrantedFromUtc, query.GrantedToUtc);

    private static Error? ValidateWindow(DateTime? from, DateTime? to) =>
        from is { Kind: not DateTimeKind.Utc } || to is { Kind: not DateTimeKind.Utc } || from > to
            ? Invalid("Time bounds must be UTC and the lower bound cannot exceed the upper bound.") : null;

    private static Error? ValidateName(string? value) =>
        value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > 256)
            ? Invalid("A supplied type label must contain 1-256 nonblank characters.") : null;

    private static TenantId? ToTenantKey(Guid? tenantId) => tenantId is { } value ? new TenantId(value) : null;
    private static Error Invalid(string description) => Error.Validation("PermissionRead.InvalidQuery", description);
    private static Error Denied(string description) => Error.Forbidden("PermissionRead.Forbidden", description);
    private sealed record ReadScope(Guid? TenantId, Guid? UserId, bool AllUsers);
}
