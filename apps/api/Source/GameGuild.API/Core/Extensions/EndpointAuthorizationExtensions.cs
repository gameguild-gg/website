using GameGuild.Identity.Authorization;

namespace GameGuild.API;

/// <summary>
///     Extension methods for applying permission requirements to endpoints
/// </summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    ///     Adds a permission requirement to the endpoint.
    ///     The endpoint will only be accessible to authenticated users with the specified permission.
    /// </summary>
    /// <param name="builder">The endpoint convention builder</param>
    /// <param name="permissionName">The permission name required (e.g., "Roles.Read", "Users.Write")</param>
    /// <returns>The endpoint convention builder for chaining</returns>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionName) where TBuilder : IEndpointConventionBuilder
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            throw new ArgumentException("Permission name cannot be null or whitespace.", nameof(permissionName));
        }

        builder.Add(endpointBuilder => { endpointBuilder.Metadata.Add(new RequiresPermissionAttribute(permissionName)); });

        return builder;
    }

    /// <summary>
    ///     Adds multiple permission requirements to the endpoint.
    ///     The endpoint will only be accessible if the user has ALL specified permissions.
    /// </summary>
    /// <param name="builder">The endpoint convention builder</param>
    /// <param name="permissionNames">The permission names required</param>
    /// <returns>The endpoint convention builder for chaining</returns>
    public static TBuilder RequirePermissions<TBuilder>(this TBuilder builder, params string[ ] permissionNames) where TBuilder : IEndpointConventionBuilder
    {
        if (permissionNames == null || permissionNames.Length == 0)
        {
            throw new ArgumentException("At least one permission name must be specified.", nameof(permissionNames));
        }

        builder.Add(endpointBuilder =>
            {
                foreach (var permissionName in permissionNames)
                {
                    if (!string.IsNullOrWhiteSpace(permissionName)) { endpointBuilder.Metadata.Add(new RequiresPermissionAttribute(permissionName)); }
                }
            }
        );

        return builder;
    }
}
