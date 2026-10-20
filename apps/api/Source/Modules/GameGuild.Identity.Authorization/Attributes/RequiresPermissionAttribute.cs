namespace GameGuild.Identity.Authorization;

/// <summary>
///     Specifies that the action method or endpoint requires a specific permission.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresPermissionAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of <see cref="RequiresPermissionAttribute"/>.
    /// </summary>
    /// <param name="permissionName">The permission name required.</param>
    public RequiresPermissionAttribute(string permissionName)
    {
        PermissionName = permissionName ?? throw new ArgumentNullException(nameof(permissionName));
    }

    /// <summary>
    ///     Gets the required permission name.
    /// </summary>
    public string PermissionName { get; }
}

/// <summary>
///     Alias for RequiresPermissionAttribute for backward compatibility.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of <see cref="RequirePermissionAttribute"/>.
    /// </summary>
    /// <param name="permissionName">The permission name required.</param>
    public RequirePermissionAttribute(string permissionName)
    {
        PermissionName = permissionName ?? throw new ArgumentNullException(nameof(permissionName));
    }

    /// <summary>
    ///     Gets the required permission name.
    /// </summary>
    public string PermissionName { get; }
}

/// <summary>
///     Specifies that the action method requires a specific permission on a resource.
///     This is a generic attribute that allows type-safe resource permission checks.
/// </summary>
/// <typeparam name="TPermission">The permission enum type for the resource.</typeparam>
/// <typeparam name="TResource">The resource entity type.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireResourcePermissionAttribute<TPermission, TResource> : Attribute, IResourcePermissionMarker
    where TPermission : struct, Enum
    where TResource : class
{
    /// <summary>
    ///     Initializes a new instance of <see cref="RequireResourcePermissionAttribute{TPermission, TResource}"/>.
    /// </summary>
    /// <param name="requiredPermission">The permission required on the resource.</param>
    /// <param name="resourceIdParameterName">The name of the route/query parameter containing the resource ID.</param>
    public RequireResourcePermissionAttribute(TPermission requiredPermission, string resourceIdParameterName = "id")
    {
        RequiredPermission = requiredPermission;
        ResourceIdParameterName = resourceIdParameterName ?? throw new ArgumentNullException(nameof(resourceIdParameterName));
    }

    /// <summary>
    ///     Gets the required permission.
    /// </summary>
    public TPermission RequiredPermission { get; }

    /// <summary>
    ///     Gets the name of the parameter containing the resource ID.
    /// </summary>
    public string ResourceIdParameterName { get; }

    /// <summary>
    ///     Gets the resource type.
    /// </summary>
    public Type ResourceType => typeof(TResource);

    /// <summary>
    ///     Gets the permission type.
    /// </summary>
    public Type PermissionEnumType => typeof(TPermission);

    // IResourcePermissionMarker implementation
    object IResourcePermissionMarker.RequiredPermission => RequiredPermission;
    string IResourcePermissionMarker.ResourceIdParameterName => ResourceIdParameterName;
}

/// <summary>
///     Single-generic resource DAC requirement (issue #321):
///     <c>RequireResourcePermissionAttribute&lt;T&gt;</c> enforces resource-level permissions
///     with route extraction. The resource type comes from <typeparamref name="TResource"/>;
///     the required permissions are names (AND logic). Enforcement runs through
///     <see cref="IDacPermissionResolver"/> with a resource context, which also activates
///     the owner-override and temporal-precedence behavior (issues #372/#363).
/// </summary>
/// <typeparam name="TResource">The resource entity type.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireResourcePermissionAttribute<TResource> : DacPermissionAttributeBase
    where TResource : class
{
    /// <summary>
    ///     Initializes the requirement for <typeparamref name="TResource"/>.
    /// </summary>
    /// <param name="permissions">The required permission names (AND logic).</param>
    /// <param name="resourceIdParameterName">The parameter holding the resource id (defaults to <c>id</c>).</param>
    public RequireResourcePermissionAttribute(string[] permissions, string resourceIdParameterName = "id")
        : base(PermissionLayer.Resource, permissions)
    {
        ContentType = typeof(TResource).Name;
        ResourceIdParameterName = resourceIdParameterName;
    }
}

/// <summary>
///     Specifies that the action method requires a specific permission on a content type.
///     Content-type level permissions apply to all instances of the content type within a tenant.
/// </summary>
/// <typeparam name="TResource">The content type entity.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireContentTypePermissionAttribute<TResource> : Attribute, IContentTypePermissionMarker
    where TResource : class
{
    public RequireContentTypePermissionAttribute(object permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }

    public object Permission { get; }
    public Type ResourceType => typeof(TResource);
}

/// <summary>
///     Specifies that the action method requires a specific tenant-level permission.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireTenantPermissionAttribute : Attribute
{
    public RequireTenantPermissionAttribute(object permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }

    public object Permission { get; }
}

/// <summary>
///     Marker interface for resource permission attributes to enable runtime discovery.
/// </summary>
public interface IResourcePermissionMarker
{
    object RequiredPermission { get; }
    string ResourceIdParameterName { get; }
    Type ResourceType { get; }
    Type PermissionEnumType { get; }
}

/// <summary>
///     Marker interface for content-type permission attributes.
/// </summary>
public interface IContentTypePermissionMarker
{
    object Permission { get; }
    Type ResourceType { get; }
}

// NOTE: AuthorizeRequestAttribute is defined in Behaviors/AuthorizationBehavior.cs
// for CQRS commands/queries authorization.
