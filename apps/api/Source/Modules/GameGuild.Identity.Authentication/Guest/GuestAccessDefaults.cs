namespace GameGuild.Identity.Authentication;

/// <summary>
///     Constants for the anonymous guest access surface (issue #213). Guests authenticate
///     through a dedicated JWT bearer scheme whose tokens carry their own audience, so a
///     guest credential is structurally invalid on every non-guest endpoint (fail closed:
///     the default scheme rejects the guest audience outright, it is not merely unauthorized).
/// </summary>
public static class GuestAccessDefaults
{
    /// <summary>
    ///     The dedicated authentication scheme for guest sessions. Endpoints admitting guests
    ///     declare it explicitly with <c>[Authorize(AuthenticationSchemes = ...)]</c>; every
    ///     other endpoint keeps accepting only the standard bearer scheme.
    /// </summary>
    public const string AuthenticationScheme = "GuestBearer";

    /// <summary>
    ///     The audience carried by guest tokens. Distinct from the user access-token audience
    ///     so the standard bearer validation never accepts a guest token as a user credential.
    /// </summary>
    public const string Audience = "GameGuild.Guests";
}
