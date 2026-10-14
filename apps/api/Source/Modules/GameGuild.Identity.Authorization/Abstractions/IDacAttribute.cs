namespace GameGuild.Identity.Authorization;

/// <summary>
///     How a DAC attribute's permission set flows through the 3-layer model
///     (tenant → content-type → resource). Issues #318/#320.
/// </summary>
public enum DacInheritancePolicy
{
    /// <summary>
    ///     The attribute's permissions apply only to the exact scope they are recorded on;
    ///     nothing flows to other layers.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The attribute's permissions are inherited from a broader (parent) scope — for
    ///     example a tenant-layer grant satisfying a content-type or resource-layer
    ///     <c>Auto</c> check.
    /// </summary>
    InheritFromParent = 1,

    /// <summary>
    ///     The attribute's permissions propagate down to narrower (child) scopes — for
    ///     example a content-type grant applying to every resource of that type.
    /// </summary>
    PropagateToChildren = 2,

    /// <summary>
    ///     The attribute's permissions flow in both directions as far as the
    ///     effective-permission engine's scoping rules allow — the owner-override pattern,
    ///     where the owner's set is honored at tenant parity and on the exact resource.
    /// </summary>
    InheritAndPropagate = 3
}

/// <summary>
///     Standardized attribute contract for the Discretionary Access Control (DAC)
///     model (issues #318/#320). Implemented by permission-grant entities and attribute
///     carriers so every DAC surface can be inspected through one shape: who owns the
///     access decision, which group co-governs it, which permission entries it carries and
///     how those entries inherit across the 3-layer model.
/// </summary>
/// <remarks>
///     <para>
///         <b>Implementations.</b> <see cref="ResourceUserPermission"/> (resource-scoped
///         grants; the owner row reports <see cref="DacInheritancePolicy.InheritAndPropagate"/>
///         because the engine's owner override honors the owner's set at tenant parity and
///         on the exact resource) and <see cref="ContentTypePermissionGrant"/> (content-type
///         grants report <see cref="DacInheritancePolicy.PropagateToChildren"/> because the
///         engine's content-type layer applies them to every resource of the type).
///         <see cref="DacAttributeInfo"/> is a concrete carrier for ad-hoc composition.
///     </para>
///     <para>
///         <b>Read-only view.</b> The interface is a projection over stored grants; it is
///         never an authorization input by itself. Every decision still runs through the
///         canonical effective-permission engine (DENY-WINS, deny-by-default, fail-closed).
///     </para>
/// </remarks>
public interface IDacAttribute
{
    /// <summary>
    ///     The user the attribute's access decision belongs to (the DAC owner of this
    ///     entry). Null for scope defaults that apply to every user of the tenant.
    /// </summary>
    Guid? Owner { get; }

    /// <summary>
    ///     Optional co-governing owner group (group-scoped DAC). Null when the entry is not
    ///     group-governed.
    /// </summary>
    string? OwnerGroup { get; }

    /// <summary>
    ///     The permission entries the attribute carries (e.g. ["Read", "Write"]).
    /// </summary>
    IReadOnlyList<string> PermissionList { get; }

    /// <summary>
    ///     How <see cref="PermissionList"/> inherits across the 3-layer model.
    /// </summary>
    DacInheritancePolicy InheritancePolicy { get; }
}

/// <summary>
///     Concrete, composable <see cref="IDacAttribute"/> carrier (issues #318/#320): a
///     value-shaped DAC attribute for tests, tooling and callers that need to describe a
///     DAC entry without a stored grant entity.
/// </summary>
/// <param name="Owner">The DAC owner (null for scope defaults).</param>
/// <param name="OwnerGroup">Optional co-governing owner group.</param>
/// <param name="PermissionList">The permission entries.</param>
/// <param name="InheritancePolicy">How the entries inherit across layers.</param>
public sealed record DacAttributeInfo(
    Guid? Owner,
    string? OwnerGroup,
    IReadOnlyList<string> PermissionList,
    DacInheritancePolicy InheritancePolicy) : IDacAttribute
{
    /// <summary>
    ///     Creates an attribute with no owner-group and no cross-layer inheritance.
    /// </summary>
    public static DacAttributeInfo ForOwner(Guid owner, params string[] permissions)
        => new(owner, null, permissions, DacInheritancePolicy.None);
}
