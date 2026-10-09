using System.Collections.Frozen;
using System.Reflection;
using GameGuild.Identity.Authorization.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Central registry of all known permissions in the system.
///     Provides validation, discovery, and documentation of all permission scopes.
/// </summary>
/// <remarks>
///     <para>
///         This registry automatically discovers all permission types at startup by scanning
///         for classes that inherit from <see cref="Permission"/>. This provides a single source
///         of truth for all permissions without requiring manual registration.
///     </para>
///     <para>
///         <b>Adding a New Permission Scope:</b>
///     </para>
///     <list type="number">
///         <item>Create a new class inheriting from <see cref="Permission"/> (e.g., <c>MyFeaturePermission</c>)</item>
///         <item>Add a nested <c>Keys</c> class with string constants</item>
///         <item>Add static readonly permission instances</item>
///         <item>The registry will automatically discover them on next startup</item>
///     </list>
///     <para>
///         Example:
///     </para>
///     <code>
///     public sealed class MyFeaturePermission : Permission
///     {
///         public static class Keys
///         {
///             public const string Read = "myfeature:read";
///             public const string Write = "myfeature:write";
///         }
///         
///         public static readonly MyFeaturePermission Read = new(Keys.Read, "Read my feature data");
///         public static readonly MyFeaturePermission Write = new(Keys.Write, "Write my feature data");
///     }
///     </code>
/// </remarks>
public static class PermissionRegistry
{
    private static readonly Lazy<FrozenDictionary<string, Permission>> AllPermissions = new(DiscoverAllPermissions);
    private static readonly Lazy<FrozenSet<string>> AllKeys = new(() => AllPermissions.Value.Keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
    private static readonly Lazy<IReadOnlyList<PermissionScope>> AllScopes = new(DiscoverAllScopes);

    /// <summary>
    ///     Gets all registered permission keys.
    /// </summary>
    public static IReadOnlyCollection<string> Keys => AllKeys.Value;

    /// <summary>
    ///     Gets all registered permissions with their metadata.
    /// </summary>
    public static IReadOnlyDictionary<string, Permission> Permissions => AllPermissions.Value;

    /// <summary>
    ///     Gets all permission scopes (grouped by resource).
    /// </summary>
    public static IReadOnlyList<PermissionScope> Scopes => AllScopes.Value;

    /// <summary>
    ///     Checks if a permission key is valid (registered in the system).
    /// </summary>
    /// <param name="key">The permission key to validate.</param>
    /// <returns>True if the permission is registered; otherwise, false.</returns>
    public static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        // Check exact match
        if (AllKeys.Value.Contains(key))
        {
            return true;
        }

        // Check if it's a wildcard that matches a registered resource
        if (key.EndsWith(":*"))
        {
            var resource = key[..^2];
            return AllPermissions.Value.Values.Any(p => p.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    /// <summary>
    ///     Gets a permission by its key.
    /// </summary>
    /// <param name="key">The permission key.</param>
    /// <returns>The permission if found; otherwise, null.</returns>
    public static Permission? GetByKey(string key)
    {
        return AllPermissions.Value.TryGetValue(key, out var permission) ? permission : null;
    }

    /// <summary>
    ///     Gets all permissions for a specific resource.
    /// </summary>
    /// <param name="resource">The resource name (e.g., "users", "content").</param>
    /// <returns>All permissions for the specified resource.</returns>
    public static IEnumerable<Permission> GetByResource(string resource)
    {
        return AllPermissions.Value.Values.Where(p => 
            p.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Validates a set of permission keys and returns any that are invalid.
    /// </summary>
    /// <param name="keys">The permission keys to validate.</param>
    /// <returns>A list of invalid keys, or empty if all are valid.</returns>
    public static IReadOnlyList<string> ValidateKeys(IEnumerable<string> keys)
    {
        return keys.Where(k => !IsValidKey(k)).ToList();
    }

    private static FrozenDictionary<string, Permission> DiscoverAllPermissions()
    {
        var permissions = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);
        
        // Find all types that inherit from Permission
        var permissionTypes = typeof(Permission).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Permission)));

        foreach (var type in permissionTypes)
        {
            // Find all static readonly Permission fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsInitOnly && typeof(Permission).IsAssignableFrom(f.FieldType));

            foreach (var field in fields)
            {
                if (field.GetValue(null) is Permission permission)
                {
                    permissions[permission.Key] = permission;
                }
            }
        }

        return permissions.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PermissionScope> DiscoverAllScopes()
    {
        return AllPermissions.Value.Values
            .GroupBy(p => p.Resource, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PermissionScope(
                Resource: g.Key,
                Permissions: g.ToList()))
            .OrderBy(s => s.Resource)
            .ToList();
    }
}

/// <summary>
///     Represents a permission scope (a group of permissions for a single resource).
/// </summary>
/// <param name="Resource">The resource name (e.g., "users", "content").</param>
/// <param name="Permissions">All permissions defined for this resource.</param>
public sealed record PermissionScope(string Resource, IReadOnlyList<Permission> Permissions)
{
    /// <summary>
    ///     Gets all permission keys for this scope.
    /// </summary>
    public IEnumerable<string> Keys => Permissions.Select(p => p.Key);

    /// <summary>
    ///     Gets the wildcard permission key for this scope.
    /// </summary>
    public string Wildcard => $"{Resource}:*";
}
