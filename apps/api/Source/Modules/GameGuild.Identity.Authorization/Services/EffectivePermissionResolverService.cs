using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Canonical implementation of the effective-permission resolution contract (issue #330).
///     Contract: <c>apps/api/docs/effective-permission-resolution.md</c>.
/// </summary>
/// <remarks>
///     <para>
///         Combines every applicable layer with <b>DENY-WINS</b> precedence:
///         <c>Effective = (Union of allows) - (Union of denies)</c>. Permissions that no
///         layer grants are denied (deny-by-default, #327). The system-account wildcard is
///         the only non-deniable grant.
///     </para>
///     <para>
///         The resolver is deterministic and cache-free. Layer order fixes source
///         attribution; grants from unrelated users, tenants or resources never contribute.
///     </para>
/// </remarks>
public sealed class EffectivePermissionResolverService(
    ITenantPermissionRepository tenantPermissionRepository,
    IRbacPermissionResolver rbacResolver,
    IEnumerable<IAuthorizationRolePermissionProvider> rolePermissionProviders,
    IResourcePermissionService resourcePermissionService,
    IOptions<AuthorizationOptions> authorizationOptions,
    ILogger<EffectivePermissionResolverService> logger,
    IJitElevationRequestRepository? jitElevationRepository = null
) : IEffectivePermissionResolver
{
    private readonly AuthorizationOptions _authOptions = authorizationOptions.Value;

    public async Task<EffectivePermissions> ResolveAsync(
        EffectivePermissionContext context,
        CancellationToken ct = default)
    {
        if (!context.IsValid)
        {
            logger.LogWarning(
                "Effective permission resolution requested with an invalid context (user {UserId}, tenant {TenantId}, resource {ResourceType}/{ResourceId}) - returning empty permissions (fail-closed).",
                context.UserId, context.TenantId, context.ResourceType ?? "<none>", context.ResourceId ?? "<none>");
            return FailClosed(context);
        }

        var allows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var denies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase);
        var roleContributions = new List<RoleContribution>();

        void AddAllow(string permission, PermissionSource source)
        {
            if (allows.Add(permission))
            {
                sources[permission] = source;
            }
        }

        // Layer 1: Static system-account wildcard (non-deniable). There are no hard-coded
        // global defaults: global baseline permissions are the data-driven row below.
        if (context.UserId == _authOptions.SystemAccountId)
        {
            AddAllow("*", PermissionSource.Static);
        }

        // Layer 2: Dynamic RBAC roles (hierarchy-aware). Direct role contributions are
        // attributed to Role, hierarchy-inherited ones to RoleInheritance; first writer
        // wins, so a direct assignment always outranks an inherited grant for attribution.
        var rbacResult = await rbacResolver.ResolvePermissionsAsync(context.UserId, context.TenantId, ct).ConfigureAwait(false);
        foreach (var contribution in rbacResult.RoleContributions)
        {
            var contributionSource = contribution.IsInherited
                ? PermissionSource.RoleInheritance
                : PermissionSource.Role;
            foreach (var permission in contribution.Permissions)
            {
                AddAllow(permission, contributionSource);
            }
        }

        // The aggregate may contain permissions beyond the itemized contributions; those get plain Role attribution.
        foreach (var permission in rbacResult.Permissions)
        {
            AddAllow(permission, PermissionSource.Role);
        }

        denies.UnionWith(rbacResult.DenyPermissions);
        roleContributions.AddRange(rbacResult.RoleContributions);

        // Layer 3: Role permission providers (the authorization entry-point role path).
        // Universal wildcards (admin:*) are not delegable to effective resolution.
        foreach (var provider in rolePermissionProviders)
        {
            var permissions = await provider
                .GetPermissionsAsync(context.UserId, context.TenantId, ct)
                .ConfigureAwait(false) ?? [];
            foreach (var permission in permissions.Where(IsDelegableRolePermission))
            {
                AddAllow(permission, PermissionSource.Role);
            }
        }

        // Layer 4: Global defaults (UserId=null, TenantId=null) — data-driven, never hard-coded.
        await AddTenantPermissionLayerAsync(null, null, PermissionSource.GlobalDefault, allows, denies, sources, ct).ConfigureAwait(false);

        // Layer 5: Tenant defaults (UserId=null, TenantId=current tenant).
        await AddTenantPermissionLayerAsync(null, context.TenantId, PermissionSource.TenantDefault, allows, denies, sources, ct).ConfigureAwait(false);

        // Layer 6: Direct user grants (UserId=current user, TenantId=current tenant).
        await AddTenantPermissionLayerAsync(context.UserId, context.TenantId, PermissionSource.DirectGrant, allows, denies, sources, ct).ConfigureAwait(false);

        // Layer 7: Just-in-Time elevation grants (issue #341, tenant-scoped,
        // resource-unscoped). An approved elevation inside its time window temporarily
        // contributes its permission to the allow set. JIT grants remain subject to
        // DENY-WINS below, and "admin:*" is never grantable through elevation.
        if (jitElevationRepository is not null)
        {
            var elevations = await jitElevationRepository
                .GetActiveByUserAsync(context.UserId, context.TenantId, ct)
                .ConfigureAwait(false) ?? [];

            foreach (var elevation in elevations)
            {
                if (elevation.ResourceId is null
                    && elevation.IsGrantInForce()
                    && IsDelegableRolePermission(elevation.Permission))
                {
                    AddAllow(elevation.Permission, PermissionSource.TemporaryElevation);
                }
            }
        }

        // Layer 8: Resource grants — only when the context names this exact resource.
        // Resource grants never contribute to tenant-wide results (context isolation).
        if (context.HasResource)
        {
            var resourceGrants = await resourcePermissionService
                .GetUserResourcesAsync(new TenantId(context.TenantId), context.UserId, context.ResourceType, ct)
                .ConfigureAwait(false);
            foreach (var grant in resourceGrants)
            {
                if (!string.Equals(grant.ResourceId, context.ResourceId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (grant.ExpiresAt.HasValue && grant.ExpiresAt.Value <= SystemClock.UtcNow)
                {
                    continue;
                }

                foreach (var permission in grant.Permissions)
                {
                    AddAllow(permission, PermissionSource.ResourceGrant);
                }
            }
        }

        // DENY-WINS: subtract every layer's explicit denies. Static (system-account)
        // permissions are the only grants that survive an explicit deny.
        var effectivePermissions = new HashSet<string>(allows, StringComparer.OrdinalIgnoreCase);
        foreach (var denied in denies)
        {
            if (sources.TryGetValue(denied, out var source) && source == PermissionSource.Static)
            {
                logger.LogWarning(
                    "Attempted to deny static permission {Permission} for user {UserId} - denies cannot override static grants",
                    denied, context.UserId);
                continue;
            }

            if (effectivePermissions.Remove(denied))
            {
                sources.Remove(denied);
            }
        }

        logger.LogDebug(
            "Resolved {Count} effective permissions ({AllowCount} allowed, {DenyCount} denied) for user {UserId} in tenant {TenantId}",
            effectivePermissions.Count, allows.Count, denies.Count, context.UserId, context.TenantId);

        return new EffectivePermissions
        {
            UserId = context.UserId,
            TenantId = context.TenantId,
            Permissions = effectivePermissions,
            Sources = sources,
            RoleContributions = roleContributions,
            Context = context,
            ContextValid = true
        };
    }

    public Task<EffectivePermissions> ResolveAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default)
        => ResolveAsync(new EffectivePermissionContext { UserId = userId, TenantId = tenantId ?? Guid.Empty }, ct);

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        Guid? tenantId,
        string permission,
        CancellationToken ct = default)
    {
        var effective = await ResolveAsync(userId, tenantId, ct).ConfigureAwait(false);
        return effective.Permissions.Contains(permission);
    }

    public async Task<bool> HasAllPermissionsAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
    {
        var effective = await ResolveAsync(userId, tenantId, ct).ConfigureAwait(false);
        return effective.HasAllPermissions(permissions);
    }

    public async Task<bool> HasAnyPermissionAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
    {
        var effective = await ResolveAsync(userId, tenantId, ct).ConfigureAwait(false);
        return effective.HasAnyPermission(permissions);
    }

    /// <summary>
    ///     Adds the allow/deny contributions of one <see cref="TenantPermission"/> layer.
    ///     Inactive (<c>IsActive == false</c>) or expired rows contribute nothing.
    /// </summary>
    private async Task AddTenantPermissionLayerAsync(
        Guid? userId,
        Guid? tenantId,
        PermissionSource source,
        HashSet<string> allows,
        HashSet<string> denies,
        Dictionary<string, PermissionSource> sources,
        CancellationToken ct)
    {
        var layer = await tenantPermissionRepository
            .GetByUserAndTenantAsync(userId, tenantId, ct)
            .ConfigureAwait(false);
        if (layer is null || !layer.IsActive || layer.IsExpired())
        {
            return;
        }

        foreach (var permission in layer.Permissions)
        {
            if (allows.Add(permission))
            {
                sources[permission] = source;
            }
        }

        denies.UnionWith(layer.DenyPermissions);
    }

    private static EffectivePermissions FailClosed(EffectivePermissionContext context)
        => new()
        {
            UserId = context.UserId,
            TenantId = context.TenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase),
            Context = context,
            ContextValid = false
        };

    /// <summary>
    ///     Universal wildcards are role-management constructs and are never delegable to
    ///     effective permission resolution.
    /// </summary>
    private static bool IsDelegableRolePermission(string permission) =>
        !string.Equals(permission, "admin:*", StringComparison.OrdinalIgnoreCase);
}
