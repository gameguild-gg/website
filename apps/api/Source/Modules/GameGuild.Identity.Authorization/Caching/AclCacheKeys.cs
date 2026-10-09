using System.Security.Cryptography;
using System.Text;

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
///         segment is therefore replaced by its SHA-256 fingerprint (fixed-length uppercase hex, which
///         never contains the delimiter), mirroring the bulk permission key builder. Distinct inputs
///         always occupy distinct, delimiter-unambiguous key segments.
///     </para>
///     <para>
///         GUID-valued segments (tenant, user, role, and group IDs) and the numeric version suffixes
///         use fixed canonical formats and are interpolated directly to keep keys debuggable.
///     </para>
/// </remarks>
public static class AclCacheKeys
{
    /// <summary>
    ///     Returns a delimiter-safe, fixed-length fingerprint for a free-form cache key segment.
    /// </summary>
    /// <param name="value">The raw segment value; <c>null</c> is fingerprinted as <c>none</c>.</param>
    public static string FingerprintCacheKeyPart(string? value) =>
        value is null ? "none" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    ///     Returns the two fingerprinted segments for a resource (type then ID), colon-delimited.
    /// </summary>
    public static string BuildResourceSegment(string resourceType, string resourceId) =>
        $"{FingerprintCacheKeyPart(resourceType)}:{FingerprintCacheKeyPart(resourceId)}";

    /// <summary>
    ///     Builds the legacy user-based ACL cache key:
    ///     <c>acl:{tenantId}:{userId}:{fingerprint(resourceType)}:{fingerprint(resourceId)}:tv..:uv..:gv..</c>.
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
    ///     <c>acl:subj:{tenantId}:{user|anon}:{roles|nr}:{groups|ng}:{fingerprint(resourceType)}:{fingerprint(resourceId)}:tv..:uv..:gv..</c>.
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
