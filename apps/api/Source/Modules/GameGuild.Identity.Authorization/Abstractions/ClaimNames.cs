namespace GameGuild.Identity.Authorization;

/// <summary>
///     Centralized claim name constants used throughout the authorization system.
///     Ensures consistent claim extraction across all rule evaluators and handlers.
/// </summary>
public static class ClaimNames
{
    /// <summary>
    ///     Standard JWT subject claim (preferred for user ID).
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    ///     Legacy user ID claim.
    /// </summary>
    public const string UserId = "UserId";

    /// <summary>
    ///     .NET NameIdentifier claim type.
    /// </summary>
    public const string NameIdentifier = System.Security.Claims.ClaimTypes.NameIdentifier;

    /// <summary>
    ///     Primary tenant ID claim.
    /// </summary>
    public const string TenantId = "TenantId";

    /// <summary>
    ///     Alternative tenant ID claim (snake_case).
    /// </summary>
    public const string TenantIdAlt = "tenant_id";

    /// <summary>
    ///     Role claim for user roles.
    /// </summary>
    public const string Role = "role";

    /// <summary>
    ///     Group claim for user groups.
    /// </summary>
    public const string Group = "group";

    /// <summary>
    ///     Authentication Method Reference claim (for MFA detection).
    /// </summary>
    public const string Amr = "amr";

    /// <summary>
    ///     MFA verified claim.
    /// </summary>
    public const string MfaVerified = "mfa_verified";

    /// <summary>
    ///     MFA timestamp claim.
    /// </summary>
    public const string MfaTime = "mfa_time";

    /// <summary>
    ///     Alternative MFA timestamp claim.
    /// </summary>
    public const string MfaTimestamp = "mfa_timestamp";

    /// <summary>
    ///     Guest session id claim. Guests live in their own identity namespace: a guest
    ///     principal NEVER carries <see cref="Subject"/>, <see cref="UserId"/> or
    ///     <see cref="NameIdentifier"/> claims, so no guest can resolve to a real user id.
    /// </summary>
    public const string GuestId = "guest_id";

    /// <summary>
    ///     Email claim.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    ///     Email verified claim.
    /// </summary>
    public const string EmailVerified = "email_verified";

    /// <summary>
    ///     Extracts user ID from claims using the standard fallback chain.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns>The user ID claim value, or null if not found.</returns>
    [Obsolete("Use ClaimsExtractor.GetUserId instead for consistent claim extraction.")]
    public static string? GetUserId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Subject)?.Value
        ?? user.FindFirst(UserId)?.Value
        ?? user.FindFirst(NameIdentifier)?.Value;

    /// <summary>
    ///     Extracts tenant ID from claims using the standard fallback chain.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns>The tenant ID claim value, or null if not found.</returns>
    [Obsolete("Use ClaimsExtractor.GetTenantId instead for consistent claim extraction.")]
    public static string? GetTenantId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(TenantId)?.Value
        ?? user.FindFirst(TenantIdAlt)?.Value;

    /// <summary>
    ///     Tries to parse the user ID as a GUID.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <param name="userId">The parsed user ID.</param>
    /// <returns>True if the user ID was found and parsed successfully.</returns>
    [Obsolete("Use ClaimsExtractor.GetUserIdAsGuid instead for consistent claim extraction.")]
    public static bool TryGetUserId(System.Security.Claims.ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var claim = GetUserId(user);
        return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out userId);
    }

    /// <summary>
    ///     Tries to parse the tenant ID as a GUID.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <param name="tenantId">The parsed tenant ID.</param>
    /// <returns>True if the tenant ID was found and parsed successfully.</returns>
    [Obsolete("Use ClaimsExtractor.GetTenantIdAsGuid instead for consistent claim extraction.")]
    public static bool TryGetTenantId(System.Security.Claims.ClaimsPrincipal user, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var claim = GetTenantId(user);
        return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out tenantId);
    }
}
