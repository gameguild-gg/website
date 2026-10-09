using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;

using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

// ==================== GET TENANT PERMISSIONS ====================

/// <summary>
///     Query to get all tenant-level permissions for a user.
/// </summary>
public sealed record GetTenantPermissionsQuery : IQuery<GetTenantPermissionsResponse>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the user ID to check (defaults to current user if not specified).
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    ///     Gets whether to include effective permissions (from roles, groups, etc).
    /// </summary>
    public bool IncludeEffective { get; init; } = true;
}

/// <summary>
///     Response containing tenant permissions for a user.
/// </summary>
public sealed record GetTenantPermissionsResponse
{
    /// <summary>
    ///     Gets the user ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    ///     Gets the list of permissions.
    /// </summary>
    public required List<string> Permissions { get; init; }

    /// <summary>
    ///     Gets whether the user is a tenant admin.
    /// </summary>
    public bool IsTenantAdmin { get; init; }

    /// <summary>
    ///     Gets whether the user is a system admin.
    /// </summary>
    public bool IsSystemAdmin { get; init; }
}

/// <summary>
///     Handler for GetTenantPermissionsQuery.
/// </summary>
public sealed class GetTenantPermissionsQueryHandler(
    IPermissionQueryService queryService,
    IActorContextAccessor actorContextAccessor,
    ILogger<GetTenantPermissionsQueryHandler> logger)
    : IQueryHandler<GetTenantPermissionsQuery, GetTenantPermissionsResponse>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<GetTenantPermissionsResponse> Handle(GetTenantPermissionsQuery request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        var actorUserId = Actor.SubjectIdAsGuid;
        var targetUserId = request.UserId ?? actorUserId ??
            throw new UnauthorizedAccessException("User not authenticated");

        logger.LogInformation(
            "Getting tenant permissions for user {UserId} in tenant {TenantId}",
            targetUserId,
            request.TenantId);

        // Determine tenant id to operate on
        var tenantId = request.TenantId;

        if (tenantId.Value == Guid.Empty)
        {
            if (Actor.TenantId is { } ctxTenantId && ctxTenantId != Guid.Empty)
            {
                tenantId = ctxTenantId;
            }
            else
            {
                throw new InvalidOperationException("TenantId is required to retrieve tenant permissions");
            }
        }

        if (!Actor.IsSystemAdmin && Actor.TenantId != tenantId.Value)
        {
            throw new UnauthorizedAccessException("Tenant permission data is restricted to the actor's tenant");
        }

        if (targetUserId != actorUserId &&
            !Actor.IsSystemAdmin &&
            !Actor.IsTenantAdmin &&
            !Actor.HasPermission(UsersPermission.Keys.Read))
        {
            throw new UnauthorizedAccessException("Reading another user's tenant permissions requires user-read access");
        }

        // Get permissions based on request
        var permissions = request.IncludeEffective
            ? await queryService.GetEffectivePermissionsAsync(targetUserId, tenantId, cancellationToken).ConfigureAwait(false)
            : await queryService.GetTenantPermissionsAsync(targetUserId, tenantId, cancellationToken).ConfigureAwait(false);

        var isTenantAdmin = permissions.Contains("TenantAdmin") || permissions.Contains("Admin");
        var isSystemAdmin = permissions.Contains("SystemAdmin");

        logger.LogInformation(
            "Found {PermissionCount} permissions for user {UserId} in tenant {TenantId}",
            permissions.Count,
            targetUserId,
            tenantId);

        return new GetTenantPermissionsResponse
        {
            UserId = targetUserId,
            TenantId = tenantId,
            Permissions = permissions,
            IsTenantAdmin = isTenantAdmin,
            IsSystemAdmin = isSystemAdmin
        };
    }
}

// ==================== GET EFFECTIVE PERMISSIONS ====================

/// <summary>
///     Query to get all effective permissions for a user on a specific resource.
/// </summary>
public sealed record GetEffectivePermissionsQuery : IQuery<EffectivePermissionsResponse>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the type of resource.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the ID of the resource.
    /// </summary>
    public required Guid ResourceId { get; init; }

    /// <summary>
    ///     Gets the user ID to check permissions for (defaults to current user if not specified).
    /// </summary>
    public Guid? UserId { get; init; }
}

/// <summary>
///     Response containing all effective permissions for a user on a resource.
/// </summary>
public sealed record EffectivePermissionsResponse
{
    /// <summary>
    ///     Gets the user ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the resource type.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the resource ID.
    /// </summary>
    public required Guid ResourceId { get; init; }

    /// <summary>
    ///     Gets the list of effective permissions.
    /// </summary>
    public required List<EffectivePermissionDto> Permissions { get; init; }

    /// <summary>
    ///     Gets whether the user is the owner of the resource.
    /// </summary>
    public bool IsOwner { get; init; }

    /// <summary>
    ///     Gets whether the user has full access to the resource.
    /// </summary>
    public bool HasFullAccess { get; init; }
}

/// <summary>
///     Represents a single effective permission with its source.
/// </summary>
public sealed record EffectivePermissionDto
{
    /// <summary>
    ///     Gets the permission name.
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    ///     Gets the source of the permission (e.g., "Direct", "Role", "Group", "Tenant").
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    ///     Gets whether the permission has an expiration date.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Gets when the permission was granted.
    /// </summary>
    public DateTime? GrantedAt { get; init; }
}

/// <summary>
///     Handler for GetEffectivePermissionsQuery.
/// </summary>
public sealed class GetEffectivePermissionsQueryHandler(
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService queryService,
    ILogger<GetEffectivePermissionsQueryHandler> logger)
    : IQueryHandler<GetEffectivePermissionsQuery, EffectivePermissionsResponse>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<EffectivePermissionsResponse> Handle(GetEffectivePermissionsQuery request, CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? Actor.SubjectIdAsGuid ??
            throw new UnauthorizedAccessException("User not authenticated");

        logger.LogInformation(
            "Getting effective permissions for user {UserId} on resource {ResourceType}/{ResourceId}",
            targetUserId,
            request.ResourceType,
            request.ResourceId);

        // Check if current user can view permissions for this resource
        var resourcePermission = $"{request.ResourceType}.{request.ResourceId}.Read";
        var canView = Actor.TenantId.HasValue && await queryService.HasTenantPermissionAsync(
            Actor.SubjectIdAsGuid!.Value,
            Actor.TenantId.Value,
            resourcePermission,
            cancellationToken).ConfigureAwait(false);

        if (!canView && !Actor.IsSystemAdmin)
        {
            logger.LogWarning(
                "User {CurrentUserId} attempted to view permissions for resource {ResourceType}/{ResourceId} without Read permission",
                Actor.SubjectIdAsGuid,
                request.ResourceType,
                request.ResourceId);

            throw new UnauthorizedAccessException("You don't have permission to view permissions for this resource");
        }

        var permissions = await GetEffectivePermissionsInternal(
            targetUserId,
            request.TenantId,
            request.ResourceType,
            request.ResourceId,
            cancellationToken).ConfigureAwait(false);

        var isOwner = Actor.SubjectIdAsGuid == targetUserId;

        return new EffectivePermissionsResponse
        {
            UserId = targetUserId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Permissions = permissions,
            IsOwner = isOwner,
            HasFullAccess = isOwner || permissions.Any(p => p.Permission == "Admin" || p.Permission == "Owner")
        };
    }

    private async Task<List<EffectivePermissionDto>> GetEffectivePermissionsInternal(
        Guid userId,
        TenantId tenantId,
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var permissionKeys = await queryService
            .GetEffectivePermissionsAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        var resourcePrefix = $"{resourceType}.{resourceId}.";

        return permissionKeys
            .Where(permission => permission.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || permission.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                || permission.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(permission => new EffectivePermissionDto
            {
                Permission = permission,
                Source = "Effective"
            })
            .ToList();
    }
}

// ==================== HAS PERMISSION ====================

/// <summary>
///     Query to check if a user has a specific permission on a resource.
/// </summary>
public sealed record HasPermissionQuery : IQuery<HasPermissionResponse>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the type of resource.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the ID of the resource.
    /// </summary>
    public required Guid ResourceId { get; init; }

    /// <summary>
    ///     Gets the permission to check.
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    ///     Gets the user ID to check (defaults to current user if not specified).
    /// </summary>
    public Guid? UserId { get; init; }
}

/// <summary>
///     Response indicating whether the user has the requested permission.
/// </summary>
public sealed record HasPermissionResponse
{
    /// <summary>
    ///     Gets whether the permission is granted.
    /// </summary>
    public required bool HasPermission { get; init; }

    /// <summary>
    ///     Gets the user ID that was checked.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    ///     Gets the resource type.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the resource ID.
    /// </summary>
    public required Guid ResourceId { get; init; }

    /// <summary>
    ///     Gets the permission that was checked.
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    ///     Gets the reason if permission was denied.
    /// </summary>
    public string? DenialReason { get; init; }
}

/// <summary>
///     Handler for HasPermissionQuery.
/// </summary>
public sealed class HasPermissionQueryHandler(
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService queryService,
    ILogger<HasPermissionQueryHandler> logger)
    : IQueryHandler<HasPermissionQuery, HasPermissionResponse>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<HasPermissionResponse> Handle(HasPermissionQuery request, CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? Actor.SubjectIdAsGuid ??
            throw new UnauthorizedAccessException("User not authenticated");

        logger.LogInformation(
            "Checking if user {UserId} has permission '{Permission}' on resource {ResourceType}/{ResourceId}",
            targetUserId,
            request.Permission,
            request.ResourceType,
            request.ResourceId);

        // Use composite permission pattern for resource-level checks
        var resourcePermission = $"{request.ResourceType}.{request.ResourceId}.{request.Permission}";
        var hasPermission = Actor.TenantId.HasValue && await queryService.HasTenantPermissionAsync(
            targetUserId,
            Actor.TenantId.Value,
            resourcePermission,
            cancellationToken).ConfigureAwait(false);

        var denialReason = hasPermission ? null : "User does not have the required permission on this resource";

        logger.LogInformation(
            "Permission check result for user {UserId}: {HasPermission}",
            targetUserId,
            hasPermission);

        return new HasPermissionResponse
        {
            HasPermission = hasPermission,
            UserId = targetUserId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Permission = request.Permission,
            DenialReason = denialReason
        };
    }
}

// ==================== GET RESOURCE USERS ====================

/// <summary>
///     Query to get all users who have access to a specific resource.
/// </summary>
public sealed record GetResourceUsersQuery : IQuery<GetResourceUsersResponse>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the type of resource.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the ID of the resource.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    ///     Gets whether to include inherited permissions from groups/roles.
    /// </summary>
    public bool IncludeInherited { get; init; } = true;

    /// <summary>
    ///     Gets whether to include expired permissions in the results.
    /// </summary>
    public bool IncludeExpired { get; init; } = false;
}

/// <summary>
///     Response containing all users with access to a resource.
/// </summary>
public sealed record GetResourceUsersResponse
{
    /// <summary>
    ///     Gets the resource type.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    ///     Gets the resource ID.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    ///     Gets the list of users with access.
    /// </summary>
    public required List<ResourceUser> Users { get; init; }

    /// <summary>
    ///     Gets the total count of users.
    /// </summary>
    public int TotalCount => Users.Count;

    /// <summary>
    ///     Gets the count of users with owner access.
    /// </summary>
    public int OwnerCount => Users.Count(u => u.IsOwner);
}

/// <summary>
///     Handler for GetResourceUsersQuery.
/// </summary>
public sealed class GetResourceUsersQueryHandler(
    IResourcePermissionService resourcePermissionService,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService queryService,
    ILogger<GetResourceUsersQueryHandler> logger)
    : IQueryHandler<GetResourceUsersQuery, GetResourceUsersResponse>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<GetResourceUsersResponse> Handle(GetResourceUsersQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Getting users with access to resource {ResourceType}/{ResourceId}",
            request.ResourceType,
            request.ResourceId);

        // Check if current user has permission to view users for this resource
        var resourceIdGuid = Guid.Parse(request.ResourceId);
        var resourcePermission = $"{request.ResourceType}.{resourceIdGuid}.Read";
        var canView = Actor.TenantId.HasValue && await queryService.HasTenantPermissionAsync(
            Actor.SubjectIdAsGuid!.Value,
            Actor.TenantId.Value,
            resourcePermission,
            cancellationToken).ConfigureAwait(false);

        if (!canView && !Actor.IsSystemAdmin)
        {
            logger.LogWarning(
                "User {UserId} attempted to view users for resource {ResourceType}/{ResourceId} without permission",
                Actor.SubjectIdAsGuid,
                request.ResourceType,
                request.ResourceId);

            throw new UnauthorizedAccessException("You don't have permission to view users for this resource");
        }

        var response = await resourcePermissionService.GetResourceUsersAsync(
            request.TenantId,
            request.ResourceType,
            request.ResourceId,
            cancellationToken).ConfigureAwait(false);

        // Map to ResourceUser list
        var users = response.Users.Select(u => new ResourceUser
        {
            UserId = u.UserId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Permissions = u.Permissions,
            GrantedAt = u.GrantedAt,
            GrantedByUserId = u.UserId, // Note: This should ideally come from the service
            ExpiresAt = u.ExpiresAt,
            LastAccessedAt = null,
            IsActive = !u.ExpiresAt.HasValue || u.ExpiresAt.Value > SystemClock.UtcNow
        }).ToList();

        // Filter out expired permissions if requested
        if (!request.IncludeExpired)
        {
            users = users.Where(u => u.IsActive).ToList();
        }

        logger.LogInformation(
            "Found {UserCount} users with access to resource {ResourceType}/{ResourceId}",
            users.Count,
            request.ResourceType,
            request.ResourceId);

        return new GetResourceUsersResponse
        {
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Users = users
        };
    }
}

// ==================== GET EXPIRING TENANT PERMISSIONS (issue #331) ====================

/// <summary>
///     Query to get active tenant permission grants that expire within a cutoff
///     (defaults to the configured upcoming-expiration window).
/// </summary>
public sealed record GetExpiringTenantPermissionsQuery : IQuery<GetExpiringTenantPermissionsResponse>
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the optional cutoff instant; grants expiring after it are excluded.
    ///     Defaults to <c>now + Authorization:PermissionExpiration:UpcomingNotificationWindow</c>.
    /// </summary>
    public DateTime? ExpiresBefore { get; init; }
}

/// <summary>
///     A tenant permission grant that is about to expire.
/// </summary>
public sealed record ExpiringTenantPermission
{
    /// <summary>
    ///     Gets the grant ID.
    /// </summary>
    public required Guid PermissionId { get; init; }

    /// <summary>
    ///     Gets the user ID the grant belongs to (null for tenant defaults).
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    ///     Gets the grant's permission strings.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the expiration instant.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }
}

/// <summary>
///     Response containing grants that are about to expire in a tenant.
/// </summary>
public sealed record GetExpiringTenantPermissionsResponse
{
    /// <summary>
    ///     Gets the tenant ID.
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    ///     Gets the grants expiring within the requested window, ordered by expiration instant.
    /// </summary>
    public required List<ExpiringTenantPermission> Expiring { get; init; }
}

/// <summary>
///     Handler for GetExpiringTenantPermissionsQuery.
/// </summary>
public sealed class GetExpiringTenantPermissionsQueryHandler(
    IPermissionExpirationService expirationService,
    IActorContextAccessor actorContextAccessor,
    ILogger<GetExpiringTenantPermissionsQueryHandler> logger)
    : IQueryHandler<GetExpiringTenantPermissionsQuery, GetExpiringTenantPermissionsResponse>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<GetExpiringTenantPermissionsResponse> Handle(GetExpiringTenantPermissionsQuery request, CancellationToken cancellationToken)
    {
        PermissionExpirationCommandGuards.EnsureCanManageTenant(Actor, request.TenantId.Value, "view expiring permissions");

        logger.LogInformation(
            "Getting permissions expiring before {Cutoff} in tenant {TenantId}",
            request.ExpiresBefore,
            request.TenantId);

        var expiring = await expirationService
            .GetExpiringAsync(request.TenantId.Value, request.ExpiresBefore, cancellationToken)
            .ConfigureAwait(false);

        return new GetExpiringTenantPermissionsResponse
        {
            TenantId = request.TenantId.Value,
            Expiring = expiring
                .Select(p => new ExpiringTenantPermission
                {
                    PermissionId = p.Id,
                    UserId = p.UserId,
                    Permissions = p.Permissions.ToArray(),
                    ExpiresAt = p.ExpiresAt!.Value
                })
                .ToList()
        };
    }
}
