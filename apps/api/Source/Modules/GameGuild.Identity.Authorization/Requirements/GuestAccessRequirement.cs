using GameGuild.Identity.Authorization.Models;
using Microsoft.AspNetCore.Authorization;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Requirement admitting an anonymous guest principal to an endpoint explicitly marked
///     for guest access (issue #213). Carries an optional required guest permission; when
///     present it must be one of the enumerated <see cref="GuestPermission.Allowlist"/> keys —
///     the registration helper refuses to create policies for any other key, so guests are
///     denied every non-enumerated permission by construction (fail closed).
/// </summary>
public sealed class GuestAccessRequirement(string? requiredGuestPermission) : IAuthorizationRequirement
{
    /// <summary>
    ///     The enumerated guest permission the endpoint requires, or null when any well-formed
    ///     guest principal is admitted. Never a permission outside the allowlist.
    /// </summary>
    public string? RequiredGuestPermission { get; } =
        requiredGuestPermission is null
            ? null
            : GuestPermission.Allowlist.Contains(requiredGuestPermission)
                ? requiredGuestPermission
                : throw new ArgumentOutOfRangeException(
                    nameof(requiredGuestPermission),
                    requiredGuestPermission,
                    "Guest access requirements may only reference enumerated GuestPermission keys (fail closed).");
}
