namespace GameGuild.Identity.Tenants;

/// <summary>
///     Strongly-typed tenant role constants to prevent magic string typos.
///     Use these constants when assigning or checking TenantMember.Role values.
/// </summary>
/// <remarks>
///     <para>
///         These roles represent the standard membership levels within a tenant.
///         Custom roles can still be created as strings, but these constants
///         should be used for the common built-in roles.
///     </para>
///     <para>
///         The implicit string conversion allows backward compatibility with
///         existing code that uses string-based role checks.
///     </para>
/// </remarks>
public sealed class TenantRole
{
    /// <summary>
    ///     The string value of this role
    /// </summary>
    public string Value { get; }

    private TenantRole(string value) => Value = value;

    /// <summary>
    ///     Owner role - Full control over the tenant including deletion
    /// </summary>
    public static readonly TenantRole Owner = new("Owner");

    /// <summary>
    ///     Administrator role - Full management capabilities except tenant deletion
    /// </summary>
    public static readonly TenantRole Admin = new("Admin");

    /// <summary>
    ///     Moderator role - Can manage content and members but not settings
    /// </summary>
    public static readonly TenantRole Moderator = new("Moderator");

    /// <summary>
    ///     Member role - Standard access with read/write on own content
    /// </summary>
    public static readonly TenantRole Member = new("Member");

    /// <summary>
    ///     Guest role - Limited read-only access
    /// </summary>
    public static readonly TenantRole Guest = new("Guest");

    /// <summary>
    ///     Contributor role - Can create content but limited editing of others
    /// </summary>
    public static readonly TenantRole Contributor = new("Contributor");

    /// <summary>
    ///     Viewer role - Read-only access to public content
    /// </summary>
    public static readonly TenantRole Viewer = new("Viewer");

    /// <summary>
    ///     All defined tenant roles for validation
    /// </summary>
    public static IReadOnlyList<TenantRole> All => new[]
    {
        Owner,
        Admin,
        Moderator,
        Member,
        Guest,
        Contributor,
        Viewer
    };

    /// <summary>
    ///     Roles with administrative privileges
    /// </summary>
    public static IReadOnlyList<TenantRole> AdminRoles => new[]
    {
        Owner,
        Admin
    };

    /// <summary>
    ///     Roles that can manage content
    /// </summary>
    public static IReadOnlyList<TenantRole> ContentManagerRoles => new[]
    {
        Owner,
        Admin,
        Moderator
    };

    /// <summary>
    ///     Checks if this role has administrative privileges
    /// </summary>
    public bool IsAdmin => this == Owner || this == Admin;

    /// <summary>
    ///     Checks if this role can manage content
    /// </summary>
    public bool CanManageContent => IsAdmin || this == Moderator;

    /// <summary>
    ///     Checks if this role can create content
    /// </summary>
    public bool CanCreateContent => CanManageContent || this == Member || this == Contributor;

    /// <summary>
    ///     Implicit conversion to string for backward compatibility
    /// </summary>
    public static implicit operator string(TenantRole role) => role.Value;

    /// <summary>
    ///     Creates a TenantRole from a string value
    /// </summary>
    public static TenantRole FromString(string value)
    {
        var match = All.FirstOrDefault(r => r.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        return match ?? new TenantRole(value);
    }

    /// <summary>
    ///     Checks if a string value matches a known role
    /// </summary>
    public static bool IsKnownRole(string value)
    {
        return All.Any(r => r.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Validates that a role string is a known role
    /// </summary>
    /// <returns>True if valid known role, false otherwise</returns>
    public static bool TryParse(string value, out TenantRole? role)
    {
        role = All.FirstOrDefault(r => r.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        return role != null;
    }

    public override string ToString() => Value;

    public override bool Equals(object? obj)
    {
        if (obj is TenantRole other)
        {
            return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
        }

        if (obj is string str)
        {
            return Value.Equals(str, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public override int GetHashCode() => Value.ToUpperInvariant().GetHashCode();

    public static bool operator ==(TenantRole? left, TenantRole? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Value.Equals(right.Value, StringComparison.OrdinalIgnoreCase);
    }

    public static bool operator !=(TenantRole? left, TenantRole? right) => !(left == right);

    public static bool operator ==(TenantRole? left, string? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Value.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    public static bool operator !=(TenantRole? left, string? right) => !(left == right);
}
