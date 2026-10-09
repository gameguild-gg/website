using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Teams;

public interface ITeamAuthorizationService
{
    Task<bool> CanCreateAsync(CancellationToken cancellationToken = default);
    Task<bool> HasAuthorityAsync(Guid teamId, TeamMemberAuthority required, CancellationToken cancellationToken = default);
    Task<bool> CanRestoreAsync(Guid teamId, CancellationToken cancellationToken = default);
    IQueryable<Team> ApplyPersonalAccess(IQueryable<Team> query, bool includeArchived = false) => query.Where(_ => false);
    IQueryable<Team> ApplyMembershipAccess(IQueryable<Team> query, bool includeArchived = false);
}

public sealed class TeamAuthorizationService(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ITeamAuthorizationService
{
    public async Task<bool> CanCreateAsync(CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || actor.TenantId is not { } tenantId)
        {
            return false;
        }

        return await IsActiveUserAsync(userId, cancellationToken).ConfigureAwait(false) &&
               await context.Set<TenantMember>().AsNoTracking().AnyAsync(member =>
                   member.UserId == userId &&
                   member.TenantId == tenantId &&
                   member.IsActive &&
                   member.DeletedAt == null,
                   cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasAuthorityAsync(
        Guid teamId,
        TeamMemberAuthority required,
        CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId)
        {
            return false;
        }

        var team = await context.Set<Team>().AsNoTracking()
            .Where(candidate => candidate.Id == teamId && candidate.IsActive && candidate.DeletedAt == null)
            .Select(candidate => new { candidate.TenantId, candidate.Visibility })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (team == null)
        {
            return false;
        }

        if (actor.TenantId == null || team.TenantId != actor.TenantId)
        {
            return false;
        }

        if (!await IsActiveUserAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var activeTenantMember = await context.Set<TenantMember>().AsNoTracking().AnyAsync(member =>
            member.UserId == userId &&
            member.TenantId == actor.TenantId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (!activeTenantMember)
        {
            return false;
        }

        if (CanManageTeams(actor))
        {
            return true;
        }

        var authority = await context.Set<TeamMember>().AsNoTracking()
            .Where(member =>
                member.TeamId == teamId &&
                member.UserId == userId &&
                member.IsActive &&
                member.LeftAt == null &&
                member.DeletedAt == null)
            .Select(member => (TeamMemberAuthority?)member.Authority)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return authority.HasValue && authority.Value >= required;
    }

    public async Task<bool> CanRestoreAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || actor.TenantId is not { } tenantId)
        {
            return false;
        }

        var teamTenantId = await context.Set<Team>().IgnoreQueryFilters().AsNoTracking()
            .Where(candidate => candidate.Id == teamId && candidate.DeletedAt == null)
            .Select(candidate => candidate.TenantId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (teamTenantId != tenantId || !await IsActiveUserAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!await context.Set<TenantMember>().AsNoTracking().AnyAsync(member =>
                member.UserId == userId && member.TenantId == tenantId && member.IsActive && member.DeletedAt == null,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (CanManageTeams(actor))
        {
            return true;
        }

        return await context.Set<TeamMember>().AsNoTracking().AnyAsync(member =>
            member.TeamId == teamId &&
            member.UserId == userId &&
            member.Authority == TeamMemberAuthority.Owner &&
            member.IsActive &&
            member.LeftAt == null &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
    }

    public IQueryable<Team> ApplyPersonalAccess(IQueryable<Team> query, bool includeArchived = false)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId)
        {
            return query.Where(team => team.Visibility == TeamVisibility.Public && (includeArchived || team.IsActive));
        }

        if (actor.TenantId is not { } tenantId)
        {
            return query.Where(team => team.Visibility == TeamVisibility.Public && (includeArchived || team.IsActive));
        }

        return query.Where(team =>
            team.TenantId == tenantId &&
            (includeArchived || team.IsActive) &&
            team.DeletedAt == null &&
            context.Set<TeamMember>().Any(member =>
                member.TeamId == team.Id &&
                member.UserId == userId &&
                member.IsActive &&
                member.LeftAt == null &&
                member.DeletedAt == null));
    }

    public IQueryable<Team> ApplyMembershipAccess(IQueryable<Team> query, bool includeArchived = false)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || actor.TenantId is not { } tenantId)
        {
            return ApplyPersonalAccess(query, includeArchived);
        }

        if (!CanManageTeams(actor))
        {
            return ApplyPersonalAccess(query, includeArchived);
        }

        return query.Where(team =>
            team.TenantId == tenantId &&
            (includeArchived || team.IsActive) &&
            team.DeletedAt == null &&
            context.Set<TenantMember>().Any(member =>
                member.UserId == userId &&
                member.TenantId == tenantId &&
                member.IsActive &&
                member.DeletedAt == null));
    }

    private Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Set<User>().AsNoTracking().AnyAsync(user =>
            user.Id == userId &&
            user.IsActive &&
            !user.IsSuspended &&
            user.DeletedAt == null,
            cancellationToken);

    private static bool CanManageTeams(ActorContext actor) =>
        actor.IsTenantAdmin || actor.HasPermission(TeamPermission.Keys.Admin);
}
