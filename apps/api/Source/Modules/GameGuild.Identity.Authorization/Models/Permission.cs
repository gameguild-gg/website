namespace GameGuild.Identity.Authorization.Models;

/// <summary>
///     Strongly-typed base class for permissions, providing compile-time safety
///     and preventing typo-based security bypasses.
/// </summary>
/// <remarks>
///     <para>
///         This class replaces stringly-typed permission checks with type-safe objects.
///         Each permission has a unique key following the format: resource:action[:scope]
///     </para>
///     <para>
///         Example: Instead of checking HasPermission("users:write"), use HasPermission(UsersPermission.Write)
///         This provides compile-time safety and IntelliSense support.
///     </para>
/// </remarks>
public abstract class Permission : IEquatable<Permission>
{
    /// <summary>
    ///     Gets the unique permission key (e.g., "users:read", "content:admin").
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Gets the resource this permission applies to (e.g., "users", "content").
    /// </summary>
    public string Resource { get; }

    /// <summary>
    ///     Gets the action this permission allows (e.g., "read", "write", "delete").
    /// </summary>
    public string Action { get; }

    /// <summary>
    ///     Gets the optional scope qualifier (e.g., "self", "all").
    /// </summary>
    public string? Scope { get; }

    /// <summary>
    ///     Gets a human-readable description of this permission.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Initializes a new permission with the specified components.
    /// </summary>
    /// <param name="resource">The resource name (e.g., "users").</param>
    /// <param name="action">The action name (e.g., "read").</param>
    /// <param name="scope">Optional scope qualifier (e.g., "self").</param>
    /// <param name="description">Human-readable description.</param>
    protected Permission(string resource, string action, string? scope, string description)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(description);

        Resource = resource;
        Action = action;
        Scope = scope;
        Description = description;

        // Build key: resource:action or resource:action:scope
        Key = scope != null ? $"{resource}:{action}:{scope}" : $"{resource}:{action}";
    }

    /// <summary>
    ///     Implicitly converts a Permission to its string key for backward compatibility.
    /// </summary>
    public static implicit operator string(Permission permission) => permission.Key;

    /// <summary>
    ///     Returns the permission key as a string.
    /// </summary>
    public override string ToString() => Key;

    /// <inheritdoc />
    public bool Equals(Permission? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Key == other.Key;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Permission other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Key.GetHashCode();

    /// <summary>
    ///     Compares two permissions for equality.
    /// </summary>
    public static bool operator ==(Permission? left, Permission? right) => Equals(left, right);

    /// <summary>
    ///     Compares two permissions for inequality.
    /// </summary>
    public static bool operator !=(Permission? left, Permission? right) => !Equals(left, right);
}
