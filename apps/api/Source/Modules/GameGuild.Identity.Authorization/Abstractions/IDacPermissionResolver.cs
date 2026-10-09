namespace GameGuild.Identity.Authorization;

/// <summary>
///     Centralized permission-resolution contract for the 3-layer DAC
///     (Discretionary Access Control) model (issue #339).
/// </summary>
/// <remarks>
///     <para>
///         Single entry point for resolving permissions across the DAC layers
///         (<see cref="PermissionLayer.Tenant"/>, <see cref="PermissionLayer.ContentType"/>,
///         <see cref="PermissionLayer.Resource"/>). Permission-name conventions match the
///         declarative attributes and <c>ResourcePermissionAuthorizationFilter</c>:
///         <list type="table">
///             <item>Tenant layer: the raw permission name (e.g. <c>content:read</c>).</item>
///             <item>Content-type layer: <c>{ContentType}.{Permission}</c> (e.g. <c>Course.Read</c>).</item>
///             <item>Resource layer: <c>{ResourceType}.{ResourceId}.{Permission}</c> (e.g. <c>Course.&lt;id&gt;.Read</c>).</item>
///         </list>
///     </para>
///     <para>
///         <b>This contract does not re-implement evaluation.</b> Every check delegates to
///         the canonical <see cref="IEffectivePermissionResolver"/> engine (DENY-WINS,
///         deny-by-default, fail-closed — issue #330), which already combines RBAC roles,
///         global/tenant defaults, direct grants, JIT elevations and resource grants.
///         Centralization removes the scattered name-building previously duplicated by
///         callers and guarantees one deterministic evaluation pipeline for all layers.
///     </para>
///     <para>
///         <b>Fail-closed.</b> An invalid query (empty user or tenant, a half-specified
///         resource pair, or a layer that lacks its required scope) resolves to an empty
///         permission set and every permission check returns <c>false</c>.
///     </para>
/// </remarks>
public interface IDacPermissionResolver
{
    /// <summary>
    ///     Resolves the effective permissions for a DAC query, evaluated at the most
    ///     specific applicable layer.
    /// </summary>
    /// <param name="query">The DAC query (user + tenant required; content-type/resource optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     The resolution with the canonical <see cref="EffectivePermissions"/> result and
    ///     layer-aware checking helpers. Invalid queries resolve to an empty, fail-closed
    ///     result (<see cref="DacPermissionResolution.ContextValid"/> = <c>false</c>).
    /// </returns>
    Task<DacPermissionResolution> ResolveAsync(
        DacPermissionQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks a single permission at the layer selected by the query. Fails closed
    ///     (returns <c>false</c>) for invalid queries or absent permissions.
    /// </summary>
    /// <param name="query">The DAC query (user + tenant required; content-type/resource optional).</param>
    /// <param name="permission">The permission name to check (unqualified, e.g. <c>Read</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the permission is granted at the queried layer.</returns>
    Task<bool> HasPermissionAsync(
        DacPermissionQuery query,
        string permission,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     A centralized permission-resolution query for the 3-layer DAC model (issue #339).
/// </summary>
/// <remarks>
///     <para>
///         <b>Layer selection.</b> <see cref="Layer"/> defaults to
///         <see cref="PermissionLayer.Auto"/>, which grants a permission when any
///         applicable layer grants it (tenant name, content-type qualified name, resource
///         qualified name — evaluated through one canonical engine pass). Explicit layers
///         check only that layer's qualified name and require the layer's scope to be
///         present on the query.
///     </para>
///     <para>
///         <b>Resource context.</b> When a complete <see cref="ResourceType"/> +
///         <see cref="ResourceId"/> pair is present, resolution uses the canonical
///         resource context so resource grants for exactly that resource contribute;
///         resource grants never leak into tenant-wide results (context isolation).
///     </para>
/// </remarks>
public sealed record DacPermissionQuery
{
    /// <summary>Required: the user whose permissions are resolved. <see cref="Guid.Empty"/> is invalid.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Required: the tenant scope. <see cref="Guid.Empty"/> is invalid (fail-closed).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    ///     The DAC layer to evaluate. Defaults to <see cref="PermissionLayer.Auto"/>
    ///     (any applicable layer may grant).
    /// </summary>
    public PermissionLayer Layer { get; init; } = PermissionLayer.Auto;

    /// <summary>Optional: content-type name for the content-type layer (e.g. <c>Course</c>).</summary>
    public string? ContentType { get; init; }

    /// <summary>Optional: resource type for the resource layer (must be paired with <see cref="ResourceId"/>).</summary>
    public string? ResourceType { get; init; }

    /// <summary>Optional: resource id for the resource layer (must be paired with <see cref="ResourceType"/>).</summary>
    public string? ResourceId { get; init; }

    /// <summary>True when a complete resource (type, id) pair is present.</summary>
    public bool HasResource =>
        !string.IsNullOrWhiteSpace(ResourceType) && !string.IsNullOrWhiteSpace(ResourceId);

    /// <summary>
    ///     True when the query is safe to resolve: non-empty user and tenant, no
    ///     half-specified resource pair, and every explicitly selected layer carries its
    ///     required scope.
    /// </summary>
    public bool IsValid =>
        UserId != Guid.Empty
        && TenantId != Guid.Empty
        && (HasResource || (string.IsNullOrWhiteSpace(ResourceType) && string.IsNullOrWhiteSpace(ResourceId)))
        && (Layer != PermissionLayer.ContentType || !string.IsNullOrWhiteSpace(ContentType))
        && (Layer != PermissionLayer.Resource || HasResource);
}

/// <summary>
///     Result of a centralized DAC permission resolution (issue #339): the canonical
///     effective permissions plus layer-aware helpers.
/// </summary>
public sealed record DacPermissionResolution
{
    /// <summary>The query this resolution was computed for.</summary>
    public required DacPermissionQuery Query { get; init; }

    /// <summary>
    ///     The canonical effective permissions (<see cref="IEffectivePermissionResolver"/>,
    ///     DENY-WINS) resolved at the most specific applicable layer. Invalid queries
    ///     resolve to an empty fail-closed result.
    /// </summary>
    public required EffectivePermissions Effective { get; init; }

    /// <summary>False when the query was invalid and the result was fail-closed.</summary>
    public bool ContextValid => Effective.ContextValid;

    /// <summary>True when the evaluation-layer denial throttle short-circuited the resolution.</summary>
    public bool Throttled => Effective.Throttled;

    /// <summary>
    ///     Checks a permission with the layer semantics of the underlying query
    ///     (layer-qualified names for content-type/resource; raw name for tenant;
    ///     any-applicable-layer for <see cref="PermissionLayer.Auto"/>).
    /// </summary>
    /// <param name="permission">The unqualified permission name (e.g. <c>Read</c>).</param>
    /// <returns>True when the permission is granted.</returns>
    public bool Grants(string permission) => DacPermissionResolver.Grants(Query, Effective, permission);
}
