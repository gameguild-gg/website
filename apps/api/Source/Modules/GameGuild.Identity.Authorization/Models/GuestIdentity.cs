using System.Security.Claims;

namespace GameGuild.Identity.Authorization.Models;

/// <summary>
///     Guest identity namespace (issue #213). Guests are anonymous principals that receive a
///     short-lived session with an explicit, minimal permission allowlist; they never resolve
///     to a real user id and live entirely in their own claim/role namespace.
/// </summary>
/// <remarks>
///     <para>
///         A principal is a guest only when it carries a parseable <see cref="ClaimNames.GuestId"/>
///         claim AND the <see cref="RoleName"/> role, and carries NO user-id claim
///         (<c>sub</c>/<c>UserId</c>/NameIdentifier). Any principal mixing guest and user
///         identity claims is rejected by every check in this class (fail closed).
///     </para>
///     <para>
///         Guest permissions are NOT stored in or resolvable from the tenant permission
///         tables: <see cref="GuestPermission.Allowlist"/> is the single enumerated source,
///         and every permission outside it is denied to guests.
///     </para>
/// </remarks>
public static class GuestIdentity
{
    /// <summary>
    ///     The only role value a guest principal may carry. It is a distinct namespace value:
    ///     tenant membership roles are never called "Guest" by the provisioning flow.
    /// </summary>
    public const string RoleName = "Guest";
}

/// <summary>
///     Fail-closed guest-principal helpers shared by the authorization handlers and services.
/// </summary>
public static class GuestPrincipalExtensions
{
    /// <summary>
    ///     Whether the principal is a well-formed guest: guest id + guest role + no user-id claims.
    /// </summary>
    public static bool IsGuest(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!principal.Claims.Any(claim =>
                string.Equals(claim.Type, ClaimNames.Role, StringComparison.Ordinal) &&
                string.Equals(claim.Value, GuestIdentity.RoleName, StringComparison.Ordinal)))
        {
            return false;
        }

        // Any user-id claim disqualifies the guest namespace: a principal may not be both.
        if (principal.FindFirst(ClaimNames.Subject) is not null ||
            principal.FindFirst(ClaimNames.UserId) is not null ||
            principal.FindFirst(ClaimNames.NameIdentifier) is not null)
        {
            return false;
        }

        return principal.TryGetGuestId(out _);
    }

    /// <summary>
    ///     Parses the guest session id. An empty or unparseable value is not a guest (fail closed).
    /// </summary>
    public static bool TryGetGuestId(this ClaimsPrincipal principal, out Guid guestId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        guestId = Guid.Empty;
        var value = principal.FindFirst(ClaimNames.GuestId)?.Value;
        return !string.IsNullOrEmpty(value) && Guid.TryParse(value, out guestId) && guestId != Guid.Empty;
    }

    /// <summary>
    ///     Whether the guest principal holds a permission. True only when the principal is a
    ///     well-formed guest and the permission is one of the enumerated
    ///     <see cref="GuestPermission.Allowlist"/> entries — guests can never hold any other
    ///     permission, regardless of what any other claim says.
    /// </summary>
    public static bool HasGuestPermission(this ClaimsPrincipal principal, string permissionKey)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        return IsGuest(principal) && GuestPermission.Allowlist.Contains(permissionKey);
    }

    /// <summary>
    ///     The permissions a guest principal actually holds: the full enumerated allowlist when
    ///     the principal is a well-formed guest, and nothing otherwise.
    /// </summary>
    public static IReadOnlyList<string> GetGuestPermissions(this ClaimsPrincipal principal)
    {
        return IsGuest(principal) ? GuestPermission.Allowlist : [];
    }
}
