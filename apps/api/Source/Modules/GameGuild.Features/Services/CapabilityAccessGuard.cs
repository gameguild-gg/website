using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Features;

/// <summary>Validates the resolved actor before accessing tenant entitlements.</summary>
internal static class CapabilityAccessGuard
{
    internal static int? GetFailureStatus(ActorContext? actor, Guid tenantId, bool requireAdministrator)
    {
        if (actor is null || !actor.IsAuthenticated || actor.ActorKind == ActorKind.Anonymous ||
            string.IsNullOrWhiteSpace(actor.SubjectId) ||
            (actor.ActorKind == ActorKind.User && (!actor.SubjectIdAsGuid.HasValue || actor.SubjectIdAsGuid == Guid.Empty)))
        {
            return StatusCodes.Status401Unauthorized;
        }

        if (tenantId == Guid.Empty ||
            (!actor.IsSystemAdmin && (actor.TenantId != tenantId || (requireAdministrator && !actor.IsTenantAdmin))))
        {
            return StatusCodes.Status403Forbidden;
        }

        return null;
    }

    internal static Guid? RequireAdministrator(ActorContext actor, Guid tenantId)
    {
        if (GetFailureStatus(actor, tenantId, true).HasValue)
        {
            throw new UnauthorizedAccessException("An authenticated administrator for the target tenant is required.");
        }

        return actor.ActorKind == ActorKind.User ? actor.SubjectIdAsGuid : null;
    }
}
