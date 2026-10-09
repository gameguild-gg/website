namespace GameGuild.Identity.Authorization.Utilities;

/// <summary>
///     Permission-key matching helpers implementing the platform wildcard semantics:
///     <c>*</c> grants everything and <c>resource:*</c> grants every key under <c>resource</c>.
///     Denies are exact-key only, mirroring <see cref="EffectivePermissionResolverService"/>.
/// </summary>
public static class PermissionKeyMatcher
{
    /// <summary>
    ///     Checks whether a single stored grant covers a concrete permission key.
    /// </summary>
    /// <param name="grant">Stored grant (may be a wildcard).</param>
    /// <param name="key">Concrete permission key.</param>
    /// <returns>True when the grant covers the key.</returns>
    public static bool Covers(string grant, string key)
    {
        if (string.IsNullOrEmpty(grant) || string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (grant == "*")
        {
            return true;
        }

        if (string.Equals(grant, key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return grant.EndsWith(":*", StringComparison.Ordinal)
            && key.StartsWith(grant[..^1], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Checks whether any of the stored grants covers a concrete permission key.
    /// </summary>
    public static bool AnyCovers(IEnumerable<string> grants, string key)
        => grants.Any(grant => Covers(grant, key));

    /// <summary>
    ///     Checks whether the exact key is present in a set (exact match, used for deny semantics).
    /// </summary>
    public static bool ContainsExact(IEnumerable<string> keys, string key)
        => keys.Contains(key, StringComparer.OrdinalIgnoreCase);
}
