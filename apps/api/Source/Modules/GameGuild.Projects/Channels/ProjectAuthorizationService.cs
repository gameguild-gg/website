using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Teams;
using Microsoft.EntityFrameworkCore;
using ResourceTenantId = GameGuild.CQRS.Models.TenantId;
using ProjectAdminPermission = GameGuild.Identity.Authorization.ProjectPermission;

namespace GameGuild.Projects;

public interface IProjectAuthorizationService
{
    Task<bool> IsActorActiveTenantMemberAsync(CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid projectId, PermissionType permission, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionIncludingDeletedAsync(Guid projectId, PermissionType permission, CancellationToken cancellationToken = default)
        => HasPermissionAsync(projectId, permission, cancellationToken);

    IQueryable<Project> ApplyReadAccess(IQueryable<Project> query) => query;

    IQueryable<Project> ApplyPersonalAccess(IQueryable<Project> query, bool includeDeleted = false) => query.Where(_ => false);

    IQueryable<Project> ApplyWorkspaceAccess(IQueryable<Project> query, bool includeDeleted = false) => query.Where(_ => false);
}

public sealed class ProjectAuthorizationService(IApplicationDbContext context, IActorContextAccessor actorContextAccessor)
    : IProjectAuthorizationService
{
    public async Task<bool> HasPermissionAsync(Guid projectId, PermissionType permission, CancellationToken cancellationToken = default)
        => await HasPermissionCoreAsync(projectId, permission, false, cancellationToken).ConfigureAwait(false);

    public async Task<bool> HasPermissionIncludingDeletedAsync(Guid projectId, PermissionType permission, CancellationToken cancellationToken = default)
        => await HasPermissionCoreAsync(projectId, permission, true, cancellationToken).ConfigureAwait(false);

    private async Task<bool> HasPermissionCoreAsync(
        Guid projectId,
        PermissionType permission,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var projectQuery = includeDeleted
            ? context.Set<Project>().IgnoreQueryFilters()
            : context.Set<Project>();
        var project = await projectQuery
            .Where(project =>
                project.Id == projectId &&
                (includeDeleted || project.DeletedAt == null))
            .Select(project => new
            {
                project.CreatedById,
                project.Status,
                project.TenantId,
                project.Visibility
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (project == null)
        {
            return false;
        }

        if (permission == PermissionType.Read &&
            project.Visibility == ContentVisibility.Public &&
            project.Status == ContentStatus.Published)
        {
            return true;
        }

        if (!await IsActorActiveTenantMemberAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (project.TenantId != actor.TenantId)
        {
            return false;
        }

        var actorId = actor.SubjectIdAsGuid!.Value;
        if (CanManageProjects(actor))
        {
            return true;
        }

        var collaborators = await context.Set<ProjectCollaborator>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.ProjectId == projectId &&
                candidate.UserId == actorId &&
                candidate.IsActive &&
                candidate.DeletedAt == null &&
                candidate.LeftAt == null)
            .Select(candidate => new { candidate.Role, candidate.Permissions })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (collaborators.Any(candidate =>
                candidate.Role == ProjectRoles.Owner ||
                HasExactPermission(candidate.Permissions, permission)))
        {
            return true;
        }

        var projectTeams = await context.Set<ProjectTeam>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.ProjectId == projectId &&
                candidate.IsActive &&
                candidate.DeletedAt == null &&
                candidate.EndedAt == null)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.TeamId,
                candidate.Role,
                candidate.Permissions,
                candidate.ParticipationMode
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var projectTeam in projectTeams)
        {
            if (projectTeam.Role != ProjectTeamRole.Owner &&
                !HasExactPermission(projectTeam.Permissions, permission))
            {
                continue;
            }

            var activeMember = await context.Set<TeamMember>()
                .AsNoTracking()
                .AnyAsync(member =>
                    member.TeamId == projectTeam.TeamId &&
                    member.UserId == actorId &&
                    member.IsActive &&
                    member.LeftAt == null &&
                    member.DeletedAt == null &&
                    context.Set<Team>().Any(team =>
                        team.Id == member.TeamId &&
                        team.TenantId == project.TenantId &&
                        team.IsActive &&
                        team.DeletedAt == null) &&
                    (projectTeam.ParticipationMode == ProjectTeamParticipationMode.AllMembers ||
                     context.Set<ProjectMemberAllocation>().Any(allocation =>
                         allocation.ProjectTeamId == projectTeam.Id &&
                         allocation.UserId == actorId &&
                         allocation.IsActive &&
                         allocation.DeletedAt == null &&
                         allocation.StartsAt <= SystemClock.UtcNow &&
                         (!allocation.EndsAt.HasValue || allocation.EndsAt > SystemClock.UtcNow))),
                    cancellationToken)
                .ConfigureAwait(false);
            if (activeMember)
            {
                return true;
            }
        }

        var resourceTenantId = new ResourceTenantId(project.TenantId!.Value);
        var directGrants = await context.Set<ResourceUserPermission>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.TenantId == resourceTenantId &&
                candidate.UserId == actorId &&
                (candidate.ResourceType == nameof(Project) || candidate.ResourceType == "projects") &&
                candidate.ResourceId == projectId.ToString() &&
                candidate.RevokedAt == null &&
                (!candidate.ExpiresAt.HasValue || candidate.ExpiresAt > SystemClock.UtcNow))
            .Select(candidate => candidate.Permissions)
            .ToListAsync(
                cancellationToken)
            .ConfigureAwait(false);
        return directGrants.Any(grant => grant.Any(value =>
            Enum.TryParse<PermissionType>(value, true, out var parsed) && parsed == permission));
    }

    public IQueryable<Project> ApplyReadAccess(IQueryable<Project> query)
    {
        var actor = actorContextAccessor.ActorContext;
        var publicProjects = query.Where(project =>
            project.Visibility == ContentVisibility.Public &&
            project.Status == ContentStatus.Published);

        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid == null || actor.TenantId == null)
        {
            return publicProjects;
        }

        var actorId = actor.SubjectIdAsGuid.Value;
        var tenantId = actor.TenantId.Value;
        var isActiveUser = context.Set<User>().Any(user =>
            user.Id == actorId &&
            user.IsActive &&
            !user.IsSuspended &&
            user.DeletedAt == null);
        if (CanManageProjects(actor))
        {
            return query.Where(project =>
            (project.Visibility == ContentVisibility.Public && project.Status == ContentStatus.Published) ||
            (project.TenantId == tenantId &&
             isActiveUser &&
             context.Set<TenantMember>().Any(member =>
                 member.UserId == actorId &&
                 member.TenantId == tenantId &&
                 member.IsActive &&
                 member.DeletedAt == null)));
        }

        var resourceTenantId = new ResourceTenantId(tenantId);
        return query.Where(project =>
            (project.Visibility == ContentVisibility.Public && project.Status == ContentStatus.Published) ||
            (project.TenantId == tenantId &&
             context.Set<User>().Any(user =>
                 user.Id == actorId &&
                 user.IsActive &&
                 !user.IsSuspended &&
                 user.DeletedAt == null) &&
             context.Set<TenantMember>().Any(member =>
                 member.UserId == actorId &&
                 member.TenantId == tenantId &&
                 member.IsActive &&
                 member.DeletedAt == null) &&
             (project.Collaborators.Any(collaborator =>
                  collaborator.UserId == actorId &&
                  collaborator.IsActive &&
                  collaborator.DeletedAt == null &&
                  collaborator.LeftAt == null &&
                  (collaborator.Role == ProjectRoles.Owner ||
                  EF.Functions.Like(
                      "," + collaborator.Permissions.Replace(" ", "").Replace(";", ",").Replace("|", ",").ToUpper() + ",",
                      "%,READ,%"))) ||
              context.Set<ProjectTeam>().Any(projectTeam =>
                  projectTeam.ProjectId == project.Id &&
                  projectTeam.IsActive &&
                  projectTeam.DeletedAt == null &&
                  projectTeam.EndedAt == null &&
                  (projectTeam.Role == ProjectTeamRole.Owner ||
                  EF.Functions.Like(
                      "," + (projectTeam.Permissions ?? string.Empty).Replace(" ", "").Replace(";", ",").Replace("|", ",").ToUpper() + ",",
                      "%,READ,%")) &&
                  context.Set<Team>().Any(team =>
                      team.Id == projectTeam.TeamId &&
                      team.TenantId == project.TenantId &&
                      team.IsActive &&
                      team.DeletedAt == null) &&
                  context.Set<TeamMember>().Any(member =>
                      member.TeamId == projectTeam.TeamId &&
                      member.UserId == actorId &&
                      member.IsActive &&
                      member.LeftAt == null &&
                      member.DeletedAt == null) &&
                  (projectTeam.ParticipationMode == ProjectTeamParticipationMode.AllMembers ||
                   context.Set<ProjectMemberAllocation>().Any(allocation =>
                       allocation.ProjectTeamId == projectTeam.Id &&
                       allocation.UserId == actorId &&
                       allocation.IsActive &&
                       allocation.DeletedAt == null &&
                       allocation.StartsAt <= SystemClock.UtcNow &&
                       (!allocation.EndsAt.HasValue || allocation.EndsAt > SystemClock.UtcNow)))) ||
              context.Set<ResourceUserPermission>().Any(grant =>
                  grant.TenantId == resourceTenantId &&
                  grant.UserId == actorId &&
                  (grant.ResourceType == nameof(Project) || grant.ResourceType == "projects") &&
                  grant.ResourceId == project.Id.ToString() &&
                  grant.RevokedAt == null &&
                  (!grant.ExpiresAt.HasValue || grant.ExpiresAt > SystemClock.UtcNow) &&
                  grant.Permissions.Any(permission => permission == nameof(PermissionType.Read))))));
    }

    public IQueryable<Project> ApplyPersonalAccess(IQueryable<Project> query, bool includeDeleted = false)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actor.TenantId is not { } tenantId)
        {
            return query.Where(_ => false);
        }

        var resourceTenantId = new ResourceTenantId(tenantId);
        return query.Where(project =>
            project.TenantId == tenantId &&
            (includeDeleted || project.DeletedAt == null) &&
            context.Set<TenantMember>().Any(member =>
                member.UserId == actorId &&
                member.TenantId == tenantId &&
                member.IsActive &&
                member.DeletedAt == null) &&
            (project.Collaborators.Any(collaborator =>
                 collaborator.UserId == actorId &&
                 collaborator.IsActive &&
                 collaborator.DeletedAt == null &&
                 collaborator.LeftAt == null) ||
              context.Set<ProjectTeam>().Any(projectTeam =>
                  projectTeam.ProjectId == project.Id &&
                  projectTeam.IsActive &&
                  projectTeam.DeletedAt == null &&
                  projectTeam.EndedAt == null &&
                  (projectTeam.Role == ProjectTeamRole.Owner ||
                  EF.Functions.Like(
                      "," + (projectTeam.Permissions ?? string.Empty).Replace(" ", "").Replace(";", ",").Replace("|", ",").ToUpper() + ",",
                      "%,READ,%")) &&
                  context.Set<TeamMember>().Any(member =>
                      member.TeamId == projectTeam.TeamId &&
                     member.UserId == actorId &&
                      member.IsActive &&
                      member.LeftAt == null &&
                      member.DeletedAt == null) &&
                  context.Set<Team>().Any(team =>
                      team.Id == projectTeam.TeamId &&
                      team.TenantId == project.TenantId &&
                      team.IsActive &&
                      team.DeletedAt == null) &&
                  (projectTeam.ParticipationMode == ProjectTeamParticipationMode.AllMembers ||
                   context.Set<ProjectMemberAllocation>().Any(allocation =>
                       allocation.ProjectTeamId == projectTeam.Id &&
                       allocation.UserId == actorId &&
                       allocation.IsActive &&
                       allocation.DeletedAt == null &&
                       allocation.StartsAt <= SystemClock.UtcNow &&
                       (!allocation.EndsAt.HasValue || allocation.EndsAt > SystemClock.UtcNow)))) ||
             context.Set<ResourceUserPermission>().Any(grant =>
                 grant.TenantId == resourceTenantId &&
                 grant.UserId == actorId &&
                 (grant.ResourceType == nameof(Project) || grant.ResourceType == "projects") &&
                   grant.ResourceId == project.Id.ToString() &&
                   grant.RevokedAt == null &&
                   (!grant.ExpiresAt.HasValue || grant.ExpiresAt > SystemClock.UtcNow) &&
                   grant.Permissions.Any(permission => permission == nameof(PermissionType.Read)))));
    }

    public IQueryable<Project> ApplyWorkspaceAccess(IQueryable<Project> query, bool includeDeleted = false)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actor.TenantId is not { } tenantId)
        {
            return query.Where(_ => false);
        }

        if (!CanManageProjects(actor))
        {
            return ApplyPersonalAccess(query, includeDeleted);
        }

        return query.Where(project =>
            project.TenantId == tenantId &&
            (includeDeleted || project.DeletedAt == null) &&
            context.Set<TenantMember>().Any(member =>
                member.UserId == actorId &&
                member.TenantId == tenantId &&
                member.IsActive &&
                member.DeletedAt == null));
    }

    public async Task<bool> IsActorActiveTenantMemberAsync(CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || actorId == null || actor.TenantId == null)
        {
            return false;
        }

        var activeUser = await context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user =>
                user.Id == actorId.Value &&
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
                member.UserId == actorId.Value &&
                member.TenantId == actor.TenantId.Value &&
                member.IsActive &&
                member.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasExactPermission(string? permissions, PermissionType permission)
    {
        if (string.IsNullOrWhiteSpace(permissions))
        {
            return false;
        }

        return permissions
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(permission.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanManageProjects(ActorContext actor) =>
        actor.IsTenantAdmin || actor.HasPermission(ProjectAdminPermission.Keys.Admin);
}
