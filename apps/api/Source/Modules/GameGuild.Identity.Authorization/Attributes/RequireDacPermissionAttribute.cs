using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Where a <c>RequireResourcePermission</c>-style attribute binds its resource id from
/// (issue #321: flexible parameter binding from multiple sources).
/// </summary>
public enum DacResourceIdSource
{
    /// <summary>Route value first, then query string (the default, backward compatible).</summary>
    RouteOrQuery = 0,

    /// <summary>Route value only.</summary>
    Route = 1,

    /// <summary>Query string only.</summary>
    Query = 2,

    /// <summary>Request header only.</summary>
    Header = 3,

    /// <summary>Claims principal claim of the same name.</summary>
    Claims = 4
}

/// <summary>
///     The declarative contract of the DAC permission-attribute family (issues #318/#320/#321).
///     Implemented by every <c>Require*</c> DAC attribute and by the extensible base
///     <see cref="DacPermissionAttributeBase"/>, so custom attributes plug into the same
///     <c>ResourcePermissionAuthorizationFilter</c> enforcement path without new filter
///     code. Enforcement always runs through <see cref="IDacPermissionResolver"/> and the
///     canonical effective-permission engine (DENY-WINS, deny-by-default, fail-closed).
/// </summary>
public interface IDacPermissionAttribute
{
    /// <summary>
    ///     The DAC layer the requirement is declared at. <see cref="PermissionLayer.Auto"/>
    ///     grants when any applicable layer grants; explicit layers check only their own
    ///     layer-qualified name.
    /// </summary>
    PermissionLayer Layer { get; }

    /// <summary>
    ///     The required permission names (unqualified, e.g. <c>Read</c>). Multiple entries
    ///     compose with AND logic.
    /// </summary>
    string[] RequiredPermissions { get; }

    /// <summary>
    ///     The content-type name for <see cref="PermissionLayer.ContentType"/> requirements
    ///     (or the resource type name for resource requirements).
    /// </summary>
    string? ContentType { get; }

    /// <summary>
    ///     The name of the route/query/header/claim parameter holding the resource id for
    ///     <see cref="PermissionLayer.Resource"/> requirements.
    /// </summary>
    string? ResourceIdParameterName { get; }

    /// <summary>
    ///     Where <see cref="ResourceIdParameterName"/> is read from.
    /// </summary>
    DacResourceIdSource ResourceIdSource { get; }
}

/// <summary>
///     Extensible base class for declarative DAC permission attributes (issues #320/#321:
///     custom permission validation through an extensible attribute framework). Derived
///     attributes declare their layer, permissions and scope; the shared
///     <c>ResourcePermissionAuthorizationFilter</c> enforces them through
///     <see cref="IDacPermissionResolver"/>. Override <see cref="ShouldApply"/> for
///     conditional application based on runtime context (fail-closed: the default applies
///     the requirement always).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "S3376",
    Justification = "Base-class name clarity: the suffix AttributeBase distinguishes the extensible abstract parent from the concrete Require* attribute family derived from it.")]
public abstract class DacPermissionAttributeBase : Attribute, IDacPermissionAttribute
{
    /// <summary>Initializes the base with the requirement set.</summary>
    /// <param name="layer">The DAC layer of the requirement.</param>
    /// <param name="requiredPermissions">
    ///     At least one non-blank permission name (validated; AND logic).
    /// </param>
    protected DacPermissionAttributeBase(PermissionLayer layer, params string[] requiredPermissions)
    {
        if (requiredPermissions is not { Length: > 0 } || requiredPermissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "A DAC permission attribute must declare at least one non-blank permission.",
                nameof(requiredPermissions));
        }

        if (!Enum.IsDefined(layer))
        {
            throw new ArgumentException("A DAC permission attribute must declare a known layer.", nameof(layer));
        }

        Layer = layer;
        RequiredPermissions = requiredPermissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public PermissionLayer Layer { get; }

    /// <inheritdoc />
    public string[] RequiredPermissions { get; protected set; }

    /// <inheritdoc />
    public string? ContentType { get; protected set; }

    /// <inheritdoc />
    public string? ResourceIdParameterName { get; protected set; }

    /// <inheritdoc />
    public DacResourceIdSource ResourceIdSource { get; protected set; } = DacResourceIdSource.RouteOrQuery;

    /// <summary>
    ///     Validates the composed requirement (called at enforcement time; attribute
    ///     constructors cannot access runtime scope). Throws
    ///     <see cref="InvalidOperationException"/> for an incomplete declaration — an
    ///     invalid attribute fails closed (the request is denied), never skipped.
    /// </summary>
    public virtual void Validate()
    {
        if (Layer == PermissionLayer.ContentType && string.IsNullOrWhiteSpace(ContentType))
        {
            throw new InvalidOperationException(
                "A content-type DAC permission attribute must declare its content type.");
        }

        if (Layer == PermissionLayer.Resource)
        {
            if (string.IsNullOrWhiteSpace(ContentType))
            {
                throw new InvalidOperationException(
                    "A resource DAC permission attribute must declare its resource type.");
            }

            if (string.IsNullOrWhiteSpace(ResourceIdParameterName))
            {
                throw new InvalidOperationException(
                    "A resource DAC permission attribute must declare its resource id parameter name.");
            }

            if (!Enum.IsDefined(ResourceIdSource))
            {
                throw new InvalidOperationException(
                    "A resource DAC permission attribute must declare a known resource id source.");
            }
        }
    }

    /// <summary>
    ///     Conditional application (issue #321): when this returns false the attribute does
    ///     not contribute its requirement for the given request. Fail-closed default:
    ///     always applies. Deriving classes must never widen access from this hook — only
    ///     narrow it is impossible (an attribute that does not apply removes a requirement,
    ///     so overrides must themselves be guarded by an independent, stricter check).
    /// </summary>
    /// <param name="httpContext">The current request context.</param>
    public virtual bool ShouldApply(HttpContext httpContext) => true;
}

/// <summary>
///     Requires a tenant-level DAC permission (issue #320/#321): the authenticated actor
///     must hold every listed permission at the tenant layer. Enforced by the
///     <c>ResourcePermissionAuthorizationFilter</c> through <see cref="IDacPermissionResolver"/>,
///     so tenant-layer grants (roles, defaults, direct grants, JIT elevation) satisfy the
///     requirement through one canonical engine pass.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireDacPermissionAttribute : DacPermissionAttributeBase
{
    /// <summary>
    ///     Initializes the requirement with one or more tenant permission names (AND logic).
    /// </summary>
    /// <param name="permissions">The required permission names (e.g. <c>content:read</c>).</param>
    public RequireDacPermissionAttribute(params string[] permissions)
        : base(PermissionLayer.Tenant, permissions)
    {
    }
}

/// <summary>
///     Requires a content-type permission on the decorated action for the named resource
///     type (issues #320/#321). The check resolves through the engine's content-type layer:
///     a content-type grant for the type grants it, a tenant-layer grant satisfies it
///     under <see cref="PermissionLayer.Auto"/> inheritance, and a content-type deny
///     subtracts it (DENY-WINS).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireContentTypeDacPermissionAttribute : DacPermissionAttributeBase
{
    /// <summary>Initializes the requirement for the resource type acting as content type.</summary>
    /// <param name="resourceType">The content/resource type (e.g. <c>typeof(Project)</c>).</param>
    /// <param name="permissions">The required permission names (AND logic).</param>
    public RequireContentTypeDacPermissionAttribute(Type resourceType, params string[] permissions)
        : base(PermissionLayer.ContentType, permissions)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        ContentType = resourceType.Name;
    }
}

/// <summary>
///     Requires a resource-level permission on the decorated action (issues #320/#321):
///     the resource id is extracted from the declared parameter (route, query, header or
///     claims) and the check resolves through the engine's resource context — which also
///     activates the owner-override and temporal-precedence behavior (issues #372/#363).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireResourceDacPermissionAttribute : DacPermissionAttributeBase
{
    /// <summary>Initializes the requirement with route-or-query id binding.</summary>
    /// <param name="resourceType">The resource type (e.g. <c>typeof(Project)</c>).</param>
    /// <param name="resourceIdParameterName">The parameter holding the resource id.</param>
    /// <param name="permissions">The required permission names (AND logic).</param>
    public RequireResourceDacPermissionAttribute(
        Type resourceType,
        string resourceIdParameterName,
        params string[] permissions)
        : this(resourceType, resourceIdParameterName, DacResourceIdSource.RouteOrQuery, permissions)
    {
    }

    /// <summary>Initializes the requirement with an explicit id binding source.</summary>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="resourceIdParameterName">The parameter holding the resource id.</param>
    /// <param name="idSource">Where the resource id is read from.</param>
    /// <param name="permissions">The required permission names (AND logic).</param>
    public RequireResourceDacPermissionAttribute(
        Type resourceType,
        string resourceIdParameterName,
        DacResourceIdSource idSource,
        params string[] permissions)
        : base(PermissionLayer.Resource, permissions)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        ContentType = resourceType.Name;
        ResourceIdParameterName = string.IsNullOrWhiteSpace(resourceIdParameterName)
            ? throw new ArgumentException("The resource id parameter name is required.", nameof(resourceIdParameterName))
            : resourceIdParameterName;
        ResourceIdSource = Enum.IsDefined(idSource)
            ? idSource
            : throw new ArgumentException("The resource id source is unknown.", nameof(idSource));
    }
}
