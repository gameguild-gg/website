using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Authentication;

internal sealed record AuthorizedPermissionWriteScope(Guid? TenantId, Guid ActorId);

/// <summary>One actor/scope boundary for compatible and typed permission mutations.</summary>
internal static class PermissionWriteAuthorization
{
    public static Result<AuthorizedPermissionWriteScope> Resolve(IActorContextAccessor? actors, Guid? tenantId, bool globalScope)
    {
        var actor = actors?.ActorContext;
        if (actor is null || !actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty)
        {
            return Result.Failure<AuthorizedPermissionWriteScope>(Error.Unauthorized(
                "PermissionWrite.AuthenticationRequired", "An authenticated actor is required."));
        }
        if (tenantId == Guid.Empty || globalScope && tenantId.HasValue)
        {
            return Result.Failure<AuthorizedPermissionWriteScope>(Error.Validation(
                "PermissionWrite.InvalidScope", "A tenant identifier cannot be empty or combined with global scope."));
        }
        if (globalScope)
        {
            return actor.IsSystemAdmin || actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults)
                ? Result.Success(new AuthorizedPermissionWriteScope(null, actorId))
                : Result.Failure<AuthorizedPermissionWriteScope>(Denied("Global mutations require system:manage-global-defaults."));
        }
        var tenant = tenantId ?? actor.TenantId;
        if (!tenant.HasValue || tenant == Guid.Empty)
        {
            return Result.Failure<AuthorizedPermissionWriteScope>(Error.Validation(
                "PermissionWrite.InvalidScope", "An explicit tenant context is required."));
        }
        if (!actor.IsSystemAdmin && (tenant != actor.TenantId || !actor.IsTenantAdmin && !actor.HasPermission("permissions:manage")))
        {
            return Result.Failure<AuthorizedPermissionWriteScope>(Denied("Permission mutations require administration of the selected tenant."));
        }
        return Result.Success(new AuthorizedPermissionWriteScope(tenant, actorId));
    }

    private static Error Denied(string description) => Error.Forbidden("PermissionWrite.Forbidden", description);
}
