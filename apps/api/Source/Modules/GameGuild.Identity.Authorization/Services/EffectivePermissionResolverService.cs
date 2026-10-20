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
///     <para>
///         <b>Owner override (issue #372).</b> When the context names an exact resource,
///         an unrevoked and unexpired ownership designation for that (tenant, user,
///         resource) tuple mints the owner's permission set — unqualified for the tenant
///         layer and <c>{ResourceType}.{ResourceId}.{permission}</c> for the DAC resource
///         layer — attributed <see cref="PermissionSource.Owner"/>. Precedence is
///         configurable under <c>Authorization:OwnerOverride</c>:
///         <c>AllowUnlessDenied</c> (default; deny-wins over owner — fail-closed),
///         <c>AlwaysAllow</c> (owner-minted grants are non-deniable, like the static
///         system-account wildcard) and <c>Off</c> (no override at all). Unknown mode
///         values fail closed to <c>Off</c> with a warning. <b>Temporal precedence</b>
///         (issue #363): the override's grants are bounded by the more restrictive of
///         the ownership designation's own expiry and any other in-force time-bound
///         grant contributing the same permission; the effective bound is reported on
///         <see cref="EffectivePermissions.OwnerOverride"/> and a lapsed ownership
///         designation applies no override. Override <em>use</em> is audited at DEBUG
///         level (kept out of the durable audit trail to avoid decision-path noise);
///         ownership <em>changes</em> are durable-audited by
///         <c>TransferOwnershipCommand</c>.
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
    IExternalAuthorizationDecisionProvider? externalDecisionProvider = null,
    IResourceOwnershipProvider? ownershipProvider = null,
    IContentTypePermissionGrantRepository? contentTypePermissionGrantRepository = null,
    IContentTypeHierarchyProvider? contentTypeHierarchyProvider = null
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
                LogRedaction.RedactId(context.UserId, "uid"), LogRedaction.RedactId(context.TenantId),
                context.ResourceType ?? "<none>", context.ResourceId ?? "<none>");
            return FailClosed(context);
        }

        // Enumeration protection (issue #358): a throttled user+tenant pair fails closed
        // immediately, without leaking store contents to a probing client.
        if (denialThrottle is not null && denialThrottle.IsThrottled(context.UserId, context.TenantId))
        {
            logger.LogWarning(
                "Effective permission resolution in tenant {TenantId} short-circuited by the evaluation denial throttle (fail-closed).",
                LogRedaction.RedactId(context.TenantId));
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

        // Layer 8: Content-type grants (issues #360/#365) — only when the context names a
        // content type (explicitly, or derived from the resource type: a resource of type
        // T is content of content type T). Each applicable row mints ONLY the
        // content-type qualified name {ContentTypeName}.{permission}, so a content-type
        // grant never conveys a tenant-wide (unqualified) permission and never applies to
        // another content type; inheritance still flows downward because the same
        // qualified name is what layer-Auto checks consult for every resource of the
        // type. Deny entries subtract the same qualified name (DENY-WINS).
        if (contentTypePermissionGrantRepository is not null)
        {
            await AddContentTypePermissionLayerAsync(context, allows, denies, sources, ct).ConfigureAwait(false);
        }

        // Layer 9: Resource grants — only when the context names this exact resource.
        // Resource grants never contribute to tenant-wide results (context isolation).
        var inForceResourceGrants = new List<ResourceUserPermission>();
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

                inForceResourceGrants.Add(grant);
                foreach (var permission in grant.Permissions)
                {
                    AddAllow(permission, PermissionSource.ResourceGrant);
                }
            }
        }

        // Owner-override layer (issue #372): an ownership designation for the exact
        // context resource mints the owner's permission set for owner-scoped operations,
        // with a configurable deny interaction (Authorization:OwnerOverride) and the
        // temporal precedence bound (issue #363). See the class remarks for the full
        // precedence contract.
        var ownerOverride = await AddOwnerOverrideLayerAsync(context, allows, sources, inForceResourceGrants, ct).ConfigureAwait(false);
        var ownerMintedPermissions = ownerOverride?.GrantedPermissions.ToList() ?? [];

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
                    LogRedaction.RedactId(context.TenantId));
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
        // permissions are the only grants that survive an explicit deny — except under
        // the AlwaysAllow owner-override mode (issue #372), where owner-minted grants
        // are equally non-deniable by explicit configuration.
        var effectivePermissions = new HashSet<string>(allows, StringComparer.OrdinalIgnoreCase);
        foreach (var denied in denies)
        {
            if (sources.TryGetValue(denied, out var source) && source == PermissionSource.Static)
            {
                logger.LogWarning(
                    "Attempted to deny static permission {Permission} for user {UserId} - denies cannot override static grants",
                    denied, LogRedaction.RedactId(context.UserId, "uid"));
                continue;
            }

            if (ownerOverride is not null
                && ownerOverride.Mode == OwnerOverrideMode.AlwaysAllow
                && ownerMintedPermissions.Contains(denied))
            {
                logger.LogWarning(
                    "Attempted to deny owner-minted permission {Permission} - the AlwaysAllow owner-override mode makes owner grants non-deniable",
                    denied);
                continue;
            }

            if (effectivePermissions.Remove(denied))
            {
                sources.Remove(denied);
            }
        }

        logger.LogDebug(
            "Resolved {Count} effective permissions ({AllowCount} allowed, {DenyCount} denied) for user {UserId} in tenant {TenantId}",
            effectivePermissions.Count, allows.Count, denies.Count,
            LogRedaction.RedactId(context.UserId, "uid"), LogRedaction.RedactId(context.TenantId));

        return new EffectivePermissions
        {
            UserId = context.UserId,
            TenantId = context.TenantId,
            Permissions = effectivePermissions,
            Sources = sources,
            RoleContributions = roleContributions,
            Context = context,
            ContextValid = true,
            OwnerOverride = ownerOverride
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
                    LogRedaction.Sanitize(permission),
                    LogRedaction.RedactId(context.UserId, "uid"), LogRedaction.RedactId(context.TenantId));
                denies.Add(permission);
                continue;
            }

            if (decision is null || decision.Outcome != ExternalAuthorizationOutcome.Deny)
            {
                continue;
            }

            logger.LogDebug(
                "External authorization decision provider denied permission {Permission} for user {UserId} in tenant {TenantId}: {Reasons}",
                LogRedaction.Sanitize(permission),
                LogRedaction.RedactId(context.UserId, "uid"), LogRedaction.RedactId(context.TenantId),
                LogRedaction.Sanitize(string.Join("; ", decision.Reasons)));
            denies.Add(permission);
        }
    }

    /// <summary>
    ///     Owner-override layer (issue #372). Applies the configured
    ///     <c>Authorization:OwnerOverride</c> precedence mode for the exact resource in
    ///     the resolution context:
    ///     <list type="bullet">
    ///         <item><c>Off</c> (also the fail-closed fallback for unknown mode values): no override.</item>
    ///         <item><c>AllowUnlessDenied</c> (default): the owner's set is minted and stays subject to the DENY-WINS subtraction (deny-wins over owner — fail-closed).</item>
    ///         <item><c>AlwaysAllow</c>: additionally, owner-minted grants survive explicit denies.</item>
    ///     </list>
    ///     Ownership must be an unrevoked designation for exactly (tenant, user, resource
    ///     type, resource id). <b>Temporal precedence (issue #363):</b> a designation whose
    ///     own expiry has lapsed applies no override, and the effective bound reported on
    ///     the evaluation is the earliest expiry among the designation and every other
    ///     in-force time-bound grant of the same resource contributing the same permission
    ///     — the more restrictive expiry wins. Override use is audited at DEBUG level only
    ///     (see class remarks); a null ownership provider behaves like <c>Off</c>.
    /// </summary>
    private async Task<OwnerOverrideEvaluation?> AddOwnerOverrideLayerAsync(
        EffectivePermissionContext context,
        HashSet<string> allows,
        Dictionary<string, PermissionSource> sources,
        IReadOnlyList<ResourceUserPermission> inForceResourceGrants,
        CancellationToken ct)
    {
        if (!context.HasResource || ownershipProvider is null)
        {
            return null;
        }

        var ownerOptions = _authOptions.OwnerOverride ?? new OwnerOverrideOptions();
        var mode = ownerOptions.ResolveMode();
        if (mode == OwnerOverrideMode.Off)
        {
            if (!ownerOptions.IsKnownMode)
            {
                logger.LogWarning(
                    "Unknown Authorization:OwnerOverride:Mode '{Mode}' - the owner override fails closed to Off in tenant {TenantId}.",
                    ownerOptions.Mode, LogRedaction.RedactId(context.TenantId));
            }

            return null;
        }

        var ownership = await ownershipProvider
            .GetOwnerAsync(context.TenantId, context.UserId, context.ResourceType!, context.ResourceId!, ct)
            .ConfigureAwait(false);
        if (ownership is null)
        {
            return null;
        }

        // Temporal precedence (issue #363), first half: an expired time-bound ownership
        // designation applies no override (fail-closed), even though the row is unrevoked.
        var now = SystemClock.UtcNow;
        if (ownership.ExpiresAt is { } ownerExpiry && ownerExpiry <= now)
        {
            logger.LogDebug(
                "Owner override suppressed on {ResourceType}/{ResourceId}: the ownership designation expired at {OwnerExpiry}.",
                context.ResourceType, context.ResourceId, ownerExpiry);
            return null;
        }

        // Temporal precedence (issue #363), second half: when another in-force time-bound
        // grant of the same resource contributes a permission the override mints, the more
        // restrictive (earlier) expiry wins for the whole override grant set.
        DateTime? effectiveThrough = ownership.ExpiresAt;
        foreach (var grant in inForceResourceGrants)
        {
            if (grant.ExpiresAt is not { } grantExpiry || grantExpiry <= now)
            {
                continue;
            }

            if (grant.Permissions.Any(ownerMints => ownership.OwnerPermissions.Contains(ownerMints, StringComparer.OrdinalIgnoreCase))
                && (effectiveThrough is null || grantExpiry < effectiveThrough))
            {
                effectiveThrough = grantExpiry;
            }
        }

        var minted = new List<string>();
        foreach (var permission in ownership.OwnerPermissions.Where(IsDelegableOwnerPermission))
        {
            // Unqualified name: parity with the resource-grant layer's plain-name minting,
            // so tenant-layer checks succeed for owners in a resource context.
            if (allows.Add(permission))
            {
                sources[permission] = PermissionSource.Owner;
            }

            minted.Add(permission);

            // Resource-qualified name: parity with the DAC resource layer
            // ({ResourceType}.{ResourceId}.{Permission}) so owner-scoped operations
            // resolve through IDacPermissionResolver too.
            var qualified = $"{context.ResourceType}.{context.ResourceId}.{permission}";
            if (allows.Add(qualified))
            {
                sources[qualified] = PermissionSource.Owner;
            }

            minted.Add(qualified);
        }

        // Override-use audit (issue #372): DEBUG level by design — the decision path is
        // high-frequency and a durable entry per evaluation would drown the audit log.
        // Durable audit coverage for ownership changes lives in TransferOwnershipCommand.
        logger.LogDebug(
            "Owner override applied on {ResourceType}/{ResourceId} in tenant {TenantId}: mode {Mode}, {PermissionCount} owner permissions, effective through {EffectiveThrough}.",
            context.ResourceType, context.ResourceId, LogRedaction.RedactId(context.TenantId), mode, minted.Count,
            effectiveThrough is null ? "indefinitely" : effectiveThrough.Value.ToString("O"));

        return new OwnerOverrideEvaluation(mode, ownership.OwnerUserId, minted, effectiveThrough);
    }

    /// <summary>
    ///     Owner overrides never mint universal wildcards: <c>admin:*</c> is a
    ///     role-management construct and <c>*</c> is the static system-account wildcard.
    /// </summary>
    private static bool IsDelegableOwnerPermission(string permission) =>
        !string.Equals(permission, "admin:*", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(permission, "*", StringComparison.Ordinal);

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

    /// <summary>
    ///     Content-type grant layer (issues #360/#365). For every content type named by
    ///     the context (explicit <see cref="EffectivePermissionContext.ContentType"/>, plus
    ///     the resource type for resource contexts), loads the applicable
    ///     <see cref="ContentTypePermissionGrant"/> rows — the tenant default
    ///     (<c>UserId = null</c>) first, then the user-specific row — and mints each allow
    ///     entry as the content-type qualified name <c>{ContentTypeName}.{permission}</c>,
    ///     attributing <see cref="PermissionSource.ContentTypeDefault"/> /
    ///     <see cref="PermissionSource.ContentTypeGrant"/>. Deny entries subtract the same
    ///     qualified name (DENY-WINS).
    ///     <para>
    ///         Qualified-only minting keeps the layer scoped: a content-type grant never
    ///         conveys a tenant-wide (unqualified) permission and never applies to another
    ///         content type, while tenant-layer grants still satisfy layer-Auto checks
    ///         (inheritance flows tenant → content-type → resource, not upward). Inactive
    ///         or expired rows contribute nothing (fail-closed), and rows of other users,
    ///         tenants or content types are never consulted (context isolation).
    ///     </para>
    /// </summary>
    private async Task AddContentTypePermissionLayerAsync(
        EffectivePermissionContext context,
        HashSet<string> allows,
        HashSet<string> denies,
        Dictionary<string, PermissionSource> sources,
        CancellationToken ct)
    {
        var contentTypeNames = new List<string>();
        if (context.HasContentType)
        {
            contentTypeNames.Add(context.ContentType!.Trim());
        }

        // A resource of type T is content of content type T: deriving the content type
        // from the resource type is what makes content-type grants apply to every
        // resource of the type (ct → resource inheritance).
        if (context.HasResource)
        {
            var resourceTypeName = context.ResourceType!.Trim();
            if (!contentTypeNames.Contains(resourceTypeName, StringComparer.Ordinal))
            {
                contentTypeNames.Add(resourceTypeName);
            }
        }

        foreach (var contentTypeName in contentTypeNames)
        {
            await AddContentTypeGrantRowAsync(null, PermissionSource.ContentTypeDefault, contentTypeName, context, allows, denies, sources, ct).ConfigureAwait(false);
            await AddContentTypeGrantRowAsync(context.UserId, PermissionSource.ContentTypeGrant, contentTypeName, context, allows, denies, sources, ct).ConfigureAwait(false);

            // Hierarchy inheritance (issue #314): grants on registered ancestor content
            // types apply to this type too, minted under THIS type's qualified name so
            // exact-name checks for the descendant find them. Inheritance flows
            // downward only; a cycle-broken or unregistered chain contributes nothing.
            if (contentTypeHierarchyProvider is not null)
            {
                var ancestors = await contentTypeHierarchyProvider
                    .GetAncestorsAsync(contentTypeName, context.TenantId, ct)
                    .ConfigureAwait(false);
                foreach (var ancestor in ancestors)
                {
                    await AddContentTypeGrantRowAsync(null, PermissionSource.ContentTypeInheritedDefault, ancestor, context, allows, denies, sources, ct, inheritInto: contentTypeName).ConfigureAwait(false);
                    await AddContentTypeGrantRowAsync(context.UserId, PermissionSource.ContentTypeInheritedGrant, ancestor, context, allows, denies, sources, ct, inheritInto: contentTypeName).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    ///     Adds the contribution of one <see cref="ContentTypePermissionGrant"/> row
    ///     (inactive, expired, soft-deleted or absent rows contribute nothing). When
    ///     <paramref name="inheritInto"/> is set, the row's permissions mint under the
    ///     descendant's qualified name instead of the row's own content type (hierarchy
    ///     inheritance, issue #314).
    /// </summary>
    private async Task AddContentTypeGrantRowAsync(
        Guid? userId,
        PermissionSource source,
        string contentTypeName,
        EffectivePermissionContext context,
        HashSet<string> allows,
        HashSet<string> denies,
        Dictionary<string, PermissionSource> sources,
        CancellationToken ct,
        string? inheritInto = null)
    {
        var row = await contentTypePermissionGrantRepository!
            .GetBySubjectAndContentTypeAsync(userId, context.TenantId, contentTypeName, ct)
            .ConfigureAwait(false);
        if (row is null || !row.IsActive || row.IsExpired())
        {
            return;
        }

        var mintFor = inheritInto ?? contentTypeName;

        foreach (var permission in row.Permissions)
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                continue;
            }

            var qualified = $"{mintFor}.{permission.Trim()}";
            if (allows.Add(qualified))
            {
                sources[qualified] = source;
            }
        }

        foreach (var denied in row.DenyPermissions)
        {
            if (string.IsNullOrWhiteSpace(denied))
            {
                continue;
            }

            denies.Add($"{mintFor}.{denied.Trim()}");
        }
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
