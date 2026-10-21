using System.Security.Claims;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Issues and validates guest access tokens (issue #213). Guest tokens are HS256 JWTs in
///     their own audience namespace (<see cref="GuestAccessDefaults.Audience"/>) carrying only
///     the guest session id and the guest role — never a user id, email or tenant claim.
/// </summary>
public interface IGuestTokenService
{
    /// <summary>
    ///     Issues a short-lived guest token for an already-persisted guest session. The token
    ///     expires exactly at <paramref name="expiresAt"/>.
    /// </summary>
    string Issue(Guid guestSessionId, DateTime expiresAt);

    /// <summary>
    ///     Validates a guest token (signature, issuer, guest audience, lifetime) and returns
    ///     the principal. Returns null for any invalid or non-guest token; never throws for
    ///     malformed input.
    /// </summary>
    ClaimsPrincipal? Validate(string? guestToken);
}
