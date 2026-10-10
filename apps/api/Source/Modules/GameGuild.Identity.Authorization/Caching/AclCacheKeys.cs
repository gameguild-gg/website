namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     Builds collision-safe authorization ACL cache keys.
/// </summary>
/// <remarks>
///     <para>
///         Free-form segments such as <c>resourceType</c> and <c>resourceId</c> are attacker- and
///         data-controlled strings that may contain the colon (<c>:</c>) key delimiter. Interpolating them
///         raw would let two distinct pairs — for example (<c>a:b</c>, <c>c</c>) and (<c>a</c>, <c>b:c</c>) —
///         produce the same cache key and share each other's cached access decisions. Every free-form
///         segment is therefore emitted as a length-prefixed component, <c>{length}:{value}</c>: the
///         leading character count lets a reader consume exactly that many characters, so delimiters
///         inside the value can never shift the segment boundaries and distinct inputs always occupy
///         distinct keys (the encoding is injective). Unlike an opaque hash, the raw value stays embedded
///         in the key, so wildcard and substring invalidation patterns — which are built from the same
///         length-prefixed components — keep matching resource entries.
///     </para>
///     <para>
///         GUID-valued segments (tenant, user, role, and group IDs) and the numeric version suffixes
///         use fixed canonical formats and are interpolated directly to keep keys debuggable.
///         A <c>null</c> free-form segment is encoded the same way as the empty string.
///     </para>
/// </remarks>
public static class AclCacheKeys
{
    /// <summary>
    ///     Encodes a free-form cache key segment as a length-prefixed component:
    ///     <c>{length}:{value}</c>, where <paramref name="value"/> may contain the key delimiter.
    /// </summary>
    /// <param name="value">The raw segment value; <c>null</c> is encoded as the empty string.</param>
    public static string EncodeLengthPrefixedSegment(string? value)
    {
        var segment = value ?? string.Empty;
        return $"{segment.Length}:{segment}";
    }

    /// <summary>
    ///     Returns the two length-prefixed segments for a resource (type then ID), colon-delimited:
    ///     <c>{typeLength}:{resourceType}:{idLength}:{resourceId}</c>.
    /// </summary>
    public static string BuildResourceSegment(string resourceType, string resourceId) =>
        $"{EncodeLengthPrefixedSegment(resourceType)}:{EncodeLengthPrefixedSegment(resourceId)}";

    /// <summary>
    ///     Builds the legacy user-based ACL cache key:
    ///     <c>acl:{tenantId}:{userId}:{len(resourceType)}:{resourceType}:{len(resourceId)}:{resourceId}:tv..:uv..:gv..</c>.
    /// </summary>
    public static string BuildUserCacheKey(
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        long tenantVersion,
        long userVersion,
        long globalVersion)
    {
        return $"acl:{tenantId}:{userId}:{BuildResourceSegment(resourceType, resourceId)}:" +
               $"tv{tenantVersion}:uv{userVersion}:gv{globalVersion}";
    }

    /// <summary>
    ///     Builds the subject-based ACL cache key:
    ///     <c>acl:subj:{tenantId}:{user|anon}:{roles|nr}:{groups|ng}:{len(resourceType)}:{resourceType}:{len(resourceId)}:{resourceId}:tv..:uv..:gv..</c>.
    /// </summary>
    public static string BuildSubjectCacheKey(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        long tenantVersion,
        long userVersion,
        long globalVersion)
    {
        // Build a stable cache key from subject principals
        // Includes both tenant version (for tenant-wide changes) and user version (for user-specific changes)
        var userPart = subject.UserId?.ToString() ?? "anon";
        var rolesPart = subject.RoleIds.Count > 0 ? string.Join(",", subject.RoleIds.OrderBy(r => r)) : "nr";
        var groupsPart = subject.GroupIds.Count > 0 ? string.Join(",", subject.GroupIds.OrderBy(g => g)) : "ng";
        return $"acl:subj:{tenantId}:{userPart}:{rolesPart}:{groupsPart}:{BuildResourceSegment(resourceType, resourceId)}:" +
               $"tv{tenantVersion}:uv{userVersion}:gv{globalVersion}";
    }
}
