using System.Reflection;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace GameGuild.Identity.Authorization.GraphQL;

/// <summary>
///     Composition mode for the permission list of a <c>@authorize</c> directive
///     application: every listed permission (<see cref="All"/>, AND logic) or at least
///     one of them (<see cref="Any"/>, OR logic).
/// </summary>
public enum GraphQLAuthorizationMode
{
    /// <summary>All listed permissions are required (AND).</summary>
    All = 0,

    /// <summary>At least one listed permission is required (OR).</summary>
    Any = 1,
}

/// <summary>
///     Schema metadata for the general GraphQL <c>@authorize</c> directive (issue #335):
///     the requirement set enforced for a field by the GraphQL authorization framework.
///     The actor and tenant are always taken from the trusted request context; they are
///     never accepted from GraphQL input.
/// </summary>
/// <param name="Permissions">
///     Typed permission keys (<see cref="PermissionType"/> names). Composed with
///     <paramref name="Mode"/> inside one application.
/// </param>
/// <param name="Policy">
///     Optional ASP.NET Core authorization policy name (role/policy-based variant).
/// </param>
/// <param name="Roles">
///     Optional role claims; the caller must hold at least one of them (role-based variant).
/// </param>
/// <param name="Mode">How <paramref name="Permissions"/> compose (AND or OR).</param>
/// <param name="MaskUnauthorized">
///     When true, a denied field resolves to null instead of producing a GraphQL error.
/// </param>
/// <param name="ResourceType">
///     Optional DAC resource type. When set (with a resource id), permission checks run
///     resource-scoped — which also activates the owner-override and temporal-precedence
///     behavior of the effective-permission engine (issues #372/#363).
/// </param>
/// <param name="ContentType">
///     Optional DAC content type (issues #360/#365/#320). When set (without a resource
///     pair), permission checks run content-type scoped: the engine's content-type grant
///     layer contributes its content-type qualified names, and tenant-layer grants still
///     satisfy the check (Auto inheritance).
/// </param>
/// <param name="ResourceIdArgumentName">
///     Dotted argument path holding the resource id, for example <c>input.projectId</c>.
/// </param>
/// <param name="ResourceIdParentPropertyName">
///     Property path on the parent resolver value holding the resource id, for nested fields.
/// </param>
public sealed record GraphQLAuthorizeDirective(
    string[] Permissions,
    string? Policy,
    string[] Roles,
    GraphQLAuthorizationMode Mode,
    bool MaskUnauthorized,
    string? ResourceType,
    string? ResourceIdArgumentName,
    string? ResourceIdParentPropertyName,
    string? ContentType = null)
{
    /// <summary>The directive name as published in the GraphQL schema.</summary>
    public const string DirectiveName = "authorize";

    /// <summary>Convenience factory for the default (tenant-scoped, AND-mode) rule.</summary>
    public static GraphQLAuthorizeDirective ForPermissions(params string[] permissions)
        => new(permissions, null, [], GraphQLAuthorizationMode.All, false, null, null, null);

    /// <summary>
    ///     Convenience factory for a content-type scoped rule (issues #360/#365): the
    ///     permission keys are checked against the engine's content-type qualified names.
    /// </summary>
    public static GraphQLAuthorizeDirective ForContentType(string contentType, params string[] permissions)
        => new(permissions, null, [], GraphQLAuthorizationMode.All, false, null, null, null, contentType);
}

/// <summary>
///     The general <c>@authorize</c> directive type: repeatable, applicable to field
///     definitions, mapped to the platform permission model. Enforcement is provided by
///     the host's GraphQL authorization field middleware (deny-by-default for protected
///     fields); applying the directive alone is the declaration of protection.
/// </summary>
public sealed class GraphQLAuthorizeDirectiveType : DirectiveType<GraphQLAuthorizeDirective>
{
    protected override void Configure(IDirectiveTypeDescriptor<GraphQLAuthorizeDirective> descriptor)
    {
        descriptor.Name(GraphQLAuthorizeDirective.DirectiveName);
        descriptor.Description(
            "Requires the authenticated actor to satisfy the declared permission, policy or role " +
            "requirements. Multiple applications on one field all apply (deny wins).");
        descriptor.Location(DirectiveLocation.FieldDefinition);
        descriptor.Repeatable();
        descriptor.Argument(directive => directive.Permissions)
            .Description("Typed permission keys; all (default) or any, per 'mode'.")
            .Type<ListType<NonNullType<StringType>>>();
        descriptor.Argument(directive => directive.Policy)
            .Description("ASP.NET Core authorization policy the caller must satisfy.")
            .Type<StringType>();
        descriptor.Argument(directive => directive.Roles)
            .Description("Role claims; the caller must hold at least one.")
            .Type<ListType<NonNullType<StringType>>>();
        descriptor.Argument(directive => directive.Mode)
            .Description("How the listed permissions compose: all (AND) or any (OR).")
            .Type<EnumType<GraphQLAuthorizationMode>>()
            .DefaultValue(GraphQLAuthorizationMode.All);
        descriptor.Argument(directive => directive.MaskUnauthorized)
            .Description("Resolve denied fields to null instead of a GraphQL error.")
            .Type<BooleanType>()
            .DefaultValue(false);
        descriptor.Argument(directive => directive.ResourceType)
            .Description("DAC resource type for resource-scoped checks (activates owner override).")
            .Type<StringType>();
        descriptor.Argument(directive => directive.ResourceIdArgumentName)
            .Description("Dotted argument path holding the resource id, e.g. 'input.projectId'.")
            .Type<StringType>();
        descriptor.Argument(directive => directive.ResourceIdParentPropertyName)
            .Description("Parent-value property path holding the resource id, for nested fields.")
            .Type<StringType>();
        descriptor.Argument(directive => directive.ContentType)
            .Description("DAC content type for content-type scoped checks (engine content-type grant layer).")
            .Type<StringType>();
    }
}

/// <summary>
///     Declares the general <c>@authorize</c> contract on a GraphQL resolver. Requirements
///     are enforced by the host's GraphQL authorization field middleware before the
///     resolver runs (deny-by-default). Usable from any module that references the
///     authorization module.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class GraphQLAuthorizeAttribute : ObjectFieldDescriptorAttribute
{
    private readonly PermissionType[] _permissions;

    /// <summary>
    ///     Permission-based variant: zero or more typed permission keys. A rule must
    ///     ultimately declare at least one requirement (permissions, policy or roles);
    ///     otherwise schema compilation fails (fail-closed).
    /// </summary>
    public GraphQLAuthorizeAttribute(params PermissionType[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions = permissions.Distinct().ToArray();
    }

    /// <summary>Policy-based variant: an ASP.NET Core authorization policy name.</summary>
    public string? Policy { get; set; }

    /// <summary>Role-based variant: the caller must hold at least one of these roles.</summary>
    public string[] Roles { get; set; } = [];

    /// <summary>How the listed permissions compose: all (AND, default) or any (OR).</summary>
    public GraphQLAuthorizationMode Mode { get; set; } = GraphQLAuthorizationMode.All;

    /// <summary>Resolve denied fields to null instead of a GraphQL error.</summary>
    public bool MaskUnauthorized { get; set; }

    /// <summary>
    ///     DAC resource type for resource-scoped permission checks. Resource-scoped
    ///     checks run through the effective-permission engine, including owner-override
    ///     and expiration-aware precedence (issues #372/#363).
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>Dotted argument path holding the resource id, for example <c>input.projectId</c>.</summary>
    public string ResourceIdArgumentName { get; set; } = "id";

    /// <summary>Parent-value property path holding the resource id, for nested fields.</summary>
    public string? ResourceIdParentPropertyName { get; set; }

    /// <summary>
    ///     DAC content type for content-type scoped permission checks (issues
    ///     #360/#365/#320). When set (and no resource pair is declared), checks run
    ///     through the engine's content-type grant layer.
    /// </summary>
    public string? ContentType { get; set; }

    protected override void OnConfigure(
        IDescriptorContext context,
        IObjectFieldDescriptor descriptor,
        MemberInfo member)
    {
        if (_permissions.Length == 0 && Policy is null && Roles.Length == 0)
        {
            throw new InvalidOperationException(
                "A GraphQL authorize rule must require a permission, a policy or a role.");
        }

        if (Policy is not null && string.IsNullOrWhiteSpace(Policy))
        {
            throw new InvalidOperationException(
                "A GraphQL authorize policy requirement must name a policy.");
        }

        if (_permissions.Any(permission => !Enum.IsDefined(permission)))
        {
            throw new InvalidOperationException(
                "A GraphQL authorize rule must use valid typed permission keys.");
        }

        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException(
                "A GraphQL authorize rule must use a valid permission composition mode.");
        }

        if (Roles.Any(role => string.IsNullOrWhiteSpace(role)))
        {
            throw new InvalidOperationException(
                "A GraphQL authorize role requirement must use non-empty role names.");
        }

        if (ResourceType is not null && string.IsNullOrWhiteSpace(ResourceType))
        {
            throw new InvalidOperationException(
                "A GraphQL authorize rule must name a non-empty resource type.");
        }

        var bindsResourceId = ResourceIdParentPropertyName is not null
            || !string.IsNullOrWhiteSpace(ResourceIdArgumentName);
        if (ResourceType is not null && _permissions.Length > 0 && !bindsResourceId)
        {
            throw new InvalidOperationException(
                "A resource-scoped GraphQL authorize rule must identify the resource id " +
                "argument or parent property.");
        }

        if (ResourceIdParentPropertyName is not null && string.IsNullOrWhiteSpace(ResourceIdParentPropertyName))
        {
            throw new InvalidOperationException(
                "A GraphQL authorize rule must name a non-empty parent resource id property.");
        }

        if (ContentType is not null
            && (string.IsNullOrWhiteSpace(ContentType) || ResourceType is not null))
        {
            // A content-type scoped rule names exactly one scope: a blank name is invalid
            // and the resource-scoped variant already covers resource contexts.
            throw new InvalidOperationException(
                "A content-type scoped GraphQL authorize rule must name a non-empty content type " +
                "and cannot also declare a resource scope.");
        }

        descriptor.Directive(new GraphQLAuthorizeDirective(
            _permissions.Select(permission => permission.ToString()).ToArray(),
            string.IsNullOrWhiteSpace(Policy) ? null : Policy,
            Roles.Where(role => !string.IsNullOrWhiteSpace(role)).Distinct().ToArray(),
            Mode,
            MaskUnauthorized,
            string.IsNullOrWhiteSpace(ResourceType) ? null : ResourceType,
            ResourceIdParentPropertyName is null && bindsResourceId ? ResourceIdArgumentName : null,
            ResourceIdParentPropertyName,
            string.IsNullOrWhiteSpace(ContentType) ? null : ContentType));
    }
}
