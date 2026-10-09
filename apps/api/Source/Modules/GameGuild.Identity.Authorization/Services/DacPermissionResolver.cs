using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Centralized permission resolution for the 3-layer DAC model (issue #339).
/// </summary>
/// <remarks>
///     <para>
///         Implements <see cref="IDacPermissionResolver"/> as a thin, deterministic facade
///         over the canonical <see cref="IEffectivePermissionResolver"/> engine (issue
///         #330): it centralizes the DAC layer vocabulary — building the layer-qualified
///         permission names previously duplicated by
///         <c>ResourcePermissionAuthorizationFilter</c> and callers — while every allow/deny
///         decision remains the engine's (DENY-WINS, deny-by-default, fail-closed).
///     </para>
///     <para>
///         <b>Name conventions (single source of truth for DAC layers):</b>
///         <list type="table">
///             <item>Tenant layer: <c>{Permission}</c></item>
///             <item>Content-type layer: <c>{ContentType}.{Permission}</c></item>
///             <item>Resource layer: <c>{ResourceType}.{ResourceId}.{Permission}</c></item>
///         </list>
///     </para>
///     <para>
///         <b>Context selection.</b> A complete resource (type, id) pair resolves through
///         the canonical resource context (so matching resource grants contribute and
///         unrelated resource grants stay isolated); otherwise the tenant context is used.
///     </para>
///     <para>
///         <b>Fail-closed.</b> Invalid queries — empty user or tenant, a half-specified
///         resource pair, a layer without its required scope, or an unknown layer value —
///         resolve to an empty permission set with
///         <see cref="DacPermissionResolution.ContextValid"/> = <c>false</c> and never
///         consult the engine.
///     </para>
/// </remarks>
public sealed class DacPermissionResolver(
    IEffectivePermissionResolver effectiveResolver,
    ILogger<DacPermissionResolver> logger
) : IDacPermissionResolver
{
    public async Task<DacPermissionResolution> ResolveAsync(
        DacPermissionQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!query.IsValid)
        {
            logger.LogWarning(
                "DAC permission resolution requested with an invalid query (user {UserId}, tenant {TenantId}, layer {Layer}, content type {ContentType}, resource {ResourceType}/{ResourceId}) - returning empty permissions (fail-closed).",
                query.UserId,
                query.TenantId,
                query.Layer,
                query.ContentType ?? "<none>",
                query.ResourceType ?? "<none>",
                query.ResourceId ?? "<none>");
            return new DacPermissionResolution
            {
                Query = query,
                Effective = new EffectivePermissions
                {
                    UserId = query.UserId,
                    TenantId = query.TenantId,
                    Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase),
                    ContextValid = false
                }
            };
        }

        // A complete resource pair routes through the canonical resource context so that
        // resource grants for exactly this resource contribute (context isolation).
        var context = query.HasResource
            ? EffectivePermissionContext.ForResource(
                query.UserId, query.TenantId, query.ResourceType!, query.ResourceId!)
            : EffectivePermissionContext.ForTenant(query.UserId, query.TenantId);

        var effective = await effectiveResolver
            .ResolveAsync(context, cancellationToken)
            .ConfigureAwait(false);

        logger.LogDebug(
            "DAC permission resolution: user {UserId}, tenant {TenantId}, layer {Layer}, content type {ContentType}, resource {ResourceType}/{ResourceId}, context valid {ContextValid}, throttled {Throttled}, effective permissions {EffectiveCount}.",
            query.UserId,
            query.TenantId,
            query.Layer,
            query.ContentType ?? "<none>",
            query.ResourceType ?? "<none>",
            query.ResourceId ?? "<none>",
            effective.ContextValid,
            effective.Throttled,
            effective.Permissions.Count);

        return new DacPermissionResolution
        {
            Query = query,
            Effective = effective
        };
    }

    public async Task<bool> HasPermissionAsync(
        DacPermissionQuery query,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (!query.IsValid || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var resolution = await ResolveAsync(query, cancellationToken).ConfigureAwait(false);
        return resolution.Grants(permission);
    }

    /// <summary>
    ///     Layer-aware grant check against a canonical effective set (shared by
    ///     <see cref="DacPermissionResolution.Grants"/>). Explicit layers check only their
    ///     own qualified name; <see cref="PermissionLayer.Auto"/> grants when any
    ///     applicable layer grants. Unknown layers and invalid contexts fail closed.
    /// </summary>
    internal static bool Grants(DacPermissionQuery query, EffectivePermissions effective, string permission)
    {
        if (!query.IsValid || string.IsNullOrWhiteSpace(permission) || !effective.ContextValid)
        {
            return false;
        }

        return query.Layer switch
        {
            PermissionLayer.Tenant => effective.HasPermission(permission),
            PermissionLayer.ContentType => effective.HasPermission($"{query.ContentType}.{permission}"),
            PermissionLayer.Resource => effective.HasPermission($"{query.ResourceType}.{query.ResourceId}.{permission}"),
            PermissionLayer.Auto => GrantsAtAnyApplicableLayer(query, effective, permission),
            _ => false // Unknown layer value: fail closed.
        };
    }

    private static bool GrantsAtAnyApplicableLayer(
        DacPermissionQuery query,
        EffectivePermissions effective,
        string permission)
    {
        if (effective.HasPermission(permission))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(query.ContentType)
            && effective.HasPermission($"{query.ContentType}.{permission}"))
        {
            return true;
        }

        return query.HasResource
            && effective.HasPermission($"{query.ResourceType}.{query.ResourceId}.{permission}");
    }
}
