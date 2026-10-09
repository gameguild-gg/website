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
///     <para>
///         <b>Extension point (issue #358).</b> Registered
///         <see cref="IPermissionEvaluationExtension"/> plugins run after built-in layers
///         1-8, in ascending <see cref="IPermissionEvaluationExtension.Order"/> (ties in DI
///         registration order), each observing the accumulated allow/deny sets. Their
///         contributions are attributed <see cref="PermissionSource.Extension"/> and remain
///         fully subject to DENY-WINS; a throwing extension is logged and skipped so a
///         broken plugin can neither open nor break the permission decision path.
///     </para>
///     <para>
///         <b>External authorization decisions (issue #146).</b> A registered
///         <see cref="IExternalAuthorizationDecisionProvider"/> is consulted for every
///         locally-allowed permission after all local layers and evaluation extensions,
///         immediately before the DENY-WINS subtraction. The external layer is
///         <b>veto-only</b>: an external deny unions into the deny set (it overrides a
///         local allow), while an external allow, a not-applicable and a missing decision
///         never add or resurrect a permission — grants remain exclusively local
///         (fail-closed paramount; absent = deny per #327). The static system-account
///         wildcard stays non-deniable. A provider that violates the never-throw
///         contract fails the permission under question closed. See
///         <c>apps/api/docs/effective-permission-resolution.md</c>.
///     </para>
///     <para>
///         <b>Evaluation throttle (issue #358).</b> When the optional
///         <see cref="IEvaluationDenialThrottleService"/> reports a throttled user+tenant
///         pair, resolution short-circuits to an empty fail-closed result
///         (<see cref="EffectivePermissions.Throttled"/> = true) without consulting any
///         permission store.
///     </para>
/// </remarks>
public sealed class EffectivePermissionResolverService(
    ITenantPermissionRepository tenantPermissionRepository,
    IRbacPermissionResolver rbacResolver,
    IEnumerable<IAuthorizationRolePermissionProvider> rolePermissionProviders,
    IResourcePermissionService resourcePermissionService,
    IOptions<AuthorizationOptions> authorizationOptions,
    ILogger<EffectivePermissionResolverService> logger,
    IJitElevationRequestRepository? jitElevationRepository = null,
    IEnumerable<IPermissionEvaluationExtension>? evaluationExtensions = null,
    IEvaluationDenialThrottleService? denialThrottle = null,
    IExternalAuthorizationDecisionProvider? externalDecisionProvider = null
) : IEffectivePermissionResolver
{
    private readonly AuthorizationOptions _authOptions = authorizationOptions.Value;
    private readonly List<IPermissionEvaluationExtension> _extensions =
        (evaluationExtensions ?? Enumerable.Empty<IPermissionEvaluationExtension>())
            .OrderBy(extension => extension.Order)
            .ToList();

    public async Task<EffectivePermissions> ResolveAsync(
        EffectivePermissionContext context,
        CancellationToken ct = default)
    {
        if (!context.IsValid)
        {
            logger.LogWarning(
                "Effective permission resolution requested with an invalid context (user {UserId}, tenant {TenantId}, resource {ResourceType}/{ResourceId}) - returning empty permissions (fail-closed).",
                context.UserId, context.TenantId, context.ResourceType ?? "<none>", context.ResourceId ?? "<none>"); // codeql[cs/cleartext-storage-of-sensitive-information] intentional user/tenant Guid audit logging
            return FailClosed(context);
        }

        // Enumeration protection (issue #358): a throttled user+tenant pair fails closed
        // immediately, without leaking store contents to a probing client.
        if (denialThrottle is not null && denialThrottle.IsThrottled(context.UserId, context.TenantId))
        {
            logger.LogWarning(
                "Effective permission resolution in tenant {TenantId} short-circuited by the evaluation denial throttle (fail-closed).",
                context.TenantId);
            return new EffectivePermissions
            {
                UserId = context.UserId,
                TenantId = context.TenantId,
                Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase),
                Context = context,
                ContextValid = true,
                Throttled = true
            };
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

        // Extension layer (issue #358): registered evaluation plugins run after every
        // built-in layer, ordered by Order then DI registration order. Each extension
        // observes the accumulated sets; contributions are subject to DENY-WINS below
        // and can never mint static or non-delegable grants.
        foreach (var extension in _extensions)
        {
            PermissionEvaluationExtensionResult? contribution = null;
            try
            {
                contribution = await extension.EvaluateAsync(
                    new PermissionEvaluationExtensionContext
                    {
                        UserId = context.UserId,
                        TenantId = context.TenantId,
                        ResourceType = context.ResourceType,
                        ResourceId = context.ResourceId,
                        CurrentAllows = allows,
                        CurrentDenies = denies
                    },
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A broken plugin must not break (or open) the decision path: skip it.
                logger.LogError(
                    exception,
                    "Permission evaluation extension {ExtensionName} threw during evaluation in tenant {TenantId} - its contributions are skipped.",
                    extension.Name,
                    context.TenantId);
            }

            if (contribution is null)
            {
                continue;
            }

            foreach (var permission in contribution.AdditionalAllows.Where(IsDelegableRolePermission))
            {
                AddAllow(permission, PermissionSource.Extension);
            }

            denies.UnionWith(contribution.AdditionalDenies);
        }

        // External authorization-decision layer (issue #146): a registered decision
        // provider is consulted for every locally-allowed permission after all local
        // layers and evaluation extensions. The layer is veto-only — an external deny
        // unions into the deny set below, an external allow / not-applicable / missing
        // decision changes nothing. Grants stay exclusively local (fail-closed).
        await AddExternalAuthorizationLayerAsync(context, allows, denies, sources, ct).ConfigureAwait(false);

        // DENY-WINS: subtract every layer's explicit denies. Static (system-account)
        // permissions are the only grants that survive an explicit deny.
        var effectivePermissions = new HashSet<string>(allows, StringComparer.OrdinalIgnoreCase);
        foreach (var denied in denies)
        {
            if (sources.TryGetValue(denied, out var source) && source == PermissionSource.Static)
            {
                logger.LogWarning(
                    "Attempted to deny static permission {Permission} for user {UserId} - denies cannot override static grants",
                    denied, context.UserId); // codeql[cs/cleartext-storage-of-sensitive-information] intentional user/tenant Guid audit logging
                continue;
            }

            if (effectivePermissions.Remove(denied))
            {
                sources.Remove(denied);
            }
        }

        logger.LogDebug(
            "Resolved {Count} effective permissions ({AllowCount} allowed, {DenyCount} denied) for user {UserId} in tenant {TenantId}",
            effectivePermissions.Count, allows.Count, denies.Count, context.UserId, context.TenantId); // codeql[cs/cleartext-storage-of-sensitive-information] intentional user/tenant Guid audit logging

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
        var hasPermission = effective.Permissions.Contains(permission);
        RecordDeniedEvaluationIfNeeded(userId, tenantId, effective, hasPermission);
        return hasPermission;
    }

    public async Task<bool> HasAllPermissionsAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
    {
        var effective = await ResolveAsync(userId, tenantId, ct).ConfigureAwait(false);
        var hasAll = effective.HasAllPermissions(permissions);
        RecordDeniedEvaluationIfNeeded(userId, tenantId, effective, hasAll);
        return hasAll;
    }

    public async Task<bool> HasAnyPermissionAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
    {
        var effective = await ResolveAsync(userId, tenantId, ct).ConfigureAwait(false);
        var hasAny = effective.HasAnyPermission(permissions);
        RecordDeniedEvaluationIfNeeded(userId, tenantId, effective, hasAny);
        return hasAny;
    }

    /// <summary>
    ///     External authorization-decision layer (issue #146). Consults the registered
    ///     <see cref="IExternalAuthorizationDecisionProvider"/> for every locally-allowed
    ///     permission and unions external denials into the deny set, which the DENY-WINS
    ///     subtraction then applies like any local deny. Veto-only by design: no external
    ///     outcome adds a permission, so an external allow can never override a local deny
    ///     and can never mint a grant. Static system-account grants are not queried (they
    ///     are non-deniable anyway). An implementation that throws despite the
    ///     never-throw contract denies the permission under question (fail-closed).
    /// </summary>
    private async Task AddExternalAuthorizationLayerAsync(
        EffectivePermissionContext context,
        HashSet<string> allows,
        HashSet<string> denies,
        Dictionary<string, PermissionSource> sources,
        CancellationToken ct)
    {
        if (externalDecisionProvider is null || allows.Count == 0)
        {
            return;
        }

        foreach (var permission in allows)
        {
            if (sources.TryGetValue(permission, out var source) && source == PermissionSource.Static)
            {
                continue;
            }

            ExternalAuthorizationDecision? decision;
            try
            {
                decision = await externalDecisionProvider.EvaluateAsync(
                    new ExternalAuthorizationQuery
                    {
                        UserId = context.UserId,
                        TenantId = context.TenantId,
                        Permission = permission,
                        ResourceType = context.ResourceType,
                        ResourceId = context.ResourceId
                    },
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "External authorization decision provider threw while evaluating permission {Permission} for user {UserId} in tenant {TenantId} - the permission fails closed.",
                    permission, context.UserId, context.TenantId); // codeql[cs/cleartext-storage-of-sensitive-information] intentional user/tenant Guid audit logging
                denies.Add(permission);
                continue;
            }

            if (decision is null || decision.Outcome != ExternalAuthorizationOutcome.Deny)
            {
                continue;
            }

            logger.LogDebug(
                "External authorization decision provider denied permission {Permission} for user {UserId} in tenant {TenantId}: {Reasons}",
                permission, context.UserId, context.TenantId, string.Join("; ", decision.Reasons)); // codeql[cs/cleartext-storage-of-sensitive-information] intentional user/tenant Guid audit logging
            denies.Add(permission);
        }
    }

    /// <summary>
    ///     Feeds denied evaluation outcomes to the throttle (enumeration protection,
    ///     issue #358). Only evaluations with a valid tenant scope are tracked; an
    ///     already-throttled (fail-closed empty) result is not re-counted.
    /// </summary>
    private void RecordDeniedEvaluationIfNeeded(Guid userId, Guid? tenantId, EffectivePermissions effective, bool allowed)
    {
        if (allowed || denialThrottle is null || effective.Throttled || tenantId is not Guid tenant)
        {
            return;
        }

        denialThrottle.RecordDenial(userId, tenant);
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
