using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

// ============================================================================
// PERMISSION EVALUATION QUERY HANDLERS (issues #298/#307)
//
// The /v1/permissions evaluation surface (PermissionEvaluationController) dispatches
// these Authentication-module query contracts. Until now the records existed without
// handlers, so the endpoints failed at dispatch time. These handlers back them with the
// Authorization module's production services: the canonical effective-permission
// resolver for tenant/effective checks, the scoped content-type grant store (#846) for
// content-type layer reads, and the resource permission service for the resource layer.
//
// All handlers are read-only. Guards (fail-closed): the actor must be authenticated;
// a non-system actor is restricted to their own tenant; reading another user's
// permissions requires system administration, tenant administration or user-read.
// ============================================================================

/// <summary>
///     Checks a user's tenant-scoped permission through the canonical resolution path.
/// </summary>
public sealed class HasTenantPermissionQueryHandler(
    IPermissionQueryService queryService,
    IActorContextAccessor actorContextAccessor,
    ILogger<HasTenantPermissionQueryHandler> logger)
    : IQueryHandler<HasTenantPermissionQuery, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(HasTenantPermissionQuery request, CancellationToken cancellationToken)
    {
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, request.TenantId);

        // SECURITY: fail-closed on an invalid user/tenant context — deny instead of
        // falling through to any baseline.
        if (request.UserId == Guid.Empty || request.TenantId == Guid.Empty)
        {
            logger.LogWarning(
                "Tenant permission check requested with an invalid context (user {UserId}, tenant {TenantId}) - denying (fail-closed).",
                request.UserId, request.TenantId);
            return false;
        }

        return await queryService.HasTenantPermissionAsync(
            request.UserId,
            request.TenantId,
            request.Permission.ToString(),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Lists a user's content-type layer permissions from the scoped grant store (#846).
/// </summary>
public sealed class GetContentTypePermissionsQueryHandler(
    IContentTypePermissionGrantRepository contentTypeGrantRepository,
    IActorContextAccessor actorContextAccessor,
    ILogger<GetContentTypePermissionsQueryHandler> logger)
    : IQueryHandler<GetContentTypePermissionsQuery, IEnumerable<PermissionType>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<IEnumerable<PermissionType>> Handle(GetContentTypePermissionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = PermissionEvaluationQueryGuards.ResolveTenantScope(Actor, request.TenantId);
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, tenantId);

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return [];
        }

        var userGrant = await contentTypeGrantRepository
            .GetBySubjectAndContentTypeAsync(request.UserId, tenantId, request.ContentType, cancellationToken)
            .ConfigureAwait(false);
        var tenantGrant = await contentTypeGrantRepository
            .GetBySubjectAndContentTypeAsync(null, tenantId, request.ContentType, cancellationToken)
            .ConfigureAwait(false);

        // Fail-closed union (deny-by-default, #327): inactive or expired rows contribute
        // nothing; deny entries subtract from the tenant-default allow set.
        var allows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var denies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var grant in new[] { tenantGrant, userGrant })
        {
            if (grant is null || !grant.IsEffective())
            {
                continue;
            }

            foreach (var permission in grant.Permissions)
            {
                allows.Add(permission);
            }

            foreach (var denied in grant.DenyPermissions)
            {
                denies.Add(denied);
            }
        }

        allows.ExceptWith(denies);

        logger.LogInformation(
            "Listed {Count} in-force permissions for content type {ContentType} (user {UserId}, tenant {TenantId})",
            allows.Count, request.ContentType, request.UserId, tenantId);

        return PermissionEvaluationQueryGuards.ToPermissionTypes(allows);
    }
}

/// <summary>
///     Checks a user's content-type permission through the scoped grant store (#846):
///     an in-force user grant or tenant-default grant carries the permission unless a
///     deny entry subtracts it.
/// </summary>
public sealed class HasContentTypePermissionQueryHandler(
    IContentTypePermissionGrantRepository contentTypeGrantRepository,
    IActorContextAccessor actorContextAccessor,
    ILogger<HasContentTypePermissionQueryHandler> logger)
    : IQueryHandler<HasContentTypePermissionQuery, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(HasContentTypePermissionQuery request, CancellationToken cancellationToken)
    {
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, request.TenantId);

        if (request.UserId == Guid.Empty || request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.ContentType))
        {
            logger.LogWarning(
                "Content-type permission check requested with an invalid context - denying (fail-closed).");
            return false;
        }

        var permissionName = request.Permission.ToString();

        var userGrant = await contentTypeGrantRepository
            .GetBySubjectAndContentTypeAsync(request.UserId, request.TenantId, request.ContentType, cancellationToken)
            .ConfigureAwait(false);
        var tenantGrant = await contentTypeGrantRepository
            .GetBySubjectAndContentTypeAsync(null, request.TenantId, request.ContentType, cancellationToken)
            .ConfigureAwait(false);

        var allowed = false;
        foreach (var grant in new[] { userGrant, tenantGrant })
        {
            if (grant is null || !grant.IsEffective())
            {
                continue;
            }

            if (grant.DenyPermissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            allowed |= grant.Permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase);
        }

        return allowed;
    }
}

/// <summary>
///     Lists a user's resource-layer permissions for one resource. Reads the same
///     <see cref="GenericResourcePermission"/> store the /v1/permissions resource-grant
///     endpoints write, so the evaluation surface and the mutation surface agree.
/// </summary>
public sealed class GetResourcePermissionsQueryHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ILogger<GetResourcePermissionsQueryHandler> logger)
    : IQueryHandler<GetResourcePermissionsQuery, IEnumerable<PermissionType>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<IEnumerable<PermissionType>> Handle(GetResourcePermissionsQuery request, CancellationToken cancellationToken)
    {
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, request.TenantId);

        if (request.UserId == Guid.Empty || request.TenantId == Guid.Empty || request.ResourceId == Guid.Empty)
        {
            logger.LogWarning(
                "Resource permission listing requested with an invalid context - returning empty (fail-closed).");
            return [];
        }

        var grant = await PermissionEvaluationQueryGuards.LoadGrantAsync(dbContext, request.UserId, request.TenantId, request.ResourceId, cancellationToken).ConfigureAwait(false);

        return grant is null ? [] : grant.GetPermissionsAsEnum();
    }
}

/// <summary>
///     Checks a user's resource-scoped permission against the same
///     <see cref="GenericResourcePermission"/> store the /v1/permissions resource-grant
///     endpoints write (inactive, expired or soft-deleted grants deny, fail-closed).
/// </summary>
public sealed class HasResourcePermissionQueryHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ILogger<HasResourcePermissionQueryHandler> logger)
    : IQueryHandler<HasResourcePermissionQuery, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(HasResourcePermissionQuery request, CancellationToken cancellationToken)
    {
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, request.TenantId);

        if (request.UserId == Guid.Empty || request.TenantId == Guid.Empty || request.ResourceId == Guid.Empty)
        {
            logger.LogWarning(
                "Resource permission check requested with an invalid context - denying (fail-closed).");
            return false;
        }

        var grant = await PermissionEvaluationQueryGuards.LoadGrantAsync(dbContext, request.UserId, request.TenantId, request.ResourceId, cancellationToken).ConfigureAwait(false);

        return grant is not null
            && grant.IsActive
            && (!grant.ExpiresAt.HasValue || grant.ExpiresAt.Value > SystemClock.UtcNow)
            && grant.HasPermission(request.Permission);
    }
}

/// <summary>
///     Aggregated view of one user's permissions across the three layers.
/// </summary>
public sealed class GetUserPermissionsQueryHandler(
    IPermissionQueryService queryService,
    IContentTypePermissionGrantRepository contentTypeGrantRepository,
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    ILogger<GetUserPermissionsQueryHandler> logger)
    : IQueryHandler<GetUserPermissionsQuery, UserPermissionsDto>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<UserPermissionsDto> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = PermissionEvaluationQueryGuards.ResolveTenantScope(Actor, request.TenantId);
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, tenantId);

        var tenantPermissions = await queryService
            .GetTenantPermissionsAsync(request.UserId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        var contentTypePermissions = new Dictionary<string, List<PermissionType>>();
        var contentTypeGrants = await contentTypeGrantRepository
            .GetByUserAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var grant in contentTypeGrants)
        {
            // Cross-tenant rows must not leak into this tenant's aggregate view.
            if (grant.TenantId is { } grantTenant && grantTenant != tenantId)
            {
                continue;
            }

            if (!grant.IsEffective())
            {
                continue;
            }

            contentTypePermissions[grant.ContentTypeName] = PermissionEvaluationQueryGuards
                .ToPermissionTypes(grant.Permissions.Where(p => !grant.DenyPermissions.Contains(p, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

        var resourcePermissions = new Dictionary<Guid, List<PermissionType>>();
        var resourceGrants = await resourcePermissionService
            .GetUserResourcesAsync(new TenantId(tenantId), request.UserId, resourceType: null, cancellationToken)
            .ConfigureAwait(false);
        foreach (var grant in resourceGrants)
        {
            if (grant.ExpiresAt.HasValue && grant.ExpiresAt.Value <= SystemClock.UtcNow)
            {
                continue;
            }

            var resourceId = Guid.TryParse(grant.ResourceId, out var parsed) ? parsed : Guid.Empty;
            if (resourceId == Guid.Empty)
            {
                continue;
            }

            if (resourcePermissions.TryGetValue(resourceId, out var existing))
            {
                existing.AddRange(PermissionEvaluationQueryGuards.ToPermissionTypes(grant.Permissions));
            }
            else
            {
                resourcePermissions[resourceId] = PermissionEvaluationQueryGuards.ToPermissionTypes(grant.Permissions).ToList();
            }
        }

        logger.LogInformation(
            "Aggregated permission view for user {UserId} in tenant {TenantId}: {TenantCount} tenant, {ContentTypeCount} content-type, {ResourceCount} resource permissions",
            request.UserId, tenantId, tenantPermissions.Count, contentTypePermissions.Count, resourcePermissions.Count);

        return new ConcreteUserPermissionsDto
        {
            UserId = request.UserId,
            TenantId = tenantId,
            TenantPermissions = PermissionEvaluationQueryGuards.ToPermissionTypes(tenantPermissions).ToList(),
            ContentTypePermissions = contentTypePermissions,
            ResourcePermissions = resourcePermissions,
            LastUpdated = SystemClock.UtcNow
        };
    }

    private sealed class ConcreteUserPermissionsDto : UserPermissionsDto;
}

/// <summary>
///     Effective permissions for a user (optionally narrowed to a resource or content
///     type) resolved through the canonical DENY-WINS engine.
/// </summary>
public sealed class GetEffectivePermissionsQueryHandler(
    IPermissionQueryService queryService,
    IActorContextAccessor actorContextAccessor,
    ILogger<GetEffectivePermissionsQueryHandler> logger)
    : IQueryHandler<GetEffectivePermissionsQuery, EffectivePermissionsDto>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<EffectivePermissionsDto> Handle(GetEffectivePermissionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = PermissionEvaluationQueryGuards.ResolveTenantScope(Actor, request.TenantId);
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, tenantId);

        var effective = await queryService
            .GetEffectivePermissionsAsync(request.UserId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        // Narrow by content type / resource qualified prefixes when requested so the
        // reported set matches the scope the caller asked about.
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            var prefix = $"{request.ContentType}.";
            effective = effective.Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (request.ResourceId.HasValue)
        {
            var prefix = $".{request.ResourceId.Value}.";
            effective = effective.Where(p => p.Contains(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var permissions = PermissionEvaluationQueryGuards.ToPermissionTypes(effective).ToList();

        logger.LogInformation(
            "Resolved {Count} effective permissions for user {UserId} in tenant {TenantId}",
            permissions.Count, request.UserId, tenantId);

        return new ConcreteEffectivePermissionsDto
        {
            UserId = request.UserId,
            TenantId = tenantId,
            ResourceId = request.ResourceId,
            ContentType = request.ContentType,
            EffectivePermissions = permissions,
            CalculatedAt = SystemClock.UtcNow
        };
    }

    private sealed class ConcreteEffectivePermissionsDto : EffectivePermissionsDto;
}

/// <summary>
///     Layer-by-layer explanation of one permission check (hierarchy resolution).
/// </summary>
public sealed class ResolvePermissionHierarchyQueryHandler(
    IPermissionQueryService queryService,
    IContentTypePermissionGrantRepository contentTypeGrantRepository,
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ResolvePermissionHierarchyQueryHandler> logger)
    : IQueryHandler<ResolvePermissionHierarchyQuery, PermissionHierarchyResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionHierarchyResult> Handle(ResolvePermissionHierarchyQuery request, CancellationToken cancellationToken)
    {
        PermissionEvaluationQueryGuards.EnsureCanReadPermissions(Actor, request.UserId, request.TenantId);

        var result = new ConcretePermissionHierarchyResult
        {
            RequestedPermission = request.Permission
        };

        var permissionName = request.Permission.ToString();

        // Layer 1: tenant (direct grants, defaults, roles via the effective resolver).
        var tenantPermissions = await queryService
            .GetTenantPermissionsAsync(request.UserId, request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var tenantFound = tenantPermissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase);
        result.ResolutionSteps.Add(new ConcretePermissionResolutionStep
        {
            Level = "Tenant",
            Found = tenantFound,
            Source = "TenantPermission",
            ExpiresAt = null
        });

        // Layer 2: content-type scoped grants (#846).
        var contentTypeFound = false;
        string? contentTypeSource = null;
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            var userGrant = await contentTypeGrantRepository
                .GetBySubjectAndContentTypeAsync(request.UserId, request.TenantId, request.ContentType, cancellationToken)
                .ConfigureAwait(false);
            var tenantGrant = await contentTypeGrantRepository
                .GetBySubjectAndContentTypeAsync(null, request.TenantId, request.ContentType, cancellationToken)
                .ConfigureAwait(false);

            foreach (var grant in new[] { userGrant, tenantGrant })
            {
                if (grant is null || !grant.IsEffective())
                {
                    continue;
                }

                if (grant.DenyPermissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase))
                {
                    contentTypeFound = false;
                    contentTypeSource = "ContentTypeDeny";
                    break;
                }

                if (grant.Permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase))
                {
                    contentTypeFound = true;
                    contentTypeSource = userGrant is not null && grant == userGrant ? "ContentTypeGrant" : "ContentTypeDefault";
                }
            }
        }

        result.ResolutionSteps.Add(new ConcretePermissionResolutionStep
        {
            Level = "ContentType",
            Found = contentTypeFound,
            Source = contentTypeSource ?? "ContentTypeGrant",
            ExpiresAt = null
        });

        // Layer 3: resource grants (exact match).
        var resourceFound = false;
        DateTime? resourceExpiry = null;
        if (request.ResourceId.HasValue)
        {
            var grants = await resourcePermissionService
                .GetUserResourcesAsync(new TenantId(request.TenantId), request.UserId, resourceType: null, cancellationToken)
                .ConfigureAwait(false);

            foreach (var grant in grants)
            {
                if (!string.Equals(grant.ResourceId, request.ResourceId.Value.ToString(), StringComparison.Ordinal))
                {
                    continue;
                }

                if (grant.ExpiresAt.HasValue && grant.ExpiresAt.Value <= SystemClock.UtcNow)
                {
                    continue;
                }

                if (grant.Permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase))
                {
                    resourceFound = true;
                    resourceExpiry = grant.ExpiresAt;
                }
            }
        }

        result.ResolutionSteps.Add(new ConcretePermissionResolutionStep
        {
            Level = "Resource",
            Found = resourceFound,
            Source = "ResourceGrant",
            ExpiresAt = resourceExpiry
        });

        // Final: the effective resolution is authoritative (DENY-WINS across all layers).
        var effective = await queryService
            .GetEffectivePermissionsAsync(request.UserId, request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        result.HasPermission = effective.Contains(permissionName, StringComparer.OrdinalIgnoreCase);
        result.FinalSource = result.HasPermission
            ? (resourceFound ? "Resource" : contentTypeFound ? "ContentType" : tenantFound ? "Tenant" : "Effective")
            : "None";

        logger.LogInformation(
            "Resolved permission hierarchy for user {UserId}, permission {Permission}: {HasPermission} via {FinalSource}",
            request.UserId, permissionName, result.HasPermission, result.FinalSource);

        return result;
    }

    private sealed class ConcretePermissionHierarchyResult : PermissionHierarchyResult;
    private sealed class ConcretePermissionResolutionStep : PermissionResolutionStep;
}

/// <summary>
///     Shared fail-closed guards for the permission evaluation queries.
/// </summary>
internal static class PermissionEvaluationQueryGuards
{
    public static void EnsureCanReadPermissions(ActorContext actor, Guid targetUserId, Guid tenantId)
    {
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required");
        }

        if (!actor.IsSystemAdmin && actor.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException("Tenant permission data is restricted to the actor's tenant");
        }

        var actorUserId = actor.SubjectIdAsGuid;
        if (targetUserId != actorUserId
            && !actor.IsSystemAdmin
            && !actor.IsTenantAdmin
            && !actor.HasPermission(UsersPermission.Keys.Read))
        {
            throw new UnauthorizedAccessException("Reading another user's permissions requires user-read access");
        }
    }

    /// <summary>
    ///     Resolves the effective tenant scope: an omitted tenant falls back to the
    ///     actor's own tenant (fail-closed when neither is known).
    /// </summary>
    public static Guid ResolveTenantScope(ActorContext actor, Guid? requestedTenantId)
    {
        if (requestedTenantId is { } tenant && tenant != Guid.Empty)
        {
            return tenant;
        }

        if (actor.TenantId is { } actorTenant && actorTenant != Guid.Empty)
        {
            return actorTenant;
        }

        throw new InvalidOperationException("TenantId is required to read permissions");
    }

    /// <summary>
    ///     Loads the live <see cref="GenericResourcePermission"/> row for one user,
    ///     tenant and resource (soft-deleted rows never match). Shared by the resource
    ///     list/check handlers so both read the exact store the grant endpoints write.
    /// </summary>
    public static Task<GenericResourcePermission?> LoadGrantAsync(
        IApplicationDbContext dbContext,
        Guid userId,
        Guid tenantId,
        Guid resourceId,
        CancellationToken cancellationToken)
        => dbContext.Set<GenericResourcePermission>()
            .FirstOrDefaultAsync(p =>
                    p.UserId == userId &&
                    p.TenantId == (tenantId == Guid.Empty ? null : tenantId) &&
                    p.ResourceId == resourceId &&
                    p.DeletedAt == null,
                cancellationToken);

    /// <summary>
    ///     Fail-closed mapping of permission names to the enum catalogue: values that do
    ///     not correspond to a defined permission type are dropped, never guessed.
    /// </summary>
    public static IEnumerable<PermissionType> ToPermissionTypes(IEnumerable<string> permissionNames)
    {
        foreach (var name in permissionNames)
        {
            if (Enum.TryParse<PermissionType>(name, ignoreCase: true, out var permission)
                && Enum.IsDefined(permission))
            {
                yield return permission;
            }
        }
    }
}
