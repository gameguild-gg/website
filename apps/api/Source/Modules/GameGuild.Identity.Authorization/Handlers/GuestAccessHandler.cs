using GameGuild.Identity.Authorization.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Fail-closed handler for <see cref="GuestAccessRequirement"/> (issue #213).
/// </summary>
/// <remarks>
///     <para>
///         Only a well-formed guest principal passes: guest session id + guest role + no
///         user-id claims (<see cref="GuestPrincipalExtensions.IsGuest"/>). When the
///         requirement names a permission, the guest holds it only because it is one of the
///         enumerated <see cref="GuestPermission.Allowlist"/> entries — there is no other
///         source of guest permissions, so everything else is denied here.
///     </para>
///     <para>
///         Non-guest principals (including fully authenticated users) fail this requirement:
///         guest-marked endpoints are served over the dedicated guest scheme, and a user
///         token never authenticates there. This keeps guest access additive — it never
///         widens what the default <c>[Authorize]</c> surface accepts.
///     </para>
/// </remarks>
public sealed class GuestAccessHandler(ILogger<GuestAccessHandler> logger) : AuthorizationHandler<GuestAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, GuestAccessRequirement requirement)
    {
        var principal = context.User;

        if (!principal.IsGuest())
        {
            logger.LogInformation("Guest access denied: the principal is not a well-formed guest");
            context.Fail(new AuthorizationFailureReason(this, "Guest identity unavailable"));
            return Task.CompletedTask;
        }

        if (requirement.RequiredGuestPermission is { } permission &&
            !principal.HasGuestPermission(permission))
        {
            logger.LogInformation(
                "Guest permission denied: guests do not hold '{Permission}' (not in the enumerated allowlist)",
                permission);
            context.Fail(new AuthorizationFailureReason(this, $"Guest permission unavailable: {permission}"));
            return Task.CompletedTask;
        }

        principal.TryGetGuestId(out var guestId);
        logger.LogDebug("Guest access granted for guest session {GuestSessionId}", guestId);
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
