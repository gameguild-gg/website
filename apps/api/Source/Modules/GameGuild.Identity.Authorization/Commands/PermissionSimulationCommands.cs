using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Kind of subject a what-if simulation is run against (issue #374).
/// </summary>
public enum PermissionSimulationSubjectKind
{
    /// <summary>An individual user: simulated through the full effective resolver.</summary>
    User = 0,

    /// <summary>A dynamic role: simulated against the role's stored allow/deny sets.</summary>
    Role = 1,

    /// <summary>A tenant's default layer: simulated against the tenant-default grant row.</summary>
    Tenant = 2
}

/// <summary>
///     What-if permission simulation (issue #374): computes the before/after effective
///     permission diff for a hypothetical grant/revoke delta <b>without applying
///     anything</b>. The command is a pure dry-run — it performs no writes, bumps no
///     security versions and writes no audit entries.
/// </summary>
/// <remarks>
///     <para>
///         For <see cref="SubjectKind"/> = <see cref="PermissionSimulationSubjectKind.User"/>
///         the "before" state is resolved through the canonical effective-permission
///         resolver (every layer, DENY-WINS, fail-closed on invalid contexts); the delta
///         is then overlaid with the resolver's own precedence semantics: hypothetical
///         grants union into the allow set, hypothetical revokes behave as explicit
///         denies (DENY-WINS), and static system-account grants stay non-revocable.
///     </para>
///     <para>
///         For <see cref="PermissionSimulationSubjectKind.Role"/> and
///         <see cref="PermissionSimulationSubjectKind.Tenant"/> the same overlay
///         arithmetic is applied to the stored role / tenant-default permission sets.
///     </para>
///     <para><b>SECURITY:</b> Requires an authenticated actor. Simulating one's own user
///     subject is allowed (self what-if); any other subject requires same-tenant
///     administration or system administration, because the result enumerates permission
///     sets. The tenant scope comes from the actor context unless the actor is a system
///     administrator targeting another tenant explicitly.</para>
/// </remarks>
public sealed record SimulatePermissionChangeCommand : ICommand<PermissionSimulationResult>
{
    /// <summary>The kind of subject being simulated.</summary>
    public required PermissionSimulationSubjectKind SubjectKind { get; init; }

    /// <summary>The subject user (required for <see cref="PermissionSimulationSubjectKind.User"/>).</summary>
    public Guid? UserId { get; init; }

    /// <summary>The subject role name (required for <see cref="PermissionSimulationSubjectKind.Role"/>).</summary>
    public string? RoleKey { get; init; }

    /// <summary>
    ///     The tenant scope. Defaults to the acting tenant. A system administrator may
    ///     target any tenant; every other actor is pinned to their own tenant.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>Hypothetical permission grants.</summary>
    public string[] Grants { get; init; } = Array.Empty<string>();

    /// <summary>Hypothetical permission revokes (applied as DENY-WINS denies).</summary>
    public string[] Revokes { get; init; } = Array.Empty<string>();

    /// <summary>Optional resource scope for user-subject resolution (type + id).</summary>
    public string? ResourceType { get; init; }

    /// <summary>Optional resource scope for user-subject resolution (type + id).</summary>
    public string? ResourceId { get; init; }
}

/// <summary>
///     Handler for <see cref="SimulatePermissionChangeCommand"/> (issue #374).
/// </summary>
public sealed class SimulatePermissionChangeCommandHandler(
    IEffectivePermissionResolver effectivePermissionResolver,
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ILogger<SimulatePermissionChangeCommandHandler> logger)
    : ICommandHandler<SimulatePermissionChangeCommand, PermissionSimulationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionSimulationResult> Handle(
        SimulatePermissionChangeCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (request.Grants.Length == 0 && request.Revokes.Length == 0)
        {
            throw new ArgumentException("A simulation requires at least one grant or revoke");
        }

        var overlapping = request.Grants.Intersect(request.Revokes, StringComparer.OrdinalIgnoreCase).ToList();
        if (overlapping.Count > 0)
        {
            throw new ArgumentException(
                $"A permission cannot be both granted and revoked in one simulation: {string.Join(", ", overlapping)}");
        }

        var tenantId = request.TenantId ?? Actor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Permission simulation requires a tenant scope");
        }

        var isSelf = request.SubjectKind == PermissionSimulationSubjectKind.User
            && request.UserId == Actor.SubjectIdAsGuid
            && Actor.TenantId == tenantId;

        if (!isSelf && !Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != tenantId))
        {
            throw new UnauthorizedAccessException(
                "Simulating another subject's permissions requires tenant administration for that tenant or system administration");
        }

        var result = request.SubjectKind switch
        {
            PermissionSimulationSubjectKind.User => await SimulateUserAsync(request, tenantId.Value, cancellationToken).ConfigureAwait(false),
            PermissionSimulationSubjectKind.Role => await SimulateRoleAsync(request, tenantId.Value, cancellationToken).ConfigureAwait(false),
            PermissionSimulationSubjectKind.Tenant => await SimulateTenantAsync(request, tenantId.Value, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.SubjectKind, "Unknown simulation subject kind")
        };

        logger.LogInformation(
            "Simulated permission change for {SubjectKind} in tenant {TenantId}: +{Granted} granted, -{Revoked} revoked (dry-run, nothing applied)",
            request.SubjectKind, tenantId, result.Granted.Count, result.Revoked.Count);

        return result;
    }

    private async Task<PermissionSimulationResult> SimulateUserAsync(
        SimulatePermissionChangeCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (request.UserId is not { } userId || userId == Guid.Empty)
        {
            throw new ArgumentException("A user simulation requires UserId");
        }

        // Dry-run through the canonical effective resolver: every layer, DENY-WINS,
        // fail-closed on invalid contexts. The resolution is read-only.
        var before = await effectivePermissionResolver.ResolveAsync(
            new EffectivePermissionContext
            {
                UserId = userId,
                TenantId = tenantId,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId
            },
            cancellationToken).ConfigureAwait(false);

        // Overlay the hypothetical delta with the resolver's own precedence semantics:
        // grants union into the allow set; revokes behave as explicit denies
        // (DENY-WINS); static system-account grants are non-revocable.
        var after = new HashSet<string>(before.Permissions, StringComparer.OrdinalIgnoreCase);
        var nonRevocable = new List<string>();
        foreach (var revoked in request.Revokes)
        {
            if (before.Sources.TryGetValue(revoked, out var source) && source == PermissionSource.Static)
            {
                nonRevocable.Add(revoked);
                continue;
            }

            after.Remove(revoked);
        }

        foreach (var granted in request.Grants)
        {
            after.Add(granted);
        }

        return BuildResult(request, tenantId, before.Permissions, after, nonRevocable, contextValid: before.ContextValid);
    }

    private async Task<PermissionSimulationResult> SimulateRoleAsync(
        SimulatePermissionChangeCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RoleKey))
        {
            throw new ArgumentException("A role simulation requires RoleKey");
        }

        var role = await dbContext.Set<DynamicRole>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == request.RoleKey
                && (r.TenantId == tenantId || r.TenantId == null), cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{request.RoleKey}' does not exist in tenant {tenantId}");
        }

        // The role's own stored sets carry the layer's DENY-WINS contract directly.
        var before = new HashSet<string>(role.Permissions, StringComparer.OrdinalIgnoreCase);
        foreach (var denied in role.DenyPermissions)
        {
            before.Remove(denied);
        }

        var after = new HashSet<string>(before, StringComparer.OrdinalIgnoreCase);
        foreach (var revoked in request.Revokes)
        {
            after.Remove(revoked);
        }

        foreach (var granted in request.Grants)
        {
            after.Add(granted);
        }

        return BuildResult(request, tenantId, before, after, [], contextValid: true);
    }

    private async Task<PermissionSimulationResult> SimulateTenantAsync(
        SimulatePermissionChangeCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenantDefaults = await dbContext.Set<TenantPermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == null && p.IsActive, cancellationToken).ConfigureAwait(false);

        var before = new HashSet<string>(
            tenantDefaults?.Permissions ?? [],
            StringComparer.OrdinalIgnoreCase);

        var after = new HashSet<string>(before, StringComparer.OrdinalIgnoreCase);
        foreach (var revoked in request.Revokes)
        {
            after.Remove(revoked);
        }

        foreach (var granted in request.Grants)
        {
            after.Add(granted);
        }

        return BuildResult(request, tenantId, before, after, [], contextValid: true);
    }

    private static PermissionSimulationResult BuildResult(
        SimulatePermissionChangeCommand request,
        Guid tenantId,
        IReadOnlySet<string> before,
        HashSet<string> after,
        IReadOnlyList<string> nonRevocable,
        bool contextValid) => new()
    {
        SubjectKind = request.SubjectKind,
        UserId = request.UserId,
        RoleKey = request.RoleKey,
        TenantId = tenantId,
        ResourceType = request.ResourceType,
        ResourceId = request.ResourceId,
        ContextValid = contextValid,
        Before = before.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
        After = after.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
        Granted = after
            .Where(p => !before.Contains(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        Revoked = before
            .Where(p => !after.Contains(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        Unchanged = before
            .Where(after.Contains)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        NonRevocableRevokes = nonRevocable,
        Applied = false
    };
}

/// <summary>
///     Result of a what-if permission simulation (issue #374). <see cref="Applied"/> is
///     always false: a simulation never mutates state.
/// </summary>
public sealed record PermissionSimulationResult
{
    /// <summary>The simulated subject kind.</summary>
    public PermissionSimulationSubjectKind SubjectKind { get; init; }

    /// <summary>The simulated user (user subjects).</summary>
    public Guid? UserId { get; init; }

    /// <summary>The simulated role name (role subjects).</summary>
    public string? RoleKey { get; init; }

    /// <summary>The tenant scope.</summary>
    public Guid TenantId { get; init; }

    /// <summary>The resource scope type (user subjects with a resource scope).</summary>
    public string? ResourceType { get; init; }

    /// <summary>The resource scope id (user subjects with a resource scope).</summary>
    public string? ResourceId { get; init; }

    /// <summary>False when the resolution context was invalid and the before set is fail-closed empty.</summary>
    public bool ContextValid { get; init; }

    /// <summary>The effective permission set before the hypothetical change.</summary>
    public IReadOnlyList<string> Before { get; init; } = [];

    /// <summary>The effective permission set after the hypothetical change.</summary>
    public IReadOnlyList<string> After { get; init; } = [];

    /// <summary>Permissions the hypothetical change would grant.</summary>
    public IReadOnlyList<string> Granted { get; init; } = [];

    /// <summary>Permissions the hypothetical change would revoke.</summary>
    public IReadOnlyList<string> Revoked { get; init; } = [];

    /// <summary>Permissions unaffected by the hypothetical change.</summary>
    public IReadOnlyList<string> Unchanged { get; init; } = [];

    /// <summary>Revokes that cannot apply because the grant is static and non-deniable.</summary>
    public IReadOnlyList<string> NonRevocableRevokes { get; init; } = [];

    /// <summary>Always false — a simulation is a pure dry-run.</summary>
    public bool Applied { get; init; }
}
