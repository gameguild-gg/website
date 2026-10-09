using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TestingLab;

/// <summary>
///     Central active-user and active-membership check for Testing Lab handlers that do not pass through
///     the standard authorization behavior. Administrative roles grant authority only inside the
///     currently selected tenant; they never replace tenant membership.
/// </summary>
internal static class TestingLabActorAccess
{
    public static async Task<bool> IsActiveTenantActorAsync(
        IApplicationDbContext context,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        var userId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || userId == null || actor.TenantId == null)
        {
            return false;
        }

        var activeUser = await context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user =>
                user.Id == userId.Value &&
                user.IsActive &&
                !user.IsSuspended &&
                user.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!activeUser)
        {
            return false;
        }

        return await context.Set<TenantMember>()
            .AsNoTracking()
            .AnyAsync(member =>
                member.UserId == userId.Value &&
                member.TenantId == actor.TenantId.Value &&
                member.IsActive &&
                member.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
